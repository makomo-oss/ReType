# ReType

**ファイル変換を、名前変更と同じくらい簡単に。**

ReType は、ファイルの拡張子を変更するだけで、誰でも簡単にファイル形式を変換できる Windows アプリです。
画像・動画・音声・文書など、さまざまな形式に対応し、専用ソフトを開いたり複雑な設定をしたりする必要なく、
エクスプローラー上で直感的に変換できます。

## 使い方

1. ReType を起動するとタスクトレイに常駐します。
2. 監視フォルダ（既定: デスクトップ / ドキュメント / ピクチャ / ビデオ / Downloads）で
   ファイルの拡張子を書き換えます。

   ```
   photo.jpg  →  photo.webp     # JPEG が本物の WebP に変換される
   song.wav   →  song.mp3       # 音声を MP3 に
   video.mp4  →  video.mkv      # 再エンコードなしでリパック
   notes.md   →  notes.docx     # 文書を変換
   ```

3. 変換が完了するとバルーン通知が出ます。失敗した場合や未対応の変換の場合は、
   **拡張子は自動的に元に戻される**ため、壊れたファイルは残りません。

拡張子の表示がオフの環境でも動作します（`photo.webp.jpg` のようなパターンを検知します）。

## インストール

GitHub の **[Releases](https://github.com/makomo-oss/ReType/releases)** ページから
**`ReType-Setup-<version>.exe`** をダウンロードして実行してください。

1. ウィザードに従うだけです（管理者権限は不要。ユーザーごとにインストールされます）。
   - 「Windows の起動時に ReType を自動的に開始する」は既定でオンです。
   - ffmpeg と pandoc が同梱されているため、追加のインストールは不要です。
2. 削除は「設定」→「アプリ」から行えます。

> メモ: 未署名のインストーラーのため、SmartScreen が表示される場合があります。
> 「詳細情報」→「実行」で進められます。

## アップデート

インストール後は**アプリ内でアップデート**できます。追加のダウンロードは不要です。

- 起動時に GitHub Releases を自動チェックし、新しいバージョンがあれば
  バルーン通知とトレイメニューの「ReType X.Y.Z にアップデート」でお知らせします。
- 選択するとインストーラーのダウンロード→サイレントインストール→
  アプリの自動再起動までが一気に行われます。
- 手動ではトレイメニューの「更新を確認...」から。

### 開発者: リリース手順

タグメッセージがそのまま**リリースノート**になります（アノテートタグを使う）。

```powershell
# 1. バージョンを上げて main に push（csproj / installer\ReType.iss / scripts\build-installer.ps1）
# 2. リリースノート付きのタグを打つ
git tag -a v0.1.5 -m "## 新機能

- ...

## 修正

- ..."
git push origin v0.1.5     # CI がインストーラーをビルドし、ノートを付けて Releases に公開
```

軽量タグ（`git tag v0.1.5`）の場合は、前タグ以降のコミット件名が自動でノートになります。

## 対応形式

| カテゴリ | 拡張子 | エンジン |
|---|---|---|
| 画像 | jpg / jpeg / png / webp / bmp / gif / tif / tiff | Windows 内蔵コーデック（WebP のみ ffmpeg） |
| 動画 | mp4 / mkv / mov / webm / avi / ts | ffmpeg（互換なら無劣化リパック、非互換なら再エンコード） |
| 音声 | mp3 / wav / flac / m4a / aac / ogg / opus / wma | ffmpeg |
| 文書 | md / html / txt / docx / epub / rst / rtf | pandoc |

- `.jpg → .jpeg` など同一形式の拡張子変更は、単なる名前変更として素通しします。
- カテゴリをまたぐ変換（画像→音声など）は未対応です。両方の拡張子既知の形式
  （例 `.jpg → .mp3`）の場合のみ、拡張子を元に戻して通知します。
- ダウンロードや一時保存に伴うリネーム（`.tmp` / `.crdownload` / `.part` /
  `.download` / `.bak` など）や、ReType が知らない拡張子への変更には一切干渉しません
  （ブラウザでの exe ダウンロードなどを壊しません）。

## 仕組み

1. `FileSystemWatcher` で監視フォルダのリネームを検知
2. 新旧の拡張子を比較し、変換マトリックスと照合
3. 一時ファイルへ変換 → 成功すればリネームされたファイルを置換、失敗すれば拡張子を復元
4. ReType 自身の書き込みはループ防止台帳で無視（OneDrive の未同期ファイルはスキップ）

## ビルド

.NET 10 SDK が必要です。

```powershell
dotnet build ReType.slnx
dotnet test tests/ReType.Tests

# 変換エンジン（ffmpeg / pandoc）を取得して thirdparty\bin に配置
.\scripts\fetch-tools.ps1
```

ffmpeg と pandoc は `thirdparty\bin` または PATH から自動的に検出されます。

### 実行

```powershell
# タトレイ常駐アプリ
dotnet run --project src/ReType.App

# CLI（監視モード / 単発変換）
dotnet run --project src/ReType.Cli -- watch <フォルダ>
dotnet run --project src/ReType.Cli -- convert <ファイル> <.変換先拡張子>
```

## 制限事項

- PDF の入出力には対応していません
- HEIC 画像はシステムの HEIF コーデックに依存します
- OneDrive のオンデマンド（未ダウンロード）ファイルは変換しません
- 長時間の動画変換中の進捗表示・キャンセルは今後の予定

## 開発ステータス

- [x] 拡張子変更の検知（隠し拡張子パターン対応）
- [x] 画像変換（Windows 内蔵コーデック + WebP）
- [x] 音声・動画変換（ffmpeg、リパック/再エンコード自動判定）
- [x] 文書変換（pandoc）
- [x] タスクトレイ常駐 + 設定 UI + 自動起動
- [x] インストーラー配布（Inno Setup、ffmpeg/pandoc 同梱）
- [ ] コードサイン（SmartScreen 警告の解消）
- [ ] 変換進捗トーストとキャンセル
