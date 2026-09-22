import os
import sys

# CUDA を見えなくして PyTorch を CPU モードで動かす（必ず torch/ultralytics を import する前に実行）
os.environ['CUDA_VISIBLE_DEVICES'] = ''

# ultralytics 等の別のライブラリが環境変数を参照する場合に備える（任意）
os.environ.setdefault('YOLO_DEVICE', 'cpu')

# デバッグ用ログ（標準エラーに出すので通常の標準出力ログに混ざりません）
print("FORCE_CPU: CUDA_VISIBLE_DEVICES='' (forcing CPU)", file=sys.stderr)