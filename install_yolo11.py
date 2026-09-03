from ultralytics import YOLO
model = YOLO("yolo11n-seg.pt") # セグメンテーション用の軽量モデルを自動ダウンロード
print("インストール成功！")