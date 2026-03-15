import argparse
import json
import urllib.request


def main() -> None:
    parser = argparse.ArgumentParser(description="Simple caption-service smoke test client")
    parser.add_argument("--base-url", default="http://localhost:8000", help="Caption service base URL")
    # Note: use a direct image URL (not Unsplash) — e.g. raw GitHub or imgur.
    # Example: https://raw.githubusercontent.com/pytorch/hub/master/images/dog.jpg
    parser.add_argument("--image-url", required=True, help="Direct public image URL (Unsplash blocks non-browser requests)")
    args = parser.parse_args()

    payload = json.dumps({"imageUrl": args.image_url}).encode("utf-8")
    request = urllib.request.Request(
        f"{args.base_url.rstrip('/')}/generate-caption",
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            body = response.read().decode("utf-8")
            print(f"HTTP {response.status}")
            print(body)
    except Exception as ex:
        print(f"Request failed: {ex}")


if __name__ == "__main__":
    main()

