from ultralytics import YOLO

# 1. 学習済みのカスタムモデルを読み込み
# (学習前は "yolo11n-seg.pt" でしたが、学習後は作成されたカスタムモデルを指定します)
model = YOLO("runs/segment/train/weights/best.pt")

# 2. 推論を実行（画像パス、信頼度のしきい値、保存フラグを指定）
results = model.predict(
    source="test_image.jpg",  # テストしたい画像のパス
    conf=0.25,                # 検出のしきい値（0.25以上で検出）
    save=True                 # 予測結果を画像として保存する
)

print("推論が完了しました。結果は runs/segment/predict/ に保存されています。")