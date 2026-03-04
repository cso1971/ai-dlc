FROM python:3.12-slim

# Install Node.js 22 for Claude Code CLI + MCP server, and git for project root detection
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates git && \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Install Claude Code CLI globally
RUN npm install -g @anthropic-ai/claude-code

# Pre-cache the GitLab MCP server so first invocation is fast
RUN npx -y @modelcontextprotocol/server-gitlab --help || true

# Copy application code
COPY . .

# Create non-root user (Claude CLI refuses --dangerously-skip-permissions as root)
RUN useradd -m claude && \
    mkdir -p /app/sessions /home/claude/.claude /home/claude/repos && \
    git init /app && \
    chown -R claude:claude /app /home/claude

# Configure MCP servers in user-level settings (always trusted, no interactive approval needed)
COPY .mcp.json /tmp/.mcp.json
RUN node -e " \
  const mcp = JSON.parse(require('fs').readFileSync('/tmp/.mcp.json','utf8')); \
  const settings = { mcpServers: mcp.mcpServers }; \
  require('fs').writeFileSync('/home/claude/.claude/settings.json', JSON.stringify(settings, null, 2)); \
" && chown claude:claude /home/claude/.claude/settings.json

USER claude

EXPOSE 8000

CMD ["uvicorn", "src.main:app", "--host", "0.0.0.0", "--port", "8000"]
