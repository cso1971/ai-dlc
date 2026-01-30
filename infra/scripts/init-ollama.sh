#!/bin/bash
# Bash script to initialize Ollama with required models
# Run this after starting the Docker infrastructure

echo -e "\033[36mInitializing Ollama models...\033[0m"

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
    echo -e "\033[31mOllama failed to start. Make sure Docker is running and infrastructure is up.\033[0m"
    exit 1
fi

# Pull required models
models=("llama3.2" "nomic-embed-text")

for model in "${models[@]}"; do
    echo -e "\033[36mPulling model: $model ...\033[0m"
    docker exec playground-ollama ollama pull "$model"
    if [ $? -eq 0 ]; then
        echo -e "\033[32mModel $model pulled successfully!\033[0m"
    else
        echo -e "\033[31mFailed to pull model $model\033[0m"
    fi
done

echo ""
echo -e "\033[32mOllama initialization complete!\033[0m"
echo -e "\033[36mAvailable models:\033[0m"
docker exec playground-ollama ollama list
