import io
import logging
import os
from threading import Lock
from typing import Any

import httpx
from fastapi import FastAPI, File, HTTPException, UploadFile
from PIL import Image
from pydantic import BaseModel

logging.basicConfig(level=logging.INFO)
log = logging.getLogger("caption-service")

app = FastAPI(title="Caption Generator Service", version="5.0.0")

# Vision-Language Model (LLaVA - sees image + generates captions directly)
MODEL_NAME = os.getenv("MODEL_NAME", "llava-hf/llava-1.5-7b-hf")
CAPTION_MAX_LENGTH = int(os.getenv("CAPTION_MAX_LENGTH", "280"))

# Model and processor
_model: Any | None = None
_processor: Any | None = None
_model_device: str = "cpu"

_model_lock = Lock()
_model_load_error: str | None = None


class CaptionRequest(BaseModel):
    imageUrl: str


class CaptionResponse(BaseModel):
    caption: str


def normalize_image_url(image_url: str) -> str:
    """Convert localhost URLs to host.docker.internal for Docker compatibility."""
    from urllib.parse import urlparse, urlunparse
    
    parsed = urlparse(image_url)
    if parsed.hostname in {"localhost", "127.0.0.1"}:
        host = "host.docker.internal"
        netloc = f"{host}:{parsed.port}" if parsed.port is not None else host
        return urlunparse((parsed.scheme, netloc, parsed.path, parsed.params, parsed.query, parsed.fragment))
    return image_url


def ensure_model_loaded() -> tuple[Any, Any]:
    """Load the vision-language model (LLaVA) on first use."""
    global _model, _processor, _model_load_error, _model_device

    if _model and _processor:
        return _model, _processor

    with _model_lock:
        try:
            import torch
            from transformers import AutoProcessor, LlavaForConditionalGeneration

            use_cuda = torch.cuda.is_available()
            _model_device = "cuda:0" if use_cuda else "cpu"
            dtype = torch.float16 if use_cuda else torch.float32

            log.info("Loading vision-language model '%s' on %s...", MODEL_NAME, _model_device)

            _processor = AutoProcessor.from_pretrained(MODEL_NAME)
            _model = LlavaForConditionalGeneration.from_pretrained(
                MODEL_NAME,
                torch_dtype=dtype,
                low_cpu_mem_usage=True
            )
            _model.to(_model_device)
            _model.eval()

            _model_load_error = None
            log.info("Model loaded successfully on %s.", _model_device)

        except Exception as ex:
            _model_load_error = str(ex)
            log.error("Model load FAILED: %s", ex, exc_info=True)
            raise

    return _model, _processor


def generate_caption(image: Image.Image, model: Any, processor: Any) -> str:
    """Generate a caption from an image using LLaVA."""
    import torch

    try:
        log.debug("Processing image with model...")

        # LLaVA 1.5 expects an explicit image placeholder token in the prompt.
        user_instruction = (
            "Write an engaging social media caption for this image. "
            f"Be accurate to what you see and keep it under {CAPTION_MAX_LENGTH} characters. "
            "Output only the caption."
        )
        prompt = f"USER: <image>\n{user_instruction}\nASSISTANT:"
        log.debug(f"Prompt: {prompt}")

        inputs = processor(text=prompt, images=image, return_tensors="pt")
        inputs = {k: v.to(_model_device) if hasattr(v, "to") else v for k, v in inputs.items()}
        input_ids = inputs.get("input_ids")
        log.debug(f"Inputs shape: {input_ids.shape if input_ids is not None else 'N/A'}")

        log.debug("Generating caption with LLaVA...")
        with torch.inference_mode():
            output = model.generate(
                **inputs,
                max_new_tokens=50,
                min_new_tokens=8,
                do_sample=True,
                temperature=0.9,
                top_p=0.9,
                top_k=50
            )

        log.debug(f"Output shape: {output.shape}")

        # Decode only tokens generated after the prompt.
        input_length = input_ids.shape[1] if input_ids is not None else 0
        generated_ids = output[0][input_length:]
        log.debug(f"Generated IDs length: {len(generated_ids)}")
        caption = processor.decode(generated_ids.detach().cpu(), skip_special_tokens=True)
        log.debug(f"Raw caption: '{caption}'")

        caption = caption.strip()

        # Strip accidental role leakage.
        for prefix in ("ASSISTANT:", "Assistant:", "Caption:"):
            if caption.startswith(prefix):
                caption = caption[len(prefix):].strip()

        if caption and not caption.endswith((".", "!", "?")):
            caption = caption + "."

        if caption and caption[0].islower():
            caption = caption[0].upper() + caption[1:]

        # Keep full model output; API/domain validation enforces max storage length.
        log.info(f"Final caption: '{caption}'")
        return caption

    except Exception as ex:
        log.error("CRITICAL: Caption generation failed: %s", ex, exc_info=True)
        raise


