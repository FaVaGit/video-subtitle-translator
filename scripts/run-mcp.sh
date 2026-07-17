#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MCP_DIR="$ROOT/mcp-server"
cd "$MCP_DIR"

echo
echo "[MCP] Starting Video Subtitle Translator MCP server..."
echo

if ! command -v node >/dev/null 2>&1; then
  echo "[ERROR] Node.js not found. Install Node.js 22+"
  exit 1
fi

if [ ! -d node_modules ]; then
  echo "Installing MCP dependencies..."
  npm install
fi

if [ -z "${GITHUB_TOKEN:-}" ] && command -v gh >/dev/null 2>&1; then
  export GITHUB_TOKEN="$(gh auth token || true)"
fi

echo "Running MCP server on stdio..."
echo "Tools: video_probe, video_extract_frame, video_extract_audio, github_models_chat"
echo

node server.js
