import os
from pathlib import Path

os.environ.setdefault("MIOPEN_FIND_MODE", "FAST")
os.environ.setdefault("KMP_DUPLICATE_LIB_OK", "TRUE")

# ROCmの共有ライブラリをtorchのimport前に設定
project_root = Path(__file__).resolve().parent
venv_root = Path(os.environ.get("VIRTUAL_ENV", ""))

rocm_library_paths = [
    venv_root / "lib/python3.10/site-packages/_rocm_sdk_core/lib",
    Path("/usr/lib/wsl/lib"),
]

library_paths = [
    str(path)
    for path in rocm_library_paths
    if path.exists()
]

if library_paths:
    current_library_path = os.environ.get("LD_LIBRARY_PATH", "")
    os.environ["LD_LIBRARY_PATH"] = os.pathsep.join(
        library_paths + ([current_library_path] if current_library_path else [])
    )

import torch
from ultralytics import YOLO


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
    print("ROCm GPUを使用して学習します")


def main() -> None:
    check_rocm_gpu()

    data_yaml_path = project_root / "data.yaml"
    model_path = project_root / "yolo26n-seg.pt"

    if not data_yaml_path.exists():
        raise FileNotFoundError(f"データセット設定が見つかりません: {data_yaml_path}")

    model = YOLO(str(model_path))

    results = model.train(
        data=str(data_yaml_path),
        epochs=300,
        imgsz=640,
        batch=30,
        device=0,
        workers=6,
        cache="ram",
        amp=True,
        project=str(project_root / "runs/segment"),
        name="finetune_yolo26_rocm",
        exist_ok=True,
    )

    best_model_path = Path(results.save_dir) / "weights" / "best.pt"

    print("学習が完了しました。")
    print(f"最良モデルの保存先: {best_model_path}")


if __name__ == "__main__":
    main()