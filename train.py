import os
from ultralytics import SAM

def main():
    # 1. 事前学習済み SAM モデルのロード
    # ※ sam_b.pt, sam_l.pt, sam2_b.pt などが使用可能です
    model = SAM("sam_b.pt")

    # 2. ファインチューニングの実行
    results = model.train(
        data="data.yaml",      # データセット設定ファイルのパス
        epochs=50,             # エポック数
        imgsz=640,             # 画像サイズ
        batch=8,               # バッチサイズ (GPUメモリに応じて調整)
        device=0,              # GPU番号 (CPUの場合は 'cpu')
        workers=4,             # データ読み込みのワーカー数
        project="runs/sam",    # 保存先フォルダ名
        name="finetune_sam",   # 実験名
        exist_ok=True          # フォルダが存在する場合に上書き許可
    )

    print("学習が完了しました。")
    print(f"最良モデルの保存先: {os.path.join(results.save_dir, 'weights', 'best.pt')}")

if __name__ == "__main__":
    main()