#!/usr/bin/env bash
# Installs the LootFall toolchain (Godot 4.7.2 .NET + .NET 8 SDK) in a fresh Ubuntu 24.04 container.
# Safe to re-run: skips anything already installed.
#   tools/setup-cloud.sh                      # editor/runtime only
#   tools/setup-cloud.sh --export-templates   # also the Windows export templates (~1 GB download)
set -euo pipefail

GODOT_VERSION="4.7.2"
GODOT_DIR="/opt/godot"
GODOT_NAME="Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64"
GODOT_BIN="${GODOT_DIR}/${GODOT_NAME}/Godot_v${GODOT_VERSION}-stable_mono_linux.x86_64"
TEMPLATES_DIR="${HOME}/.local/share/godot/export_templates/${GODOT_VERSION}.stable.mono"
BASE_URL="https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-stable"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Installing .NET 8 SDK..."
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-8.0
fi

if [ ! -x "${GODOT_BIN}" ]; then
  echo "Installing Godot ${GODOT_VERSION} (.NET)..."
  mkdir -p "${GODOT_DIR}"
  curl -sSL -o /tmp/godot.zip "${BASE_URL}/${GODOT_NAME}.zip"
  python3 -c "import zipfile; zipfile.ZipFile('/tmp/godot.zip').extractall('${GODOT_DIR}')"
  rm /tmp/godot.zip
  chmod +x "${GODOT_BIN}"
fi
ln -sf "${GODOT_BIN}" /usr/local/bin/godot

if [ "${1:-}" = "--export-templates" ] && [ ! -f "${TEMPLATES_DIR}/windows_release_x86_64.exe" ]; then
  echo "Installing Windows export templates..."
  mkdir -p "${TEMPLATES_DIR}"
  curl -sSL -o /tmp/templates.tpz "${BASE_URL}/Godot_v${GODOT_VERSION}-stable_mono_export_templates.tpz"
  python3 - "${TEMPLATES_DIR}" <<'EOF'
import os, sys, zipfile
dest = sys.argv[1]
with zipfile.ZipFile('/tmp/templates.tpz') as z:
    for name in z.namelist():
        base = os.path.basename(name)
        if base == 'version.txt' or base.startswith('windows_') and 'x86_64' in base:
            with z.open(name) as src, open(os.path.join(dest, base), 'wb') as dst:
                dst.write(src.read())
EOF
  rm /tmp/templates.tpz
fi

godot --version
