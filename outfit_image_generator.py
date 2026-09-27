import base64
import hashlib
import re
from pathlib import Path
from dotenv import load_dotenv

from openai import OpenAI

load_dotenv()


BASE_DIR = Path(__file__).resolve().parent

OUTPUT_DIR = (
    BASE_DIR
    / "output"
    / "generated_outfits"
)

OUTPUT_DIR.mkdir(
    parents=True,
    exist_ok=True
)


client = OpenAI()


def safe_filename(value: str):
    return re.sub(
        r"[^A-Za-z0-9_-]+",
        "_",
        value
    )


def generate_outfit_image(
    item_id: str,
    description: str,
    category=None,
    material=None,
    pattern=None,
    length=None,
):
    """
    Generates ONE polished image representing
    the complete outfit described by the recommendation.

    Images are cached so the same recommendation
    does not regenerate every time.
    """

    description = description or ""

    fingerprint = hashlib.sha1(
        description.encode("utf-8")
    ).hexdigest()[:10]


    filename = (
        safe_filename(item_id)
        + "_"
        + fingerprint
        + ".jpg"
    )


    output_path = (
        OUTPUT_DIR
        / filename
    )


    # Already generated = instant.
    if output_path.exists():
        return filename


    prompt = f"""
Create a high-quality e-commerce fashion product image
showing ONE COMPLETE OUTFIT.

The recommendation description below is authoritative.
The generated outfit should visually match the garments,
materials, patterns, lengths, colors, and styling described.

RECOMMENDATION DESCRIPTION:
{description}

ADDITIONAL METADATA:
Category: {category or "unspecified"}
Material: {material or "unspecified"}
Pattern: {pattern or "unspecified"}
Length: {length or "unspecified"}

IMAGE REQUIREMENTS:

- Show the complete outfit in one image.
- Show every major garment described.
- If there is a top and bottom, place the top naturally
  above the bottom as a coordinated outfit.
- If the recommendation is a dress, show the complete dress.
- Front-facing view.
- NO human model.
- NO mannequin body.
- NO face.
- NO hands.
- NO text.
- NO labels.
- NO logos.
- NO watermark.
- Clean light neutral studio background.
- Professional high-end online clothing-store photography.
- Garments centered and large in frame.
- Crisp fabric detail.
- Realistic proportions.
- Minimal shadows.
- Do not invent unrelated garments.
- The final image should look like a clean product listing,
  not a fashion editorial photo.
"""


    print(
        "Generating outfit image:",
        item_id
    )


    result = client.images.generate(
        model="gpt-image-2.5-flare",
        prompt=prompt,
        size="1024x1536",
        quality="medium",
        output_format="jpeg",
    )


    image_base64 = (
        result.data[0].b64_json
    )


    image_bytes = (
        base64.b64decode(
            image_base64
        )
    )


    with open(
        output_path,
        "wb"
    ) as f:
        f.write(
            image_bytes
        )


    print(
        "Saved:",
        output_path
    )


    return filename