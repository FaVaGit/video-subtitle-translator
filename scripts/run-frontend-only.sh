#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT/src/Frontend"

GREEN='\033[0;32m'; CYAN='\033[0;36m'; NC='\033[0m'

echo -e "${CYAN}[FRONTEND] Starting React dev server only...${NC}"
echo

[ ! -d "node_modules" ] && npm install --silent &>/dev/null

echo -e "  ${GREEN}Frontend${NC}  → http://localhost:5173"
echo -e "  ${GREEN}API proxy${NC} → http://localhost:5000 (configure in vite.config.ts)"
echo

npm run dev
