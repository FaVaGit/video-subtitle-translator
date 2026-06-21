#!/usr/bin/env bash
set -euo pipefail

# ============================================================
# Docker mode: All services via docker-compose
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

GREEN='\033[0;32m'; YELLOW='\033[0;33m'; RED='\033[0;31m'; CYAN='\033[0;36m'; NC='\033[0m'

echo -e "${CYAN}[DOCKER] Starting via Docker Compose...${NC}"
echo

# Verify Docker
if ! docker info &>/dev/null; then
    echo -e "${RED}Docker is not running. Please start Docker.${NC}"
    exit 1
fi

# Detect compose command
COMPOSE="docker compose"
if ! docker compose version &>/dev/null; then
    if command -v docker-compose &>/dev/null; then
        COMPOSE="docker-compose"
    else
        echo -e "${RED}Docker Compose not found.${NC}"
        exit 1
    fi
fi

echo "  Using: $COMPOSE"
echo

# Build and start
echo -e "${CYAN}Building images...${NC}"
cd "$ROOT/docker"
$COMPOSE build

echo
echo -e "${CYAN}Starting services...${NC}"
echo
echo -e "  ${GREEN}Frontend${NC}  → http://localhost:3000"
echo -e "  ${GREEN}API${NC}       → http://localhost:5000"
echo -e "  ${GREEN}NATS${NC}      → nats://localhost:4222"
echo -e "  ${GREEN}NATS Mon${NC}  → http://localhost:8222"
echo
echo -e "  ${YELLOW}Press Ctrl+C to stop${NC}"
echo

$COMPOSE up
