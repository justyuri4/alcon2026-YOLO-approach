import os
from pathlib import Path

#環境有効コマンド cd /mnt/d/Users/ideka/Downloads/alcon2026-YOLO-approach source convert_env/bin/activate


# CUDA / PyTorch の高速化設定
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")
os.environ.setdefault("CUDA_DEVICE_ORDER", "PCI_BUS_ID")

import torch
from ultralytics import YOLO


PROJECT_ROOT = Path(__file__).resolve().parent

DATA_YAML_PATH = Path(
    os.environ.get(
        "YOLO_DATA",
        str(PROJECT_ROOT / "data.yaml"),
    )
)

MODEL_PATH = os.environ.get(
    "YOLO_MODEL",
    str(PROJECT_ROOT / "yolo26n-seg.pt"),
)

RUNS_DIR = Path(
    os.environ.get(
        "YOLO_PROJECT",
        str(PROJECT_ROOT / "runs/segment"),
    )
)

RUN_NAME = os.environ.get(
    "YOLO_RUN_NAME",
    "finetune_yolo26_cloud",
)

DEVICE = os.environ.get("YOLO_DEVICE", "0")
IMAGE_SIZE = int(os.environ.get("YOLO_IMAGE_SIZE", "640"))
EPOCHS = int(os.environ.get("YOLO_EPOCHS", "100"))
MAX_DET = int(os.environ.get("YOLO_MAX_DET", "600"))

# -1の場合、UltralyticsがGPUメモリからバッチサイズを自動決定します。
BATCH_SIZE = int(os.environ.get("YOLO_BATCH", "-1"))

# Google CloudではCPUコア数を使い切らず、データローダーの過負荷を防止します。
DEFAULT_WORKERS = min(os.cpu_count() or 4, 16)
WORKERS = int(os.environ.get("YOLO_WORKERS", str(DEFAULT_WORKERS)))

# "disk"はGPU VMのローカルSSD / Persistent Diskを使用します。
# RAMに余裕がある場合は YOLO_CACHE=ram に変更できます。
CACHE_MODE = os.environ.get("YOLO_CACHE", "disk")

AMP_ENABLED = os.environ.get("YOLO_AMP", "true").lower() in {
    "1",
    "true",
    "yes",
    "on",
}


def get_device_index() -> int:
    """Ultralyticsに渡すデバイス指定からGPU番号を取得します。"""
    device_name = DEVICE.lower().replace("cuda:", "")

    if not device_name.isdigit():
        raise ValueError(
            f"YOLO_DEVICEにはGPU番号を指定してください: {DEVICE}"
        )

    return int(device_name)


def check_cuda_gpu() -> int:
    """CUDA GPUの認識状態と性能情報を確認します。"""
    print(f"PyTorch: {torch.__version__}")
    print(f"CUDA: {torch.version.cuda}")

    if not torch.cuda.is_available():
        raise RuntimeError(
            "CUDA GPUが認識されません。"
            " Google CloudのGPUドライバーとCUDA対応PyTorchを確認してください。"
        )

    device_index = get_device_index()

    if device_index >= torch.cuda.device_count():
        raise RuntimeError(
            f"指定されたGPUが存在しません: {DEVICE} "
            f"(利用可能なGPU数: {torch.cuda.device_count()})"
        )

    torch.cuda.set_device(device_index)

    device_properties = torch.cuda.get_device_properties(device_index)
    total_memory_gb = device_properties.total_memory / (1024**3)

    print(f"GPU: {device_properties.name}")
    print(f"VRAM: {total_memory_gb:.1f} GB")
    print(f"CUDA device: {device_index}")
    print(f"CUDA capability: {device_properties.major}.{device_properties.minor}")

    # Ampere以降のGPUでTF32を有効化します。
    if device_properties.major >= 8:
        torch.backends.cuda.matmul.allow_tf32 = True
        torch.backends.cudnn.allow_tf32 = True
        print("TF32: enabled")

    torch.backends.cudnn.benchmark = True
    torch.set_float32_matmul_precision("high")

    print(f"AMP: {'enabled' if AMP_ENABLED else 'disabled'}")
    print(f"Batch size: {'auto' if BATCH_SIZE == -1 else BATCH_SIZE}")
    print(f"Workers: {WORKERS}")
    print(f"Cache: {CACHE_MODE}")

    return device_index


def validate_paths() -> None:
    """学習に必要なファイルを確認します。"""
    if not DATA_YAML_PATH.exists():
        raise FileNotFoundError(
            f"データセット設定が見つかりません: {DATA_YAML_PATH}"
        )

    model_path = Path(MODEL_PATH)

    # モデル名を指定した場合はUltralyticsによる自動ダウンロードを許可します。
    if model_path.suffix == ".pt" and not model_path.exists():
        raise FileNotFoundError(
            f"学習済みモデルが見つかりません: {MODEL_PATH}"
        )


def main() -> None:
    check_cuda_gpu()
    validate_paths()

    print(f"Dataset: {DATA_YAML_PATH}")
    print(f"Model: {MODEL_PATH}")
    print(f"Output: {RUNS_DIR / RUN_NAME}")

    model = YOLO(MODEL_PATH)

    # Objects365のクラス名はTitle Caseのため、小文字化して変換します。
    model.model.names = {
        class_id: ALIASES.get(class_name.lower(), class_name)
        for class_id, class_name in model.model.names.items()
    }

    results = model.train(
        data=str(DATA_YAML_PATH),
        epochs=EPOCHS,
        imgsz=IMAGE_SIZE,
        batch=BATCH_SIZE,
        device=DEVICE,
        workers=WORKERS,
        cache=CACHE_MODE,
        amp=AMP_ENABLED,
        max_det=MAX_DET,
        project=str(RUNS_DIR),
        name=RUN_NAME,
        exist_ok=True,

        optimizer="auto",
        cos_lr=True,
        close_mosaic=10,
        deterministic=False,
        patience=50,
        plots=True,
    )

    best_model_path = Path(results.save_dir) / "weights" / "best.pt"

    print("学習が完了しました。")
    print(f"最良モデルの保存先: {best_model_path}")


# Objects365のクラス名（小文字）からCOCOクラス名への変換
ALIASES = {
    "wild bird": "bird",
    "handbag/satchel": "handbag",
    "luggage": "suitcase",
    "bowl/basin": "bowl",
    "orange/tangerine": "orange",
    "monitor/tv": "tv",
    "stuffed toy": "teddy bear",
    "hair dryer": "hair drier",
}


if __name__ == "__main__":
    main()