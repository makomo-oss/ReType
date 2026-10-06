using ReType.Core.Tools;

namespace ReType.Core.Tests;

public class ExternalToolResolverTests : IDisposable
{
    private readonly string originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";

    public void Dispose() => Environment.SetEnvironmentVariable("PATH", originalPath);

    private static string ExeName(string toolName) =>
        OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;

    private static string CreateFakeTool(string toolName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "retype-test-tools-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ExeName(toolName)), "fake tool");
        return dir;
    }

    [Fact]
    public void Find_ReturnsNull_WhenToolNotFound()
    {
        var resolver = new ExternalToolResolver();
        Assert.Null(resolver.Find("definitely-not-installed-xyz"));
    }

    [Fact]
    public void Find_FindsTool_InToolsDirectory()
    {
        var dir = CreateFakeTool("ffmpeg");
        try
        {
            var resolver = new ExternalToolResolver(dir);
            Assert.Equal(Path.Combine(dir, ExeName("ffmpeg")), resolver.Find("ffmpeg"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Find_FindsTool_InPath()
    {
        var dir = CreateFakeTool("pandoc");
        try
        {
            // PATH をフェイクディレクトリのみにして決定性を持たせる（Dispose で復元）。
            Environment.SetEnvironmentVariable("PATH", dir);
            var resolver = new ExternalToolResolver();
            Assert.Equal(Path.Combine(dir, ExeName("pandoc")), resolver.Find("pandoc"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ToolsDirectory_TakesPriority_OverPath()
    {
        var toolsDir = CreateFakeTool("ffmpeg");
        var pathDir = CreateFakeTool("ffmpeg");
        try
        {
            Environment.SetEnvironmentVariable("PATH", pathDir);
            var resolver = new ExternalToolResolver(toolsDir);
            Assert.Equal(Path.Combine(toolsDir, ExeName("ffmpeg")), resolver.Find("ffmpeg"));
        }
        finally
        {
            Directory.Delete(toolsDir, true);
            Directory.Delete(pathDir, true);
        }
    }
}
