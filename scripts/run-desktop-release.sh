#!/usr/bin/env bash
set -euo pipefail

# ============================================================
# Desktop release mode: build and run packaged desktop app
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
}
trap cleanup EXIT INT TERM

echo -e "${CYAN}[DESKTOP-RELEASE] Building and launching packaged app...${NC}"
echo

command -v cargo &>/dev/null || { echo -e "${RED}Rust not found. Install from https://rustup.rs${NC}"; exit 1; }
command -v node &>/dev/null || { echo -e "${RED}Node.js not found. Install Node.js 22+${NC}"; exit 1; }
command -v dotnet &>/dev/null || { echo -e "${RED}.NET SDK not found.${NC}"; exit 1; }
cargo tauri --version &>/dev/null || { echo -e "${RED}cargo-tauri not found. Install with: cargo install tauri-cli${NC}"; exit 1; }

# ── NATS ──
echo -e "${CYAN}[1/4] Checking NATS...${NC}"
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
echo -e "${CYAN}[2/4] Starting backend...${NC}"
cd "$ROOT/src/Backend"
dotnet build --nologo -q &>/dev/null

dotnet run --project VideoSubtitleTranslator.Worker --no-build &>/dev/null &
PIDS+=($!)
dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000" &>/dev/null &
PIDS+=($!)
echo -e "  ${GREEN}✓ API + Worker started${NC}"
echo

# ── Build desktop ──
echo -e "${CYAN}[3/4] Building desktop app...${NC}"
cd "$ROOT/src/Frontend"
[ ! -d "node_modules" ] && npm install --silent &>/dev/null
cd "$ROOT/src/Desktop/src-tauri"
cargo tauri build
echo -e "  ${GREEN}✓ Desktop build completed${NC}"
echo

# ── Launch packaged binary ──
echo -e "${CYAN}[4/4] Launching packaged desktop app...${NC}"
APP_BIN="$ROOT/src/Desktop/src-tauri/target/release/video-subtitle-translator"
if [ ! -f "$APP_BIN" ]; then
    echo -e "${RED}Packaged binary not found: $APP_BIN${NC}"
    exit 1
fi

echo -e "  ${GREEN}API${NC}       → http://localhost:5000"
echo -e "  ${GREEN}Desktop${NC}   → $APP_BIN"
echo
"$APP_BIN"
