"""Generative virtual try-on: OpenAI / Replicate when keyed, else garment composite fallback."""

from __future__ import annotations

import base64
import io
import os
import time
from typing import Any
from urllib.parse import urlparse

import httpx
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps


def _decode_data_url(data: str) -> bytes:
    if "," in data and data.strip().startswith("data:"):
        data = data.split(",", 1)[1]
    return base64.b64decode(data)


def _encode_png(image: Image.Image) -> str:
    buffer = io.BytesIO()
    image.convert("RGB").save(buffer, format="PNG", optimize=True)
    return base64.b64encode(buffer.getvalue()).decode("ascii")


def _hex_to_rgba(color: str, alpha: int = 255) -> tuple[int, int, int, int]:
    value = color.lstrip("#")
    if len(value) == 3:
        value = "".join(ch * 2 for ch in value)
    try:
        r, g, b = int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16)
    except Exception:
        r, g, b = 90, 110, 120
    return r, g, b, alpha


def _load_garment(url: str | None, color: str) -> Image.Image:
    if url:
        try:
            parsed = urlparse(url)
            if parsed.scheme in ("http", "https"):
                response = httpx.get(url, timeout=12.0, follow_redirects=True)
                response.raise_for_status()
                return Image.open(io.BytesIO(response.content)).convert("RGBA")
            if url.startswith("/") or url.startswith("garments/"):
                candidates = [
                    os.path.join(os.path.dirname(__file__), "..", "..", "frontend", "public", url.lstrip("/")),
                    os.path.join(os.getcwd(), "frontend", "public", url.lstrip("/")),
                    os.path.join(os.getcwd(), "..", "frontend", "public", url.lstrip("/")),
                ]
                for path in candidates:
                    path = os.path.abspath(path)
                    if os.path.isfile(path):
                        return Image.open(path).convert("RGBA")
        except Exception:
            pass
    plate = Image.new("RGBA", (480, 560), (0, 0, 0, 0))
    draw = ImageDraw.Draw(plate)
    draw.rounded_rectangle((40, 40, 440, 520), radius=48, fill=_hex_to_rgba(color, 235))
    return plate


