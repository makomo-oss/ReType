using ReType.Core.Config;

namespace ReType.Core.Converters;

/// <summary>
/// フォーマット変換エンジンの抽象。入力ファイルを読み、指定出力パスに書き出す。
/// 呼び出し側（JobQueue）は出力パスを SelfWriteTracker に登録してから呼ぶ。
/// </summary>
public interface IConverter
{
    /// <summary>GUI/ログ表示用のエンジン名（例: "ImageSharp", "FFmpeg", "Pandoc"）。</summary>
    string Name { get; }

    /// <summary>このファミリー組み合わせを変換できるか。</summary>
    bool CanConvert(FileFamily source, FileFamily target);

    /// <summary>inputPath の内容を targetFamily で読み、outputPath に書く。</summary>
    Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        Settings settings,
        CancellationToken ct);
}
