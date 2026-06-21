#!/usr/bin/env bash
set -euo pipefail

# ============================================================
# Desktop mode: Tauri + Backend
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

GREEN='\033[0;32m'; YELLOW='\033[0;33m'; RED='\033[0;31m'; CYAN='\033[0;36m'; NC='\033[0m'

PIDS=()
NATS_DOCKER=0; NATS_LOCAL=0

cleanup() {
    echo -e "\n${YELLOW}Shutting down...${NC}"
    for pid in "${PIDS[@]}"; do kill "$pid" 2>/dev/null || true; done
    [ "$NATS_DOCKER" -eq 1 ] && docker stop vst-nats &>/dev/null || true
    [ "$NATS_LOCAL" -eq 1 ] && killall nats-server 2>/dev/null || true
    echo -e "${GREEN}All services stopped.${NC}"
    exit 0
}
trap cleanup EXIT INT TERM

echo -e "${CYAN}[DESKTOP] Starting Tauri desktop app...${NC}"
echo

# Verify
command -v cargo &>/dev/null || { echo -e "${RED}Rust not found. Install from https://rustup.rs${NC}"; exit 1; }
command -v dotnet &>/dev/null || { echo -e "${RED}.NET SDK not found.${NC}"; exit 1; }

# ── NATS ──
echo -e "${CYAN}[1/3] Checking NATS...${NC}"
if ss -tlnp 2>/dev/null | grep -q ':4222' || netstat -tlnp 2>/dev/null | grep -q ':4222'; then
    echo -e "  ${GREEN}✓ NATS already running${NC}"
elif command -v nats-server &>/dev/null; then
    mkdir -p "$ROOT/data/nats"
    nats-server --jetstream --store_dir "$ROOT/data/nats" &>/dev/null &
    PIDS+=($!); NATS_LOCAL=1; sleep 2
    echo -e "  ${GREEN}✓ NATS started (local)${NC}"
elif command -v docker &>/dev/null && docker info &>/dev/null; then
    docker run -d --name vst-nats -p 4222:4222 -p 8222:8222 \
        nats:2.11-alpine --jetstream &>/dev/null 2>&1 || docker start vst-nats &>/dev/null
    NATS_DOCKER=1; sleep 2
    echo -e "  ${GREEN}✓ NATS started (Docker)${NC}"
else
    echo -e "${RED}  No NATS available.${NC}"; exit 1
fi
echo

# ── Backend ──
echo -e "${CYAN}[2/3] Starting backend...${NC}"
cd "$ROOT/src/Backend"
dotnet build --nologo -q &>/dev/null

dotnet run --project VideoSubtitleTranslator.Worker --no-build &>/dev/null &
PIDS+=($!)
dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000" &
PIDS+=($!)
echo -e "  ${GREEN}✓ API + Worker started${NC}"
echo

# ── Tauri ──
echo -e "${CYAN}[3/3] Starting Tauri dev mode...${NC}"
echo
echo -e "  ${GREEN}API${NC}       → http://localhost:5000"
echo -e "  ${GREEN}Desktop${NC}   → Tauri window will open"
echo

cd "$ROOT/src/Desktop"
[ ! -d "node_modules" ] && npm install --silent &>/dev/null
npx tauri dev
