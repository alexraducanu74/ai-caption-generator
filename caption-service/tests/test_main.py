import io
import sys
from pathlib import Path

import httpx
from fastapi.testclient import TestClient
from PIL import Image

SERVICE_DIR = Path(__file__).resolve().parents[1]
if str(SERVICE_DIR) not in sys.path:
    sys.path.insert(0, str(SERVICE_DIR))

import main as service


def _png_bytes() -> bytes:
    image = Image.new("RGB", (8, 8), color="red")
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    return buffer.getvalue()


def _client_with_stubbed_model(monkeypatch):
    monkeypatch.setattr(service, "ensure_model_loaded", lambda: ("model", "processor"))
    return TestClient(service.app)


def test_normalize_image_url_localhost_port():
    result = service.normalize_image_url("http://localhost:5000/img.jpg")
    assert result == "http://host.docker.internal:5000/img.jpg"


def test_normalize_image_url_remote_unchanged():
    source = "https://example.com/img.jpg"
    assert service.normalize_image_url(source) == source


def test_health_reports_starting_when_model_not_loaded(monkeypatch):
    monkeypatch.setattr(service, "_model", None)
    monkeypatch.setattr(service, "_processor", None)
    monkeypatch.setattr(service, "_model_load_error", None)

    payload = service.health()

    assert payload["status"] == "starting"
    assert payload["model"] == service.MODEL_NAME


def test_generate_caption_upload_success(monkeypatch):
    monkeypatch.setattr(service, "generate_caption", lambda *_: "A usable caption.")

    with _client_with_stubbed_model(monkeypatch) as client:
        response = client.post(
            "/generate-caption-upload",
            files={"file": ("image.png", _png_bytes(), "image/png")},
        )

    assert response.status_code == 200
    assert response.json() == {"caption": "A usable caption."}


def test_generate_caption_upload_rejects_empty_file(monkeypatch):
    with _client_with_stubbed_model(monkeypatch) as client:
        response = client.post(
            "/generate-caption-upload",
            files={"file": ("empty.png", b"", "image/png")},
        )

    assert response.status_code == 400
    assert response.json()["detail"] == "Uploaded file is empty."


def test_generate_caption_endpoint_success(monkeypatch):
    class FakeResponse:
        def __init__(self, content: bytes):
            self.content = content

        def raise_for_status(self):
            return None

    class FakeAsyncClient:
        requested_url = None

        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            return False

        async def get(self, url: str):
            FakeAsyncClient.requested_url = url
            return FakeResponse(_png_bytes())

    monkeypatch.setattr(service.httpx, "AsyncClient", FakeAsyncClient)
    monkeypatch.setattr(service, "generate_caption", lambda *_: "Caption from URL.")

    with _client_with_stubbed_model(monkeypatch) as client:
        response = client.post(
            "/generate-caption",
            json={"imageUrl": "http://localhost:1234/image.png"},
        )

    assert response.status_code == 200
    assert response.json() == {"caption": "Caption from URL."}
    assert (
        FakeAsyncClient.requested_url
        == "http://host.docker.internal:1234/image.png"
    )


def test_generate_caption_endpoint_download_failure_returns_502(monkeypatch):
    class FailingAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            return False

        async def get(self, url: str):
            request = httpx.Request("GET", url)
            raise httpx.RequestError("network error", request=request)

    monkeypatch.setattr(service.httpx, "AsyncClient", FailingAsyncClient)

    with _client_with_stubbed_model(monkeypatch) as client:
        response = client.post(
            "/generate-caption",
            json={"imageUrl": "https://example.com/image.png"},
        )

    assert response.status_code == 502
    assert "Failed to download image" in response.json()["detail"]


def test_generate_caption_post_processing(monkeypatch):
    class FakeTensor:
        shape = (1, 3)

    class FakeGeneratedIds:
        def __len__(self):
            return 5

        def detach(self):
            return self

        def cpu(self):
            return self

    class FakeOutputRow:
        def __getitem__(self, key):
            return FakeGeneratedIds()

    class FakeOutput:
        shape = (1, 8)

        def __getitem__(self, key):
            return FakeOutputRow()

    class FakeModel:
        def generate(self, **kwargs):
            return FakeOutput()

    class FakeProcessor:
        def __call__(self, **kwargs):
            return {"input_ids": FakeTensor()}

        def decode(self, _ids, skip_special_tokens=True):
            return "Assistant: hello world"

    class FakeTorch:
        @staticmethod
        def inference_mode():
            class _Context:
                def __enter__(self):
                    return None

                def __exit__(self, exc_type, exc_val, exc_tb):
                    return False

            return _Context()

    monkeypatch.setitem(sys.modules, "torch", FakeTorch)
    monkeypatch.setattr(service, "_model_device", "cpu")

    caption = service.generate_caption(
        Image.new("RGB", (8, 8)),
        FakeModel(),
        FakeProcessor(),
    )

    assert caption == "Hello world."
