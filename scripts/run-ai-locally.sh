#!/bin/bash
# Run AI service locally and expose to Azure via ngrok

set -e

PORT=${1:-8000}
USE_DOCKER=${USE_DOCKER:-false}

echo "=== Local AI Service Setup ==="
echo ""

# Check for NVIDIA GPU
check_gpu() {
    if command -v nvidia-smi &> /dev/null; then
        echo "✓ NVIDIA GPU detected!"
        nvidia-smi --query-gpu=name --format=csv,noheader
        return 0
    else
        echo "✗ No NVIDIA GPU detected - will use CPU (slower)"
        return 1
    fi
}

HAS_GPU=false
if check_gpu; then
    HAS_GPU=true
fi

# Run with Docker
if [ "$USE_DOCKER" = "true" ]; then
    echo ""
    echo "Starting AI service in Docker..."

    docker stop caption-service 2>/dev/null || true
    docker rm caption-service 2>/dev/null || true

    DOCKER_ARGS="-d --name caption-service -p $PORT:8000"
    DOCKER_ARGS="$DOCKER_ARGS -e MODEL_NAME=llava-hf/llava-1.5-7b-hf"
    DOCKER_ARGS="$DOCKER_ARGS -e CAPTION_MAX_LENGTH=280"

    if [ "$HAS_GPU" = true ]; then
        echo "Running with GPU support..."
        DOCKER_ARGS="$DOCKER_ARGS --gpus all"
    else
        echo "Running with CPU only..."
    fi

    docker run $DOCKER_ARGS captiongeneratoreqh42kmb646vq.azurecr.io/caption-service:latest

    echo "Container started!"
    echo "Waiting for service to be ready..."
    sleep 10

else
    echo ""
    echo "Starting AI service with Python..."

    # Check Python
    if ! command -v python3 &> /dev/null; then
        echo "Error: Python 3 not found"
        exit 1
    fi

    cd caption-service

    echo "Installing dependencies..."

    # Install PyTorch
    if [ "$HAS_GPU" = true ]; then
        echo "Installing PyTorch with CUDA support..."
        pip3 install torch==2.5.1 torchvision==0.20.1 --index-url https://download.pytorch.org/whl/cu124
    else
        echo "Installing PyTorch (CPU)..."
        pip3 install torch==2.5.1 torchvision==0.20.1
    fi

    pip3 install -r requirements.txt

    echo ""
    echo "Starting service on port $PORT..."
    echo "Press Ctrl+C to stop"
    echo ""

    # Run service
    python3 -m uvicorn main:app --host 0.0.0.0 --port $PORT &
    PYTHON_PID=$!
    sleep 5
fi

# Test the service
echo ""
echo "Testing local service..."
if curl -s http://localhost:$PORT/health > /dev/null; then
    echo "✓ Service is running!"
    curl -s http://localhost:$PORT/health | jq .
else
    echo "✗ Service is not responding"
    exit 1
fi

# Setup ngrok
echo ""
echo "=== Setting up Ngrok Tunnel ==="
echo ""

if ! command -v ngrok &> /dev/null; then
    echo "Installing ngrok..."

    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        curl -s https://ngrok-agent.s3.amazonaws.com/ngrok.asc | sudo tee /etc/apt/trusted.gpg.d/ngrok.asc >/dev/null
        echo "deb https://ngrok-agent.s3.amazonaws.com buster main" | sudo tee /etc/apt/sources.list.d/ngrok.list
        sudo apt update && sudo apt install ngrok
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        brew install ngrok/ngrok/ngrok
    else
        echo "Please install ngrok manually from https://ngrok.com/download"
        echo "Then run: ngrok http $PORT"
        exit 0
    fi
fi

echo "Starting ngrok tunnel on port $PORT..."
ngrok http $PORT > /dev/null &
sleep 3

# Get ngrok URL
NGROK_URL=$(curl -s http://localhost:4040/api/tunnels | jq -r '.tunnels[0].public_url')

echo ""
echo "=== SUCCESS ==="
echo ""
echo "Local AI Service: http://localhost:$PORT"
echo "Public URL (ngrok): $NGROK_URL"
echo ""
echo "Next steps:"
echo "1. Update your Azure API configuration:"
echo "   CaptionGeneration__BaseUrl = '$NGROK_URL'"
echo ""
echo "For Azure Container App:"
echo "   az containerapp update \\"
echo "     --name caption-generator-api \\"
echo "     --resource-group YOUR_RG \\"
echo "     --set-env-vars CaptionGeneration__BaseUrl='$NGROK_URL'"
echo ""
echo "Ngrok dashboard: http://localhost:4040"
echo ""
echo "Press Ctrl+C to stop all services"

# Wait
wait
