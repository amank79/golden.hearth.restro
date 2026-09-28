#!/usr/bin/env bash
# Setup script for Claude Code on the web (Linux cloud machine).
# Installs the .NET 10 SDK if missing, then restores .NET tools/packages and frontend packages.
set -euo pipefail

if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -q '^10\.'; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
  echo 'export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"' >> "$HOME/.bashrc"
  export PATH="$HOME/.dotnet:$PATH"
fi

cd "$(dirname "$0")/.."
dotnet tool restore
dotnet restore
npm ci --prefix src/web
