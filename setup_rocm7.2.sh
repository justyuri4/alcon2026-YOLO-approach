#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_DIR="$SCRIPT_DIR/convert_env"
REQUIREMENTS="$SCRIPT_DIR/requirements-rocm7.2.txt"
ROCM_HSA="/opt/rocm-7.2.0/lib/libhsa-runtime64.so.1"

echo "===== ROCm 7.2 environment setup ====="
echo "Project: $SCRIPT_DIR"
echo "Virtual environment: $ENV_DIR"

if [ ! -f "$REQUIREMENTS" ]; then
    echo "ERROR: $REQUIREMENTS がありません"
    exit 1
fi

if [ ! -f "$ROCM_HSA" ]; then
    echo "ERROR: $ROCM_HSA がありません"
    echo "ROCm 7.2 がインストールされているか確認してください"
    exit 1
fi

if [ -n "${VIRTUAL_ENV:-}" ]; then
    echo "ERROR: 仮想環境が有効です。先に deactivate を実行してください"
    exit 1
fi

if [ -d "$ENV_DIR" ]; then
    echo "ERROR: $ENV_DIR は既に存在します"
    echo "削除してから再実行してください:"
    echo "  rm -rf '$ENV_DIR'"
    exit 1
fi

echo
echo "===== Creating virtual environment ====="
python3 -m venv "$ENV_DIR"
source "$ENV_DIR/bin/activate"

echo
echo "===== Installing Python packages ====="
python -m pip install --upgrade pip
python -m pip install \
    --extra-index-url https://download.pytorch.org/whl/rocm7.2 \
    -r "$REQUIREMENTS"

echo
echo "===== Configuring ROCm runtime ====="
cat >> "$ENV_DIR/bin/activate" <<'EOF'

# AMD ROCm 7.2 on WSL2
if [ -f /opt/rocm-7.2.0/lib/libhsa-runtime64.so.1 ]; then
    export LD_PRELOAD=/opt/rocm-7.2.0/lib/libhsa-runtime64.so.1
fi
EOF

source "$ENV_DIR/bin/activate"

echo
echo "===== Verifying PyTorch and GPU ====="
python - <<'PY'
import sys
import torch

print("Python:", sys.executable)
print("PyTorch:", torch.__version__)
print("HIP:", torch.version.hip)
print("Available:", torch.cuda.is_available())
print("Device count:", torch.cuda.device_count())

if not torch.cuda.is_available():
    raise SystemExit(
        "ERROR: ROCm GPU が利用できません。LD_PRELOAD と ROCm の状態を確認してください"
    )

print("GPU:", torch.cuda.get_device_name(0))
PY

echo
echo "===== Setup completed ====="
echo "Activate with:"
echo "  source '$ENV_DIR/bin/activate'"
