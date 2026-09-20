from ultralytics import SAM

# 1. 学習済み重みのロード
model = SAM("runs/sam/finetune_sam/weights/best.pt")

# 2. 推論の実行
results = model("test_image.jpg", conf=0.25)

# 3. 描画結果の保存
for i, r in enumerate(results):
    output_path = f"output_{i}.jpg"
    r.save(filename=output_path)
    print(f"推論結果を保存しました: {output_path}")