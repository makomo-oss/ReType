namespace ReType.Core.Jobs;

/// <summary>
/// リネーム検出から生成される変換ジョブ。
/// NewName のファイルに元の内容（旧拡張子のフォーマット）が残っている状態を前提とする。
/// </summary>
public sealed record ConversionJob(
    Guid Id,
    string Directory,
    string OriginalName,   // リネーム前の名前（例: photo.png）
    string NewName,        // リネーム後の名前（例: photo.jpg）。内容はまだ旧フォーマット
    FileFamily SourceFamily,
    FileFamily TargetFamily)
{
    /// <summary>変換入力（リネーム後のファイル。内容のフォーマットは SourceFamily）。</summary>
    public string InputPath => System.IO.Path.Combine(Directory, NewName);

    /// <summary>KeepOriginal 時に元内容を保存するパス（旧名復元）。</summary>
    public string OriginalPath => System.IO.Path.Combine(Directory, OriginalName);
}
