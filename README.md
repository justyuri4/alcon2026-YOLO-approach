<h2>環境構築</h1>

pip install ultralytics opencv-python pandas

from ultralytics import YOLO
model = YOLO("yolo11n-seg.pt") # セグメンテーション用の軽量モデルを自動ダウンロード
print("インストール成功！")
