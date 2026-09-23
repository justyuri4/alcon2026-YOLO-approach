import os
from pathlib import Path

os.environ.setdefault("MIOPEN_FIND_MODE", "FAST")
os.environ.setdefault("KMP_DUPLICATE_LIB_OK", "TRUE")

import torch
from ultralytics import YOLO


project_root = Path(__file__).resolve().parent


def check_rocm_gpu() -> None:
    print(f"PyTorch: {torch.__version__}")
    print(f"HIP: {torch.version.hip}")

    if not torch.cuda.is_available():
        raise RuntimeError(
            "ROCm GPUが認識されません。"
            " torch.cuda.is_available() がFalseです。"
        )

    gpu_name = torch.cuda.get_device_name(0)
    print(f"GPU: {gpu_name}")
    print("ROCm GPUを使用します")


def main() -> None:
    check_rocm_gpu()

    data_yaml_path = project_root / "data.yaml"
    model_path = project_root / "yolo26n-seg.pt"

    if not data_yaml_path.exists():
        raise FileNotFoundError(
            f"データセット設定が見つかりません: {data_yaml_path}"
        )

    if not model_path.exists():
        raise FileNotFoundError(
            f"モデルファイルが見つかりません: {model_path}"
        )

    model = YOLO(str(model_path))

    results = model.train(
        data=str(data_yaml_path),
        epochs=50,
        imgsz=1024,
        batch=8,
        device=0,
        workers=8,
        cache="ram",
        amp=True,
        max_det=600,
        degrees=15.0,
        fliplr=0.5,
        project=str(project_root / "runs/segment"),
        name="finetune_yolo26_rocm",
        exist_ok=True,
    )

    best_model_path = Path(results.save_dir) / "weights" / "best.pt"

    print("学習が完了しました。")
    print(f"最良モデルの保存先: {best_model_path}")


if __name__ == "__main__":
    main()