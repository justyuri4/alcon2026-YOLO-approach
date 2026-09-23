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
    (255, 0, 0): 0,
    (0, 255, 0): 1,
    (0, 0, 255): 2,
}

# ==========================================
# 設定 2: 相対パスと分割率の設定
# ==========================================
TARGET_DIR = Path("solo/sequence.0")
OUTPUT_DIR = Path("dataset")
VAL_RATIO = 0.2
CROP_SIZE = 600
MIN_FOREGROUND_PIXELS = 100

def convert_sequence_masks():
    train_img_dir = OUTPUT_DIR / "images/train"
    val_img_dir = OUTPUT_DIR / "images/val"
    train_lbl_dir = OUTPUT_DIR / "labels/train"
    val_lbl_dir = OUTPUT_DIR / "labels/val"

    for directory in [
        train_img_dir,
        val_img_dir,
        train_lbl_dir,
        val_lbl_dir,
    ]:
        directory.mkdir(parents=True, exist_ok=True)

    if not TARGET_DIR.exists():
        print(f"❌ エラー: 指定されたパスが存在しません: {TARGET_DIR.resolve()}")
        return

    raw_images = [
        file_path
        for file_path in TARGET_DIR.glob("step*.camera.png")
        if "semantic segmentation" not in file_path.name
    ]

    print(f"対象フォルダ: {TARGET_DIR.resolve()}")
    print(f"検出された元画像数: {len(raw_images)} 件")

    random.seed(42)
    sorted_raw_images = sorted(raw_images)
    random.shuffle(sorted_raw_images)

    val_count = int(len(sorted_raw_images) * VAL_RATIO)
    success_count = 0
    train_count = 0
    val_success_count = 0

    for index, img_path in enumerate(sorted_raw_images):
        step_prefix = img_path.name.split(".camera")[0]
        mask_path = TARGET_DIR / f"{step_prefix}.camera.semantic segmentation.png"

        if not mask_path.exists():
            print(
                f"⚠️ 警告: {img_path.name} に対応するマスク "
                f"({mask_path.name}) が見つかりません。"
            )
            continue

        img = cv2.imread(str(img_path))
        mask = cv2.imread(str(mask_path))

        if img is None or mask is None:
            print(
                f"⚠️ エラー: {img_path.name} または "
                f"{mask_path.name} の読み込みに失敗。"
            )
            continue

        if img.shape[:2] != mask.shape[:2]:
            print(
                f"⚠️ 警告: {img_path.name} と {mask_path.name} の画像サイズが異なります。"
            )
            continue

        height, width = img.shape[:2]

        if height < CROP_SIZE or width < CROP_SIZE:
            print(
                f"⚠️ 警告: {img_path.name} は "
                f"{CROP_SIZE}x{CROP_SIZE} より小さいためスキップします。"
            )
            continue

        half_crop_size = CROP_SIZE // 2

        # 画像端からCROP_SIZE / 2離れた範囲内で中心点をランダムに選択
        center_x = random.randint(
            half_crop_size,
            width - half_crop_size,
        )
        center_y = random.randint(
            half_crop_size,
            height - half_crop_size,
        )

        crop_x1 = center_x - half_crop_size
        crop_y1 = center_y - half_crop_size
        crop_x2 = center_x + half_crop_size
        crop_y2 = center_y + half_crop_size

        # 画像とマスクを同じ位置から切り出す
        cropped_img = img[crop_y1:crop_y2, crop_x1:crop_x2]
        cropped_mask = mask[crop_y1:crop_y2, crop_x1:crop_x2]

        is_val = index < val_count
        target_img_dir = val_img_dir if is_val else train_img_dir
        target_lbl_dir = val_lbl_dir if is_val else train_lbl_dir

        mask_rgb = cv2.cvtColor(cropped_mask, cv2.COLOR_BGR2RGB)
        yolo_lines = []

        # 各クラス色ごとに輪郭抽出(YOLO Polygon形式)
        for target_rgb, class_id in COLOR_TO_CLASS.items():
            lower_bound = np.clip(
                np.array(target_rgb, dtype=np.int16) - 10,
                0,
                255,
            ).astype(np.uint8)
            upper_bound = np.clip(
                np.array(target_rgb, dtype=np.int16) + 10,
                0,
                255,
            ).astype(np.uint8)

            binary_mask = cv2.inRange(
                mask_rgb,
                lower_bound,
                upper_bound,
            )

            contours, _ = cv2.findContours(
                binary_mask,
                cv2.RETR_EXTERNAL,
                cv2.CHAIN_APPROX_SIMPLE,
            )

            for contour in contours:
                if cv2.contourArea(contour) < MIN_FOREGROUND_PIXELS:
                    continue

                polygon = []

                for point in contour:
                    x, y = point[0]
                    polygon.append(f"{x / CROP_SIZE:.6f}")
                    polygon.append(f"{y / CROP_SIZE:.6f}")

                if len(polygon) >= 6:
                    yolo_lines.append(
                        f"{class_id} " + " ".join(polygon)
                    )

        out_name = step_prefix

        cv2.imwrite(
            str(target_img_dir / f"{out_name}.jpg"),
            cropped_img,
        )

        txt_path = target_lbl_dir / f"{out_name}.txt"
        with open(txt_path, "w", encoding="utf-8") as file:
            file.write("\n".join(yolo_lines))

        success_count += 1

        if is_val:
            val_success_count += 1
        else:
            train_count += 1

    print(
        f"\n✅ 変換完了: 全{success_count}組を "
        f"'{OUTPUT_DIR}' フォルダに展開しました。"
    )
    print(f"  - Train: {train_count} 件")
    print(f"  - Val: {val_success_count} 件")


if __name__ == "__main__":
    convert_sequence_masks()