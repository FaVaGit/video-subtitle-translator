#!/usr/bin/env bash
set -euo pipefail

# ============================================================
# Development mode: API + Worker + Frontend + NATS
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

GREEN='\033[0;32m'; YELLOW='\033[0;33m'; RED='\033[0;31m'; CYAN='\033[0;36m'; NC='\033[0m'

# Cleanup function
PIDS=()
cleanup() {
    echo -e "\n${YELLOW}Shutting down...${NC}"
    for pid in "${PIDS[@]}"; do
        kill "$pid" 2>/dev/null || true
    done
    # Stop NATS Docker container if we started it
    if [ "${NATS_DOCKER:-0}" -eq 1 ]; then
        docker stop vst-nats &>/dev/null || true
    fi
    if [ "${NATS_LOCAL:-0}" -eq 1 ]; then
        killall nats-server 2>/dev/null || true
    fi
    echo -e "${GREEN}All services stopped.${NC}"
    exit 0
}
trap cleanup EXIT INT TERM

echo -e "${CYAN}[DEV] Starting development environment...${NC}"
echo

# ── Step 1: NATS ──
echo -e "${CYAN}[1/4] Checking NATS server...${NC}"
NATS_DOCKER=0
NATS_LOCAL=0

if ss -tlnp 2>/dev/null | grep -q ':4222' || netstat -tlnp 2>/dev/null | grep -q ':4222'; then
    echo -e "  ${GREEN}✓ NATS already running on :4222${NC}"
elif command -v nats-server &>/dev/null; then
    echo "  Starting local NATS with JetStream..."
    mkdir -p "$ROOT/data/nats"
    nats-server --jetstream --store_dir "$ROOT/data/nats" &>/dev/null &
    PIDS+=($!)
    NATS_LOCAL=1
    sleep 2
    echo -e "  ${GREEN}✓ NATS started (local)${NC}"
elif command -v docker &>/dev/null && docker info &>/dev/null; then
    if docker ps --filter "name=vst-nats" --format '{{.Names}}' | grep -q vst-nats; then
        echo -e "  ${GREEN}✓ NATS container already running${NC}"
    else
        echo "  Starting NATS via Docker..."
        docker run -d --name vst-nats -p 4222:4222 -p 8222:8222 \
            nats:2.11-alpine --jetstream &>/dev/null 2>&1 || \
            docker start vst-nats &>/dev/null
        NATS_DOCKER=1
        sleep 2
        echo -e "  ${GREEN}✓ NATS started (Docker)${NC}"
    fi
else
    echo -e "  ${RED}No NATS server or Docker found. Install one.${NC}"
    exit 1
fi
echo

# ── Step 2: Backend ──
echo -e "${CYAN}[2/4] Building backend...${NC}"
cd "$ROOT/src/Backend"
dotnet restore --nologo -q &>/dev/null
dotnet build --nologo -q &>/dev/null
echo -e "  ${GREEN}✓ Backend built${NC}"
echo

# ── Step 3: Frontend ──
echo -e "${CYAN}[3/4] Preparing frontend...${NC}"
cd "$ROOT/src/Frontend"
if [ ! -d "node_modules" ]; then
    echo "  Installing npm packages..."
    npm install --silent &>/dev/null
fi
echo -e "  ${GREEN}✓ Frontend ready${NC}"
echo

# ── Step 4: Launch ──
echo -e "${CYAN}[4/4] Starting services...${NC}"
echo
echo -e "  ${GREEN}API${NC}       → http://localhost:5000"
echo -e "  ${GREEN}Swagger${NC}   → http://localhost:5000/swagger"
echo -e "  ${GREEN}Frontend${NC}  → http://localhost:5173"
echo -e "  ${GREEN}NATS${NC}      → nats://localhost:4222"
echo -e "  ${GREEN}NATS Mon${NC}  → http://localhost:8222"
echo
echo -e "  ${YELLOW}Press Ctrl+C to stop all services${NC}"
echo

# Worker
cd "$ROOT/src/Backend"
dotnet run --project VideoSubtitleTranslator.Worker --no-build &>/dev/null &
PIDS+=($!)
echo -e "  ${GREEN}✓${NC} Worker started (PID: ${PIDS[-1]})"

# API
dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000" &
PIDS+=($!)
echo -e "  ${GREEN}✓${NC} API started (PID: ${PIDS[-1]})"

# Frontend (foreground)
cd "$ROOT/src/Frontend"
echo -e "  ${GREEN}✓${NC} Starting frontend dev server..."
echo
npm run dev
