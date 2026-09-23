import os
from pathlib import Path

os.environ.setdefault("KMP_DUPLICATE_LIB_OK", "TRUE")

from ultralytics import YOLO


def main():
    project_root = Path(__file__).resolve().parent
    data_yaml_path = project_root / "data.yaml"

    model = YOLO("yolo26n-seg.pt")

    results = model.train(
        data=str(data_yaml_path),
        epochs=100,
        imgsz=800,
        batch=10,
        device=0,
        workers=24,
        cache="ram",
        amp=True,
        max_det=500,
        degrees=15.0,
        fliplr=0.5,
        mosaic=1.0,       # Mosaicを常時有効化
        close_mosaic=15,  # 最後の10エポックは無効化
        cos_lr=True,
        lr0=0.01,
        lrf=0.01,
        warmup_epochs=5.0,
        cls=2.5,
        box=10.0,
        project=str(project_root / "runs/segment"),
        name="finetune_yolo26_rocm",
        exist_ok=True,
    )

    print("学習が完了しました。")
    print(f"最良モデルの保存先: {os.path.join(results.save_dir, 'weights', 'best.pt')}")


if __name__ == "__main__":
    main()