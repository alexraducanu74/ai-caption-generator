import io
import logging
import os
import re
from threading import Lock
from urllib.parse import urlparse, urlunparse
from typing import Any

import httpx
from fastapi import FastAPI, File, HTTPException, UploadFile
from PIL import Image
from pydantic import BaseModel

logging.basicConfig(level=logging.INFO)
log = logging.getLogger("caption-service")

app = FastAPI(title="Caption Generator Service", version="1.0.0")

MODEL_NAME = os.getenv("CAPTION_MODEL_NAME", "Salesforce/blip-image-captioning-base")

CAPTION_PROMPT = (
    "You are a professional photo captioner. Follow these rules exactly:\n"
    "- Describe only what is visible in the image.\n"
    "- Do not hallucinate or infer anything beyond what you see.\n"
    "- Keep the caption concise and social-media friendly.\n"
    "- Use plain, natural language; avoid hashtags, emojis, or special formatting.\n"
    "- Maximum length: 100 characters.\n"
    "- Make it friendly and engaging for social media.\n"
    "Caption:"
)

_processor: Any | None = None
_model: Any | None = None
_model_lock = Lock()
_model_load_error: str | None = None


class CaptionRequest(BaseModel):
    imageUrl: str


class CaptionResponse(BaseModel):
    caption: str


def normalize_image_url(image_url: str) -> str:
    parsed = urlparse(image_url)
    if parsed.hostname in {"localhost", "127.0.0.1"}:
        host = "host.docker.internal"
        netloc = f"{host}:{parsed.port}" if parsed.port is not None else host
        return urlunparse((parsed.scheme, netloc, parsed.path, parsed.params, parsed.query, parsed.fragment))
    return image_url


def ensure_model_loaded() -> tuple[Any, Any]:
    global _processor, _model, _model_load_error
    if _processor is not None and _model is not None:
        return _processor, _model

    with _model_lock:
        if _processor is None or _model is None:
            try:
                log.info("Loading BLIP model '%s' ...", MODEL_NAME)
                import torch
                from transformers import BlipForConditionalGeneration, BlipProcessor as BP

                _processor = BP.from_pretrained(MODEL_NAME)
                _model = BlipForConditionalGeneration.from_pretrained(MODEL_NAME)
                _model.eval()
                torch.set_num_threads(1)
                _model_load_error = None
                log.info("BLIP model loaded OK — status is now ready.")
            except Exception as ex:
                _model_load_error = str(ex)
                log.error("BLIP model load FAILED: %s", ex)
                raise

    return _processor, _model


def fallback_caption_from_name(name: str) -> str:
    stem = name.rsplit(".", 1)[0] if "." in name else name
    cleaned = re.sub(r"[_\-]+", " ", stem).strip()
    cleaned = re.sub(r"\s+", " ", cleaned)
    caption = f"A post featuring {cleaned}." if cleaned else "A moment worth sharing."
    return caption[:100]


def fallback_caption_from_url(image_url: str) -> str:
    parsed = urlparse(image_url)
    path = parsed.path.rsplit("/", 1)[-1]
    return fallback_caption_from_name(path)


def sanitize_generated_caption(raw_caption: str) -> str:
    """Remove prompt/rules text if the model echoes it, return only caption text."""
    if not raw_caption:
        return ""

    caption = raw_caption.strip()
    lowered = caption.lower()

    if lowered.startswith(CAPTION_PROMPT.lower()):
        caption = caption[len(CAPTION_PROMPT):].strip()
        lowered = caption.lower()

    marker = "caption:"
    marker_index = lowered.rfind(marker)
    if marker_index != -1:
        caption = caption[marker_index + len(marker):].strip()

    caption = re.sub(r"\s+", " ", caption).strip()
    return caption[:100]


def looks_like_prompt_echo(caption: str) -> bool:
    if not caption:
        return True

    lowered = caption.lower()
    return (
        "follow these rules" in lowered
        or "do not hallucinate" in lowered
        or "maximum length" in lowered
        or lowered.startswith("you are a professional photo captioner")
    )


def run_blip_inference(image_bytes: bytes) -> str:
    processor: Any | None = None
    model: Any | None = None
    try:
        processor, model = ensure_model_loaded()
    except Exception:
        log.warning("Model unavailable — will use fallback.")
        return ""

    try:
        import torch

        image = Image.open(io.BytesIO(image_bytes)).convert("RGB")

        # First try guided generation with rules.
        prompt_inputs = processor(images=image, text=CAPTION_PROMPT, return_tensors="pt")
        with torch.inference_mode():
            prompt_output = model.generate(**prompt_inputs, max_new_tokens=30)
        caption = processor.decode(prompt_output[0], skip_special_tokens=True).strip()
        caption = sanitize_generated_caption(caption)

        # If model echoes instructions, regenerate in unconditional caption mode.
        if looks_like_prompt_echo(caption):
            plain_inputs = processor(images=image, return_tensors="pt")
            with torch.inference_mode():
                plain_output = model.generate(**plain_inputs, max_new_tokens=30)
            caption = processor.decode(plain_output[0], skip_special_tokens=True).strip()
            caption = sanitize_generated_caption(caption)

        log.info("BLIP caption: %s", caption)
        return caption
    except Exception as ex:
        log.error("BLIP inference failed: %s", ex)
        return ""


@app.get("/health")
def health() -> dict[str, str | None]:
    status = "ready" if _processor is not None and _model is not None else "starting"
    return {"status": status, "model": MODEL_NAME, "lastError": _model_load_error}


@app.post("/generate-caption-upload", response_model=CaptionResponse)
async def generate_caption_upload(file: UploadFile = File(...)) -> CaptionResponse:
    image_bytes = await file.read()
    if not image_bytes:
        raise HTTPException(status_code=400, detail="Uploaded file is empty.")

    caption = run_blip_inference(image_bytes)
    if not caption:
        caption = fallback_caption_from_name(file.filename or "an image")
        log.info("Fallback caption: %s", caption)

    return CaptionResponse(caption=caption[:100])


@app.post("/generate-caption", response_model=CaptionResponse)
async def generate_caption(request: CaptionRequest) -> CaptionResponse:
    if not request.imageUrl.strip():
        raise HTTPException(status_code=400, detail="imageUrl is required.")

    target_url = normalize_image_url(request.imageUrl.strip())

    try:
        async with httpx.AsyncClient(timeout=20, follow_redirects=True) as client:
            response = await client.get(target_url)
            response.raise_for_status()
        image_bytes = response.content
    except Exception as ex:
        raise HTTPException(status_code=502, detail=f"Failed to download image: {ex}") from ex

    caption = run_blip_inference(image_bytes)
    if not caption:
        caption = fallback_caption_from_url(request.imageUrl.strip())
        log.info("Fallback caption: %s", caption)

    return CaptionResponse(caption=caption[:100])
