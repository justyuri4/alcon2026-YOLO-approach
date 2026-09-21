import os
from pathlib import Path
from ultralytics import YOLO

def main():
    # 実行する train.py が置いてあるディレクトリを取得
    PROJECT_ROOT = Path(__file__).resolve().parent

    # プロジェクトルートからの相対パスで data.yaml を指定
    data_yaml_path = PROJECT_ROOT / "data.yaml"

    # YOLO26 (または YOLOv11/v8) のモデル指定
    model = YOLO("yolo26n-seg.pt")

    results = model.train(
        data=str(data_yaml_path), # パスオブジェクトを渡す
        epochs=50,
        imgsz=640,
        batch=8,
        device="mps",             # Apple Silicon Macの場合 ('cpu', 'cuda', 'mps')
        workers=4,
        project="runs/segment",   # runs/segment フォルダ以下に出力 (相対パス)
        name="finetune_yolo26_seg",
        exist_ok=True
    )

    print("学習が完了しました。")
    print(f"最良モデルの保存先: {os.path.join(results.save_dir, 'weights', 'best.pt')}")

if __name__ == "__main__":
    main()