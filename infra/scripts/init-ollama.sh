#!/bin/bash
# Initialize Ollama with required models.
# Supports both native (Apple Silicon Metal) and Docker Ollama.
#
# Usage:
#   ./init-ollama.sh          # auto-detect: prefer native, fallback to Docker
#   ./init-ollama.sh --native # force native Ollama
#   ./init-ollama.sh --docker # force Docker Ollama

MODE=""
if [ "$1" = "--native" ]; then
    MODE="native"
elif [ "$1" = "--docker" ]; then
    MODE="docker"
fi

echo -e "\033[36mInitializing Ollama models...\033[0m"

# Auto-detect mode if not specified
if [ -z "$MODE" ]; then
    if command -v ollama &> /dev/null && curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
        MODE="native"
        echo -e "\033[32mDetected native Ollama running locally (Metal GPU acceleration)\033[0m"
    elif docker ps --format '{{.Names}}' 2>/dev/null | grep -q playground-ollama; then
        MODE="docker"
        echo -e "\033[33mDetected Docker Ollama (CPU only)\033[0m"
    elif command -v ollama &> /dev/null; then
        MODE="native"
        echo -e "\033[33mNative Ollama found but not running. Checking if it starts...\033[0m"
    else
        MODE="docker"
        echo -e "\033[33mNo native Ollama found, using Docker Ollama\033[0m"
    fi
fi

# Pull function for native Ollama
pull_native() {
    local model=$1
    echo -e "\033[36mPulling model: $model (native)...\033[0m"
    ollama pull "$model"
    if [ $? -eq 0 ]; then
        echo -e "\033[32mModel $model pulled successfully!\033[0m"
    else
        echo -e "\033[31mFailed to pull model $model\033[0m"
        return 1
    fi
}

# Pull function for Docker Ollama
pull_docker() {
    local model=$1
    echo -e "\033[36mPulling model: $model (Docker)...\033[0m"
    docker exec playground-ollama ollama pull "$model"
    if [ $? -eq 0 ]; then
        echo -e "\033[32mModel $model pulled successfully!\033[0m"
    else
        echo -e "\033[31mFailed to pull model $model\033[0m"
        return 1
    fi
}

# Wait for Ollama to be ready
max_retries=30
retry_count=0

while [ $retry_count -lt $max_retries ]; do
    if curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
        echo -e "\033[32mOllama is ready!\033[0m"
        break
    fi
    retry_count=$((retry_count + 1))
    echo -e "\033[33mWaiting for Ollama to be ready... ($retry_count/$max_retries)\033[0m"
    sleep 2
done

if [ $retry_count -eq $max_retries ]; then
    echo -e "\033[31mOllama failed to start.\033[0m"
    if [ "$MODE" = "native" ]; then
        echo -e "\033[31mMake sure Ollama is installed and running: ollama serve\033[0m"
    else
        echo -e "\033[31mMake sure Docker is running and Ollama container is up:\033[0m"
        echo -e "\033[31m  cd infra && docker-compose --profile infra --profile ollama up -d\033[0m"
    fi
    exit 1
fi

# Pull required models
models=("llama3.2" "nomic-embed-text")

for model in "${models[@]}"; do
    if [ "$MODE" = "native" ]; then
        pull_native "$model"
    else
        pull_docker "$model"
    fi
done

echo ""
echo -e "\033[32mOllama initialization complete! (mode: $MODE)\033[0m"
echo -e "\033[36mAvailable models:\033[0m"
if [ "$MODE" = "native" ]; then
    ollama list
else
    docker exec playground-ollama ollama list
fi

if [ "$MODE" = "native" ]; then
    echo ""
    echo -e "\033[32m✓ Using native Ollama with Metal GPU acceleration\033[0m"
fi
