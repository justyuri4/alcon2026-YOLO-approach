import os
import cv2
import numpy as np
from pathlib import Path
import random

# ==========================================
# 設定 1: 色分け画像(RGB)とクラスIDの対応表
# ==========================================
COLOR_TO_CLASS = {
    # (R, G, B): クラスID
    (255, 0, 0): 0,    # 例: 赤色 -> クラス 0
    (0, 255, 0): 1,    # 例: 緑色 -> クラス 1
    (0, 0, 255): 2,    # 例: 青色 -> クラス 2
}

# ==========================================
# 設定 2: 相対パスと分割率の設定
# ==========================================
TARGET_DIR = Path("solo/sequence.0")
OUTPUT_DIR = Path("dataset")
VAL_RATIO = 0.2  # 20% を検証データ(val)に割り当てる

def convert_sequence_masks():
    # 1. YOLO標準のディレクトリ構造を作成 (train / val)
    train_img_dir = OUTPUT_DIR / "images/train"
    val_img_dir = OUTPUT_DIR / "images/val"
    train_lbl_dir = OUTPUT_DIR / "labels/train"
    val_lbl_dir = OUTPUT_DIR / "labels/val"

    for d in [train_img_dir, val_img_dir, train_lbl_dir, val_lbl_dir]:
        d.mkdir(parents=True, exist_ok=True)

    if not TARGET_DIR.exists():
        print(f"❌ エラー: 指定されたパスが存在しません: {TARGET_DIR.resolve()}")
        return

    raw_images = [
        f for f in TARGET_DIR.glob("step*.camera.png") 
        if "semantic segmentation" not in f.name
    ]
    
    print(f"対象フォルダ: {TARGET_DIR.resolve()}")
    print(f"検出された元画像数: {len(raw_images)} 件")

    # 再現性のためにシード値を固定してシャッフル
    random.seed(42)
    sorted_raw_images = sorted(raw_images)
    random.shuffle(sorted_raw_images)

    val_count = int(len(sorted_raw_images) * VAL_RATIO)
    success_count = 0

    for idx, img_path in enumerate(sorted_raw_images):
        # train と val の割り振り決定
        is_val = idx < val_count
        target_img_dir = val_img_dir if is_val else train_img_dir
        target_lbl_dir = val_lbl_dir if is_val else train_lbl_dir

        step_prefix = img_path.name.split(".camera")[0] 
        mask_path = TARGET_DIR / f"{step_prefix}.camera.semantic segmentation.png"

        if not mask_path.exists():
            print(f"⚠️ 警告: {img_path.name} に対応するマスク ({mask_path.name}) が見つかりません。")
            continue

        img = cv2.imread(str(img_path))
        mask = cv2.imread(str(mask_path))

        if img is None or mask is None:
            print(f"⚠️ エラー: {img_path.name} または {mask_path.name} の読み込みに失敗。")
            continue

        mask_rgb = cv2.cvtColor(mask, cv2.COLOR_BGR2RGB)
        h, w, _ = mask_rgb.shape

        yolo_lines = []

        # 各クラス色ごとに輪郭抽出 (YOLO Polygon形式)
        for target_rgb, class_id in COLOR_TO_CLASS.items():
            lower_bound = np.array(target_rgb) - 10
            upper_bound = np.array(target_rgb) + 10
            binary_mask = cv2.inRange(mask_rgb, lower_bound, upper_bound)

            contours, _ = cv2.findContours(binary_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

            for cnt in contours:
                if cv2.contourArea(cnt) < 10:
                    continue

                polygon = []
                for point in cnt:
                    x, y = point[0]
                    polygon.append(f"{x / w:.6f}")
                    polygon.append(f"{y / h:.6f}")

                if len(polygon) >= 6:
                    line = f"{class_id} " + " ".join(polygon)
                    yolo_lines.append(line)

        out_name = step_prefix

        # 1. 画像の保存
        cv2.imwrite(str(target_img_dir / f"{out_name}.jpg"), img)

        # 2. テキストラベルの保存
        txt_path = target_lbl_dir / f"{out_name}.txt"
        with open(txt_path, "w", encoding="utf-8") as f:
            f.write("\n".join(yolo_lines))

        success_count += 1

    print(f"\n✅ 変換完了: 全{success_count}組を '{OUTPUT_DIR}' フォルダに展開しました。")
    print(f"  - Train: {success_count - val_count} 件")
    print(f"  - Val: {val_count} 件")

if __name__ == "__main__":
    convert_sequence_masks()