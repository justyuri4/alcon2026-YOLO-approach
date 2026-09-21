import sys
from pathlib import Path

# 画像サイズ(width, height)取得用にPillowを使用します
try:
    from PIL import Image
except ImportError:
    print("エラー: Pillow ライブラリが見つかりません。")
    print("以下のコマンドでインストールしてください:")
    print("  pip install Pillow")
    sys.exit(1)


def main():
    # 処理対象のディレクトリ（カレントディレクトリ）
    target_dir = Path('.')

    # 対象とする画像拡張子（大文字小文字対応のため小文字で定義）
    valid_extensions = {'.jpg', '.jpeg', '.png'}

    # ディレクトリ内の画像を検索（ソートして順番を固定）
    image_paths = sorted([
        p for p in target_dir.iterdir()
        if p.is_file() and p.suffix.lower() in valid_extensions
    ])

    # --------------------------------------------------
    # 判定・動作確認（デバッグ出力）
    # --------------------------------------------------
    print("=== 画像検索結果 ===")
    print(f"検索対象フォルダ : {target_dir.resolve()}")
    print(f"検出された画像数 : {len(image_paths)}")

    if not image_paths:
        print("\n⚠️ 画像ファイルが見つかりませんでした。")
        print("【フォルダ内のファイル一覧】")
        for p in target_dir.iterdir():
            if p.is_file():
                print(f"  - {p.name}")
        sys.exit(1)

    print("検出ファイル一覧:")
    for p in image_paths:
        print(f"  - {p.name}")
    print("====================\n")

    # --------------------------------------------------
    # input.csv の生成処理
    # --------------------------------------------------
    output_csv = target_dir / "input.csv"

    with open(output_csv, mode="w", encoding="utf-8", newline="") as f:
        # 1行目: 処理画像数 N
        f.write(f"{len(image_paths)}\n")

        # 2行目以降: [width],[height],[相対パス]
        for img_path in image_paths:
            try:
                with Image.open(img_path) as img:
                    width, height = img.size
                
                # パス表記（文字列）を取得
                relative_path = str(img_path)

                # 書き込み
                f.write(f"{width},{height},{relative_path}\n")

            except Exception as e:
                print(f"⚠️ {img_path.name} の処理中にエラーが発生しました: {e}")

    print(f"✅ 作成完了: {output_csv.resolve()}")


if __name__ == "__main__":
    main()