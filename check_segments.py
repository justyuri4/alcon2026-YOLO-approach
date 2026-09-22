from pathlib import Path
import argparse


def check_label_file(label_path: Path, class_count: int) -> list[str]:
    errors = []

    try:
        lines = label_path.read_text(encoding="utf-8").splitlines()
    except UnicodeDecodeError:
        return ["UTF-8で読み込めません"]

    for line_number, line in enumerate(lines, start=1):
        if not line.strip():
            continue

        values = line.split()

        if len(values) < 7:
            errors.append(
                f"{line_number}行目: 座標が不足しています（{len(values)}項目）"
            )
            continue

        if (len(values) - 1) % 2 != 0:
            errors.append(
                f"{line_number}行目: 座標の数が不正です"
            )
            continue

        try:
            class_id = int(values[0])
            coordinates = [float(value) for value in values[1:]]
        except ValueError:
            errors.append(f"{line_number}行目: 数値として解釈できません")
            continue

        if not 0 <= class_id < class_count:
            errors.append(
                f"{line_number}行目: class_id={class_id} が範囲外です"
            )

        invalid_coordinates = [
            value for value in coordinates
            if not 0.0 <= value <= 1.0
        ]

        if invalid_coordinates:
            errors.append(
                f"{line_number}行目: 座標が0～1の範囲外です"
            )

    return errors


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--labels", required=True, help="ラベルフォルダー")
    parser.add_argument("--classes", type=int, required=True, help="クラス数")
    args = parser.parse_args()

    label_root = Path(args.labels)
    label_files = sorted(label_root.rglob("*.txt"))

    valid_count = 0
    invalid_count = 0

    for label_path in label_files:
        errors = check_label_file(label_path, args.classes)

        if errors:
            invalid_count += 1
            print(f"\n[NG] {label_path}")
            for error in errors:
                print(f"  - {error}")
        else:
            valid_count += 1

    print("\n検査結果")
    print(f"正常: {valid_count}")
    print(f"異常: {invalid_count}")
    print(f"合計: {len(label_files)}")


if __name__ == "__main__":
    main()