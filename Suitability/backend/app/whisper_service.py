"""Voice transcription via OpenAI Whisper, with clear offline messaging."""

from __future__ import annotations

import os
from typing import Any


def transcribe_bytes(data: bytes, filename: str = "audio.webm") -> dict[str, Any]:
    api_key = os.getenv("OPENAI_API_KEY", "").strip()
    if not api_key:
        return {
            "text": "",
            "provider": "none",
            "error": "Set OPENAI_API_KEY for server Whisper, or use on-device Transformers.js / browser speech.",
        }

    try:
        from openai import OpenAI
        import io

        client = OpenAI(api_key=api_key)
        buffer = io.BytesIO(data)
        buffer.name = filename
        result = client.audio.transcriptions.create(
            model=os.getenv("OPENAI_WHISPER_MODEL", "whisper-1"),
            file=buffer,
        )
        text = getattr(result, "text", None) or (result.get("text") if isinstance(result, dict) else "")
        return {"text": str(text or "").strip(), "provider": "openai-whisper"}
    except Exception as exc:  # noqa: BLE001
        return {"text": "", "provider": "openai-whisper", "error": str(exc)}
