import csv
import os
from pathlib import Path
import cv2
import numpy as np
from ultralytics import YOLO

# ==========================================
# 設定
# ==========================================
PROJECT_ROOT = Path(__file__).resolve().parent
INPUT_CSV = PROJECT_ROOT / "input.csv"
OUTPUT_CSV = PROJECT_ROOT / "output.csv"

# クラスIDの定義
CLASS_RICE = 0  # 水稲
CLASS_WEED = 1  # 雑草

# 警告閾値 (雑草比率 r % 以上で WARNING)
WEED_RATIO_THRESHOLD = 10.0

# マスク画像用のカラー定義 (BGR順)
COLOR_BACKGROUND = (0, 0, 0)          # 背景: 黒色
COLOR_RICE = (128, 128, 128)          # 水稲 (p): 灰色 (0x80, 0x80, 0x80)
COLOR_WEED = (255, 255, 255)          # 雑草 (w): 白色 (0xFF, 0xFF, 0xFF)


def load_model():
    weights_path = PROJECT_ROOT / "runs/segment/finetune_yolo26_seg/weights/best.pt"
    if not weights_path.exists():
        weights_path = PROJECT_ROOT / "yolo26n-seg.pt"

    return YOLO(str(weights_path))


def process_image(model, img_rel_path, width, height):
    img_path = PROJECT_ROOT / img_rel_path

    if not img_path.exists():
        print(f"⚠️ 画像が見つかりません: {img_path}")
        return None

    # 推論実行
    results = model.predict(source=str(img_path), imgsz=640, verbose=False)[0]

    # 画素数カウント用変数の初期化
    p_pixels = 0  # 水稲画素数
    w_pixels = 0  # 雑草画素数

    # マスク画像作成用のキャンバス（背景は黒色）
    mask_img = np.zeros((height, width, 3), dtype=np.uint8)

    # マスク情報が存在する場合、クラスごとに描画と画素数を集計
    if results.masks is not None:
        classes = results.boxes.cls.cpu().numpy().astype(int)
        masks = results.masks.data.cpu().numpy()  # (N, H, W)

        for cls_id, mask in zip(classes, masks):
            # 元画像サイズにリサイズ
            resized_mask = cv2.resize(
                mask, (width, height), interpolation=cv2.INTER_NEAREST
            )
            mask_bool = resized_mask > 0.5
            pixel_count = int(np.sum(mask_bool))

            if cls_id == CLASS_RICE:
                p_pixels += pixel_count
                mask_img[mask_bool] = COLOR_RICE
            elif cls_id == CLASS_WEED:
                w_pixels += pixel_count
                mask_img[mask_bool] = COLOR_WEED

    # 雑草比率 r (%) の計算
    total_crop_pixels = p_pixels + w_pixels
    if total_crop_pixels > 0:
        r_ratio = (w_pixels / total_crop_pixels) * 100.0
    else:
        r_ratio = 0.0

    # 判定結果 (OK / WARNING)
    status = "WARNING" if r_ratio >= WEED_RATIO_THRESHOLD else "OK"

    # 描画画像の保存パス組み立て
    out_img_path = img_path.parent / f"{img_path.stem}-output{img_path.suffix}"
    out_img_rel_path = out_img_path.relative_to(PROJECT_ROOT)

    # マスク画像の保存
    cv2.imwrite(str(out_img_path), mask_img)

    return {
        "width": width,
        "height": height,
        "out_path": str(out_img_rel_path).replace("\\", "/"),
        "status": status,
        "p_pixels": p_pixels,
        "w_pixels": w_pixels,
        "r_ratio": round(r_ratio, 1),
    }


def main():
    if not INPUT_CSV.exists():
        print(f"❌ 入力ファイルが見つかりません: {INPUT_CSV}")
        return

    model = load_model()

    # input.csv の読み込み
    with open(INPUT_CSV, "r", encoding="utf-8") as f:
        lines = [line.strip() for line in f if line.strip()]

    if not lines:
        print("❌ input.csv が空です。")
        return

    raw_rows = lines[1:]
    processed_results = []

    for row in raw_rows:
        parts = [p.strip() for p in row.split(",")]
        if len(parts) < 3:
            continue

        width = int(parts[0])
        height = int(parts[1])
        img_rel_path = parts[2]

        res = process_image(model, img_rel_path, width, height)
        if res:
            processed_results.append(res)

    # output.csv の書き出し
    with open(OUTPUT_CSV, "w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([len(processed_results)])

        for res in processed_results:
            writer.writerow(
                [
                    res["width"],
                    res["height"],
                    res["out_path"],
                    res["status"],
                    res["p_pixels"],
                    res["w_pixels"],
                    f"{res['r_ratio']:.1f}",
                ]
            )

    print(
        f"✅ 処理完了: {len(processed_results)} 件の書き出し完了 ➔ '{OUTPUT_CSV.name}'"
    )


if __name__ == "__main__":
    main()