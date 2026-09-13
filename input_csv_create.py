import os
import csv
from PIL import Image

# ==========================================
# 設定
# ==========================================
# カレントディレクトリを対象に設定
IMAGE_DIR = "."

# input.csv の保存先
OUTPUT_CSV = "input.csv"

# 対象とする画像の拡張子 (lower()で判定するため小文字のみに統一)
VALID_EXTENSIONS = ('.jpg', '.jpeg', '.png', '.bmp')


def generate_input_csv(target_dir, csv_path):
    if not os.path.exists(target_dir):
        print(f"エラー: フォルダ '{target_dir}' が見つかりません。")
        return

    image_entries = []

    # カレントディレクトリ内のファイルを取得 (直下のみを対象)
    for file in sorted(os.listdir(target_dir)):
        # 拡張子判定（大文字小文字を区別しない）
        if file.lower().endswith(VALID_EXTENSIONS):
            # output.csv 自体や自分自身は除外
            full_path = os.path.join(target_dir, file)
            rel_path = os.path.relpath(full_path, start=".").replace("\\", "/")

            try:
                # 画像を開いてサイズ（幅, 高さ）を取得
                with Image.open(full_path) as img:
                    width, height = img.size
                    image_entries.append((width, height, rel_path))
            except Exception as e:
                print(f"警告: {full_path} の読み込みに失敗しました ({e})。スキップします。")

    if not image_entries:
        print(f"対象となる画像ファイルが '{target_dir}' 内に見つかりませんでした。")
        return

    # input.csv に書き出し
    with open(csv_path, "w", encoding="utf-8") as f:
        # 1行目: 処理画像数 N
        f.write(f"{len(image_entries)}\n")
        
        # 2行目以降: 画像幅,画像高さ,入力画像ファイルの相対パス
        for width, height, rel_path in image_entries:
            f.write(f"{width},{height},{rel_path}\n")

    print(f"成功: {len(image_entries)} 件の画像情報を '{csv_path}' に書き出しました。")


if __name__ == "__main__":
    generate_input_csv(IMAGE_DIR, OUTPUT_CSV)