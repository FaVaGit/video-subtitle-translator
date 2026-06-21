#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

GREEN='\033[0;32m'; CYAN='\033[0;36m'; NC='\033[0m'

echo -e "${CYAN}Stopping all Video Subtitle Translator services...${NC}"
echo

# Kill .NET processes
pkill -f "VideoSubtitleTranslator" 2>/dev/null || true

# Stop NATS
killall nats-server 2>/dev/null || true
docker stop vst-nats 2>/dev/null || true
docker rm vst-nats 2>/dev/null || true

# Stop docker-compose
cd "$ROOT/docker"
docker compose down 2>/dev/null || true

echo -e "${GREEN}All services stopped.${NC}"