def _composite_fallback(person: Image.Image, garment: Image.Image, color: str) -> Image.Image:
    """Replace upper-body clothing region with an image-based garment plate."""
    base = person.convert("RGBA")
    width, height = base.size
    if width < 32 or height < 32:
        base = base.resize((max(32, width * 32), max(32, height * 32)), Image.Resampling.NEAREST)
        width, height = base.size
    left = int(width * 0.22)
    right = max(left + 8, int(width * 0.78))
    top = int(height * 0.18)
    bottom = max(top + 8, int(height * 0.72))

    mask = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(mask)
    draw.polygon(
        [
            (int(width * 0.32), top),
            (int(width * 0.68), top),
            (right, int(height * 0.34)),
            (int(width * 0.82), bottom),
            (int(width * 0.18), bottom),
            (left, int(height * 0.34)),
        ],
        fill=255,
    )
    mask = mask.filter(ImageFilter.GaussianBlur(18))

    desat = ImageOps.grayscale(base).convert("RGBA")
    desat = ImageEnhance.Brightness(desat).enhance(0.92)
    wash = Image.new("RGBA", base.size, _hex_to_rgba("#d8e0e4", 180))
    cleared = Image.composite(Image.blend(desat, wash, 0.55), base, mask)

    box_w, box_h = right - left, bottom - top
    fitted = ImageOps.contain(garment, (box_w, box_h))
    tint = Image.new("RGBA", fitted.size, _hex_to_rgba(color, 90))
    fitted = Image.alpha_composite(fitted, tint)
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ox = left + (box_w - fitted.size[0]) // 2
    oy = top + max(0, (box_h - fitted.size[1]) // 5)
    layer.paste(fitted, (ox, oy), fitted)

    garment_mask = Image.new("L", base.size, 0)
    garment_mask.paste(fitted.split()[-1], (ox, oy))
    garment_mask = garment_mask.filter(ImageFilter.GaussianBlur(4))
    garment_mask = ImageEnhance.Brightness(garment_mask).enhance(0.92)

    return Image.composite(layer, cleared, garment_mask).convert("RGB")


def _try_openai_edit(person_bytes: bytes, garment: Image.Image, prompt: str) -> str | None:
    api_key = os.getenv("OPENAI_API_KEY", "").strip()
    if not api_key:
        return None
    try:
        from openai import OpenAI

        client = OpenAI(api_key=api_key)
        person_file = io.BytesIO(person_bytes)
        person_file.name = "person.png"
        garment_buf = io.BytesIO()
        garment.convert("RGBA").save(garment_buf, format="PNG")
        garment_buf.seek(0)
        garment_buf.name = "garment.png"
        model = os.getenv("OPENAI_IMAGE_MODEL", "gpt-image-1")
        try:
            result = client.images.edit(
                model=model,
                image=[person_file, garment_buf],
                prompt=prompt,
                size=os.getenv("OPENAI_IMAGE_SIZE", "1024x1024"),
            )
        except TypeError:
            person_file.seek(0)
            result = client.images.edit(
                model=model,
                image=person_file,
                prompt=prompt,
                size=os.getenv("OPENAI_IMAGE_SIZE", "1024x1024"),
            )
        data = result.data[0]
        b64 = getattr(data, "b64_json", None)
        if b64:
            return b64
        url = getattr(data, "url", None)
        if url:
            raw = httpx.get(url, timeout=30.0).content
            return base64.b64encode(raw).decode("ascii")
    except Exception:
        return None
    return None


def _try_replicate_idm_vton(person_bytes: bytes, garment: Image.Image) -> str | None:
    token = os.getenv("REPLICATE_API_TOKEN", "").strip()
    if not token:
        return None
    try:
        person_b64 = base64.b64encode(person_bytes).decode("ascii")
        gbuf = io.BytesIO()
        garment.convert("RGB").save(gbuf, format="JPEG", quality=92)
        garment_b64 = base64.b64encode(gbuf.getvalue()).decode("ascii")
        model = os.getenv(
            "REPLICATE_TRYON_MODEL",
            "cuuupid/idm-vton:0513734a452173b8173e907e3a59d19a759aa11109bc925262fcb730e927885b",
        )
        version = model.split(":")[-1] if ":" in model else model
        create = httpx.post(
            "https://api.replicate.com/v1/predictions",
            headers={"Authorization": f"Token {token}", "Content-Type": "application/json"},
            json={
                "version": version,
                "input": {
                    "human_img": f"data:image/png;base64,{person_b64}",
                    "garm_img": f"data:image/jpeg;base64,{garment_b64}",
                    "garment_des": "clothing item for virtual try-on",
                },
            },
            timeout=30.0,
        )
        if create.status_code >= 400:
            return None
        prediction = create.json()
        get_url = prediction.get("urls", {}).get("get") or f"https://api.replicate.com/v1/predictions/{prediction.get('id')}"
        for _ in range(40):
            poll = httpx.get(get_url, headers={"Authorization": f"Token {token}"}, timeout=15.0)
            body = poll.json()
            status = body.get("status")
            if status == "succeeded":
                output = body.get("output")
                url = output[0] if isinstance(output, list) else output
                if not url:
                    return None
                raw = httpx.get(str(url), timeout=30.0).content
                return base64.b64encode(raw).decode("ascii")
            if status in {"failed", "canceled"}:
                return None
            time.sleep(1.2)
    except Exception:
        return None
    return None


def generate_tryon(
    person_image_base64: str,
    *,
    garment_image_url: str | None = None,
    garment_color: str = "#6b7c85",
    garment_name: str = "garment",
    size: str = "M",
    product_id: str = "",
) -> dict[str, Any]:
    person_bytes = _decode_data_url(person_image_base64)
    person = Image.open(io.BytesIO(person_bytes)).convert("RGBA")
    person.thumbnail((1024, 1280), Image.Resampling.LANCZOS)
    buf = io.BytesIO()
    person.convert("RGB").save(buf, format="PNG")
    person_bytes = buf.getvalue()

    garment = _load_garment(garment_image_url, garment_color)
    prompt = (
        f"Replace the person's current upper-body clothing with a realistic {garment_name} "
        f"in size {size} (product {product_id or 'catalog'}). Keep the same person, face, pose, "
        f"and background. The original clothing should be fully replaced, not layered as a silhouette. "
        f"Photorealistic fashion try-on."
    )

    openai_b64 = _try_openai_edit(person_bytes, garment, prompt)
    if openai_b64:
        return {
            "image_base64": openai_b64,
            "provider": "openai-images",
            "privacy": "Frame sent to OpenAI Images API for generative try-on. Not stored by Suitability.",
            "message": "Generated with OpenAI image edit.",
        }

    replicate_b64 = _try_replicate_idm_vton(person_bytes, garment)
    if replicate_b64:
        return {
            "image_base64": replicate_b64,
            "provider": "replicate-idm-vton",
            "privacy": "Frame sent to Replicate IDM-VTON for generative try-on. Not stored by Suitability.",
            "message": "Generated with Replicate virtual try-on.",
        }

    result = _composite_fallback(person, garment, garment_color)
    return {
        "image_base64": _encode_png(result),
        "provider": "composite-fallback",
        "privacy": "Processed on the Suitability API host only (no third-party vision API). Set OPENAI_API_KEY or REPLICATE_API_TOKEN for photo-realistic replacement.",
        "message": "API key not set — used image-based garment composite (replaces torso region).",
    }
