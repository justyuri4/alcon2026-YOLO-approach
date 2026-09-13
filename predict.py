import csv
import os
import cv2
import numpy as np
import torch
import torch.nn.functional as F
from ultralytics import YOLO

# ==========================================
# 設定
# ==========================================
MODEL_PATH = "yolo11n-seg.pt"
INPUT_CSV = "input.csv"
OUTPUT_CSV = "output.csv"

# クラスIDの設定（data.yamlの定義に合わせる）
YOUNG_RICE_CLASS_ID = 0  # 若い水稲
OLD_RICE_CLASS_ID   = 1  # 古い水稲
WEED_CLASS_ID       = 2  # 雑草

# カラー定義 (BGR形式)
COLOR_RICE = [0x80, 0x80, 0x80]  # 水稲 : 灰色 (128, 128, 128)
COLOR_WEED = [0xFF, 0xFF, 0xFF]  # 雑草 : 白色 (255, 255, 255)
COLOR_BG   = [0x00, 0x00, 0x00]  # 背景 : 黒色 (0, 0, 0)

# 判定基準閾値 (%)
WEED_RATIO_THRESHOLD = 10.0

# モデルの読み込み
model = YOLO(MODEL_PATH)


def process_images():
    if not os.path.exists(INPUT_CSV):
        print(f"エラー: {INPUT_CSV} が見つかりません。")
        return

    with open(INPUT_CSV, "r", encoding="utf-8") as f:
        lines = [line.strip() for line in f if line.strip()]

    if not lines:
        print("エラー: input.csv が空です。")
        return

    # 1行目は画像数 N、2行目以降がデータ
    num_images_to_process = int(lines[0])
    image_entries = lines[1:num_images_to_process + 1]

    output_rows = []

    for entry in image_entries:
        parts = entry.split(",")
        width, height, img_path = int(parts[0]), int(parts[1]), parts[2]

        if not os.path.exists(img_path):
            print(f"警告: 画像ファイル {img_path} が存在しません。スキップします。")
            continue

        # 推論実行
        results = model.predict(source=img_path, conf=0.25, save=False)[0]

        # 出力画像（背景：黒）の初期化
        mask_image = np.zeros((height, width, 3), dtype=np.uint8)

        rice_pixels = 0
        weed_pixels = 0

        if results.masks is not None:
            # テンソルデータ取得
            masks_tensor = results.masks.data  # Shape: (N, H_mask, W_mask)
            classes_tensor = results.boxes.cls  # Shape: (N,)

            # 元画像サイズにリサイズ (バイリニア補間)
            masks_resized = F.interpolate(
                masks_tensor.unsqueeze(1),
                size=(height, width),
                mode="bilinear",
                align_corners=False
            ).squeeze(1) > 0.5  # bool テンソルに変換

            # クラスごとのマスクを初期化 (全画素False)
            rice_mask_union = torch.zeros((height, width), dtype=torch.bool, device=masks_tensor.device)
            weed_mask_union = torch.zeros((height, width), dtype=torch.bool, device=masks_tensor.device)

            # 各インスタンスのマスク論理和 (OR結合) を取る
            for mask, cls_id in zip(masks_resized, classes_tensor):
                cls_id = int(cls_id.item())
                # 若い稲 (0) と 古い稲 (1) の両方を rice_mask_union にまとめる
                if cls_id == YOUNG_RICE_CLASS_ID or cls_id == OLD_RICE_CLASS_ID:
                    rice_mask_union = rice_mask_union | mask
                elif cls_id == WEED_CLASS_ID:
                    weed_mask_union = weed_mask_union | mask

            # 重複排除後の画素数カウント
            rice_pixels = int(rice_mask_union.sum().item())
            weed_pixels = int(weed_mask_union.sum().item())

            # NumPy配列に変換してカラーマップ画像に描画
            rice_mask_np = rice_mask_union.cpu().numpy()
            weed_mask_np = weed_mask_union.cpu().numpy()

            # 水稲（統合後）・雑草の順で塗りつぶし
            mask_image[rice_mask_np] = COLOR_RICE
            mask_image[weed_mask_np] = COLOR_WEED

        # 雑草比率 r [%] の計算 (四捨五入して小数点以下1桁に整形)
        total_pixels = rice_pixels + weed_pixels
        if total_pixels > 0:
            weed_ratio_raw = (weed_pixels / total_pixels) * 100
        else:
            weed_ratio_raw = 0.0

        # 小数点以下2桁目を四捨五入 (例: 16.71 -> 16.7, 4.80 -> 4.8)
        weed_ratio = round(weed_ratio_raw, 1)

        # 判定結果
        status = "WARNING" if weed_ratio >= WEED_RATIO_THRESHOLD else "OK"

        # 出力画像パスの整形 (拡張子直前に -output を挿入)
        base, ext = os.path.splitext(img_path)
        out_img_path = f"{base}-output{ext}"

        # 出力用ディレクトリ作成
        out_dir = os.path.dirname(out_img_path)
        if out_dir and not os.path.exists(out_dir):
            os.makedirs(out_dir, exist_ok=True)

        # 画像書き出し
        cv2.imwrite(out_img_path, mask_image)

        # 出力行追加
        output_rows.append(
            f"{width},{height},{out_img_path},{status},{rice_pixels},{weed_pixels},{weed_ratio:.1f}"
        )

    # output.csv の書き出し
    with open(OUTPUT_CSV, "w", encoding="utf-8") as f:
        f.write(f"{len(output_rows)}\n")
        for row in output_rows:
            f.write(f"{row}\n")

    print(f"処理完了: {len(output_rows)} 件を出力しました。")


if __name__ == "__main__":
    process_images()