@app.on_event("startup")
def preload_model_on_startup() -> None:
    """Warm up LLaVA so first caption request is fast and avoids client timeouts."""
    try:
        log.info("Preloading model during startup...")
        ensure_model_loaded()
        log.info("Startup preload complete.")
    except Exception as ex:
        # Keep service up so /health can surface the error via lastError.
        log.error("Startup preload failed: %s", ex, exc_info=True)


@app.get("/health")
def health() -> dict[str, str | None]:
    """Health check endpoint."""
    status = "ready" if _model and _processor else "starting"
    return {
        "status": status,
        "model": MODEL_NAME,
        "device": _model_device,
        "type": "Vision-Language (LLaVA)",
        "lastError": _model_load_error
    }


@app.post("/generate-caption-upload", response_model=CaptionResponse)
async def generate_caption_upload(file: UploadFile = File(...)) -> CaptionResponse:
    """Generate caption from uploaded image."""
    image_bytes = await file.read()
    if not image_bytes:
        raise HTTPException(status_code=400, detail="Uploaded file is empty.")

    try:
        log.debug("Reading image from upload...")
        image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        
        log.debug("Loading model...")
        model, processor = ensure_model_loaded()
        
        log.debug("Generating caption...")
        caption = generate_caption(image, model, processor)
        
        if not caption:
            raise HTTPException(status_code=500, detail="Failed to generate caption")
        
        return CaptionResponse(caption=caption)
        
    except HTTPException:
        raise
    except Exception as ex:
        log.error(f"Unhandled exception in generate_caption_upload: {ex}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Caption generation failed: {str(ex)}")


@app.post("/generate-caption", response_model=CaptionResponse)
async def generate_caption_endpoint(request: CaptionRequest) -> CaptionResponse:
    """Generate caption from image URL."""
    if not request.imageUrl.strip():
        raise HTTPException(status_code=400, detail="imageUrl is required.")

    target_url = normalize_image_url(request.imageUrl.strip())
    log.info(f"Fetching image from: {target_url}")

    try:
        async with httpx.AsyncClient(timeout=20, follow_redirects=True) as client:
            response = await client.get(target_url)
            response.raise_for_status()
        image_bytes = response.content
        log.info(f"Image downloaded successfully ({len(image_bytes)} bytes)")
    except Exception as ex:
        log.error(f"Failed to download image from {target_url}: {ex}", exc_info=True)
        raise HTTPException(status_code=502, detail=f"Failed to download image: {ex}") from ex

    try:
        log.debug("Reading image...")
        image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        
        log.debug("Loading model...")
        model, processor = ensure_model_loaded()
        
        log.debug("Generating caption...")
        caption = generate_caption(image, model, processor)
        
        if not caption:
            raise HTTPException(status_code=500, detail="Failed to generate caption")
        
        log.info(f"Caption generated successfully: '{caption}'")
        return CaptionResponse(caption=caption)
        
    except HTTPException:
        raise
    except Exception as ex:
        log.error(f"Unhandled exception in generate_caption_endpoint: {ex}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Caption generation failed: {str(ex)}")
