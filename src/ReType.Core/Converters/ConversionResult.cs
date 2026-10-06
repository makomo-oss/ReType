namespace ReType.Core.Converters;

/// <summary>変換結果。失敗時は Error にユーザー表示用のメッセージを入れる。</summary>
public sealed record ConversionResult(bool Success, string OutputPath, string? Error)
{
    public static ConversionResult Ok(string outputPath) => new(true, outputPath, null);
    public static ConversionResult Fail(string error) => new(false, "", error);
}
