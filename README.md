# ReType

ファイルの拡張子を変更すると、自動的にそのファイルを変換するアプリケーションです。Windows Explorerでファイルをリネームした瞬間に変換が開始されます。

## 特徴

- **即時変換**: ファイル名を変更した瞬間に変換が開始されます
- **広範なフォーマット対応**: 画像・動画・音声・ドキュメント形式をサポート
- **GUI**: ドラッグ&ドロップとディレクトリ監視の2つのモードに対応
- **設定可能**: 品質設定や元のファイルの保持/削除を設定で切り替え可能

## 対応フォーマット

### 画像
PNG, JPEG, WebP, GIF, BMP, TIFF, AVIF

### 動画
MP4, WebM, AVI, MOV, MKV

### 音声
MP3, WAV, FLAC, OGG, M4A

### ドキュメント
Markdown, TXT, HTML, RTF, PDF

## インストール

### 前提条件

- Python 3.10+
- FFmpeg（動画・音声変換用）[ダウンロード](https://ffmpeg.org/download.html)
- pandoc（ドキュメント変換用、オプション）[ダウンロード](https://pandoc.org/installing.html)

### 手順

```bash
pip install -r requirements.txt
```

## 使用方法

### GUI アプリケーション

```bash
python -m src.main
```

または

```bash
python src/main.py
```

アプリケーション起動後、以下の2つの方法でファイルを変換できます：

1. **ドラッグ&ドロップ**: ファイルをアプリのドロップゾーンにドラッグ&ドロップ
2. **ディレクトリ監視**: 「Add Watch Directory」で監視するフォルダを選択。そのフォルダ内のファイル拡張子変更を自動検知

## 設定

`config/settings.yaml` で設定できます：

```yaml
# 変換後の元ファイル保持（true = 保持, false = 削除）
keep_original: true

# 変換品質（0-100）
quality: 85

# 既存ファイルの自動上書き
overwrite_existing: false

# 各ファイルタイプの監視有効化
monitor:
  image: true
  video: true
  audio: true
  document: true
```

## アーキテクチャ

```
watchdog FileSystemWatcher
    ↓ ファイル変更を検知
ConversionEngine (変換オーケストレーター)
    ↓ フォーマット判別
FormatConverters
    ├── ImageConverter (Pillow)
    ├── VideoConverter (FFmpeg)
    ├── AudioConverter (FFmpeg)
    └── DocumentConverter (Pandoc)
```

## テスト

```bash
python -m pytest tests/ -v
```

## プロジェクト構成

```
ReType/
├── config/
│   └── settings.yaml      # 設定ファイル
├── src/
│   ├── main.py            # エントリポイント
│   ├── core/
│   │   ├── watcher.py     # ファイル監視
│   │   ├── detector.py    # 拡張子検出
│   │   └── engine.py      # 変換エンジン
│   ├── converters/
│   │   ├── base.py        # 変換基底クラス
│   │   ├── image_conv.py  # 画像変換
│   │   ├── video_conv.py  # 動画変換
│   │   ├── audio_conv.py  # 音声変換
│   │   └── doc_conv.py    # ドキュメント変換
│   ├── ui/
│   │   ├── main_window.py # メインウィンドウ
│   │   └── components.py  # UIコンポーネント
│   └── utils/
│       ├── config_loader.py
│       ├── logger.py
│       └── file_ops.py
├── tests/
│   ├── test_core.py
│   ├── test_image_conv.py
│   └── test_media_conv.py
├── requirements.txt
└── README.md
```

## ライセンス

MIT
