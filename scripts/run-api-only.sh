#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT/src/Backend"

GREEN='\033[0;32m'; CYAN='\033[0;36m'; NC='\033[0m'

echo -e "${CYAN}[API] Starting backend API only...${NC}"
echo

dotnet build --nologo -q &>/dev/null

echo -e "  ${GREEN}API${NC}       → http://localhost:5000"
echo -e "  ${GREEN}Swagger${NC}   → http://localhost:5000/swagger"
echo

dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"
