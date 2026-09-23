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
        epochs=300,
        imgsz=1024,
        batch=2,
        device="cpu",
        workers=0,
        amp=False,
        project="runs/segment",
        name="finetune_yolo26_seg",
        exist_ok=True,
    )

    print("学習が完了しました。")
    print(f"最良モデルの保存先: {os.path.join(results.save_dir, 'weights', 'best.pt')}")


if __name__ == "__main__":
    main()