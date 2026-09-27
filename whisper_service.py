import os
import tempfile
from pathlib import Path

from faster_whisper import WhisperModel


_whisper_model = None


def get_whisper_model():
    global _whisper_model

    if _whisper_model is None:

        model_name = os.getenv(
            "WHISPER_MODEL",
            "base.en"
        )

        print(
            "Loading Whisper model:",
            model_name
        )

        _whisper_model = WhisperModel(
            model_name,
            device="cpu",
            compute_type="int8"
        )

    return _whisper_model


async def transcribe_upload(file):
    suffix = Path(
        file.filename or "speech.wav"
    ).suffix

    if not suffix:
        suffix = ".wav"

    audio_bytes = await file.read()

    with tempfile.NamedTemporaryFile(
        suffix=suffix,
        delete=False
    ) as temp_file:

        temp_file.write(
            audio_bytes
        )

        temp_path = temp_file.name


    try:
        model = get_whisper_model()

        segments, info = model.transcribe(
            temp_path,
            beam_size=5,
            vad_filter=True
        )

        text = " ".join(
            segment.text.strip()
            for segment in segments
        ).strip()

        print(
            "WHISPER TRANSCRIPT:",
            text
        )

        return text

    finally:
        try:
            os.remove(
                temp_path
            )
        except OSError:
            pass