#!/usr/bin/env bash
set -euo pipefail

# ============================================================
# Smart launcher - auto-detects environment and available tools
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

echo -e "${CYAN}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║      Video Subtitle Translator - Launcher       ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════╝${NC}"
echo

# ── Detect OS ──
OS="unknown"
case "$(uname -s)" in
    Linux*)   OS="linux";;
    Darwin*)  OS="macos";;
    MINGW*|MSYS*|CYGWIN*) OS="windows";;
esac
echo -e "${CYAN}OS:${NC} $OS"

# ── Detect available tools ──
HAS_DOTNET=0; HAS_NODE=0; HAS_DOCKER=0; HAS_RUST=0; HAS_NATS=0
DOCKER_RUNNING=0

command -v dotnet &>/dev/null && HAS_DOTNET=1
command -v node &>/dev/null && HAS_NODE=1
command -v docker &>/dev/null && HAS_DOCKER=1
command -v cargo &>/dev/null && HAS_RUST=1
command -v nats-server &>/dev/null && HAS_NATS=1

if [ "$HAS_DOCKER" -eq 1 ]; then
    docker info &>/dev/null && DOCKER_RUNNING=1
fi

echo -e "${CYAN}Environment Detection:${NC}"
[ "$HAS_DOTNET" -eq 1 ] && echo -e "  ${GREEN}✓${NC} .NET SDK" || echo -e "  ${RED}✗${NC} .NET SDK"
[ "$HAS_NODE" -eq 1 ]   && echo -e "  ${GREEN}✓${NC} Node.js"  || echo -e "  ${RED}✗${NC} Node.js"
if [ "$HAS_DOCKER" -eq 1 ]; then
    [ "$DOCKER_RUNNING" -eq 1 ] && echo -e "  ${GREEN}✓${NC} Docker (running)" || echo -e "  ${YELLOW}~${NC} Docker (not running)"
else
    echo -e "  ${RED}✗${NC} Docker"
fi
[ "$HAS_RUST" -eq 1 ] && echo -e "  ${GREEN}✓${NC} Rust/Cargo" || echo -e "  ${YELLOW}~${NC} Rust (desktop only)"
[ "$HAS_NATS" -eq 1 ] && echo -e "  ${GREEN}✓${NC} NATS server" || echo -e "  ${YELLOW}~${NC} NATS (will use Docker)"
echo

# ── If argument passed, use that mode ──
if [ -n "${1:-}" ]; then
    MODE="$1"
else
    # ── Show menu ──
    OPTIONS=()
    LABELS=()

    if [ "$HAS_DOTNET" -eq 1 ] && [ "$HAS_NODE" -eq 1 ]; then
        OPTIONS+=("dev")
        LABELS+=("Development    (API + Worker + Frontend dev server + NATS)")
    fi
    if [ "$DOCKER_RUNNING" -eq 1 ]; then
        OPTIONS+=("docker")
        LABELS+=("Docker         (All services via docker-compose)")
    fi
    if [ "$HAS_DOTNET" -eq 1 ] && [ "$HAS_NODE" -eq 1 ] && [ "$HAS_RUST" -eq 1 ]; then
        OPTIONS+=("desktop")
        LABELS+=("Desktop        (Tauri desktop app + backend)")
        OPTIONS+=("desktop-release")
        LABELS+=("Desktop Release (Build and run packaged desktop app)")
    fi
    if [ "$HAS_DOTNET" -eq 1 ]; then
        OPTIONS+=("api-only")
        LABELS+=("API Only       (Backend API only)")
    fi
    if [ "$HAS_NODE" -eq 1 ]; then
        OPTIONS+=("frontend-only")
        LABELS+=("Frontend Only  (React dev server only)")
    fi

    if [ ${#OPTIONS[@]} -eq 0 ]; then
        echo -e "${RED}No supported tools found. Install .NET SDK, Node.js, or Docker.${NC}"
        exit 1
    fi

    echo -e "${CYAN}Select run mode:${NC}"
    echo
    for i in "${!OPTIONS[@]}"; do
        echo -e "  ${GREEN}$((i+1))${NC}  ${LABELS[$i]}"
    done
    echo
    read -rp "Select [1-${#OPTIONS[@]}]: " CHOICE

    idx=$((CHOICE - 1))
    if [ "$idx" -lt 0 ] || [ "$idx" -ge "${#OPTIONS[@]}" ]; then
        echo -e "${RED}Invalid selection.${NC}"
        exit 1
    fi
    MODE="${OPTIONS[$idx]}"
fi

echo
echo -e "${CYAN}Starting in ${BOLD}${MODE}${NC}${CYAN} mode...${NC}"
echo

case "$MODE" in
    dev)            exec "$SCRIPT_DIR/run-dev.sh";;
    docker)         exec "$SCRIPT_DIR/run-docker.sh";;
    desktop)        exec "$SCRIPT_DIR/run-desktop.sh";;
    desktop-release) exec "$SCRIPT_DIR/run-desktop-release.sh";;
    api-only)       exec "$SCRIPT_DIR/run-api-only.sh";;
    frontend-only)  exec "$SCRIPT_DIR/run-frontend-only.sh";;
    *)              echo -e "${RED}Unknown mode: $MODE${NC}"; exit 1;;
esac
