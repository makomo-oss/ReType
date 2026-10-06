namespace ReType.Core.Config;

/// <summary>ファミリー別の品質設定。</summary>
public sealed class QualitySettings
{
    /// <summary>画像品質 (0-100)。JPEG/WebP 等のエンコーダに適用。</summary>
    public int ImageQuality { get; set; } = 90;

    /// <summary>音声ビットレート (kbps)。FFmpeg の -b:a に適用。</summary>
    public int AudioBitrateKbps { get; set; } = 192;

    /// <summary>動画 CRF (0-51、小さいほど高品質)。FFmpeg の -crf に適用。</summary>
    public int VideoCrf { get; set; } = 23;
}

/// <summary>アプリ全体の設定。settings.json に永続化される。</summary>
public sealed class Settings
{
    /// <summary>変換後も元のファイル（元拡張子のコピー）を残すか。false なら変換結果で上書き。</summary>
    public bool KeepOriginal { get; set; } = true;

    public QualitySettings Quality { get; set; } = new();

    /// <summary>監視対象フォルダの絶対パス一覧。</summary>
    public List<string> WatchedFolders { get; set; } = [];
}
