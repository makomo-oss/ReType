namespace ReType.Core.Tools;

/// <summary>
/// ffmpeg / pandoc などの外部ツールExecutableを、設定された tools/ フォルダと PATH から解決する。
/// 優先順位: ToolsDirectory > PATH。見つからなければ null（呼び出し側でエラー扱い）。
/// </summary>
public sealed class ExternalToolResolver
{
    private readonly string? toolsDirectory;

    public ExternalToolResolver(string? toolsDirectory = null)
    {
        this.toolsDirectory = string.IsNullOrWhiteSpace(toolsDirectory) ? null : toolsDirectory;
    }

    /// <summary>ツール名（例: "ffmpeg"）から実行ファイルの絶対パスを返す。見つからなければ null。</summary>
    public string? Find(string toolName)
    {
        var exeName = OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;

        // 1. tools/ フォルダが優先。
        if (toolsDirectory is not null)
        {
            var candidate = Path.Combine(toolsDirectory, exeName);
            if (File.Exists(candidate))
                return candidate;
        }

        // 2. PATH を走査。
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;
            var candidate = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
