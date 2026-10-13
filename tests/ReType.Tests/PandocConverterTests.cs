using ReType.Converters;
using ReType.Core;
using Xunit;

namespace ReType.Tests;

public sealed class PandocConverterTests : IDisposable
{
    private const string SampleMarkdown = """
        # Title

        Hello **world**, this is a *test*.

        - item one
        - item two
        """;

    private readonly string _dir;
    private readonly PandocConverter _converter = new();

    public PandocConverterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "retype-pandoc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task MdToHtml_ProducesHtml()
    {
        if (ToolRunner.Locate("pandoc.exe") is null)
        {
            return; // pandoc not installed on this machine; nothing to verify.
        }

        var md = Path.Combine(_dir, "doc.md");
        var html = Path.Combine(_dir, "doc.html");
        await File.WriteAllTextAsync(md, SampleMarkdown);

        await _converter.ConvertAsync(md, ".md", ".html", html, CancellationToken.None);

        var content = await File.ReadAllTextAsync(html);
        Assert.Contains("<h1", content);
        Assert.Contains("Hello", content);
    }

    [Fact]
    public async Task MdToDocx_ProducesZipContainer()
    {
        if (ToolRunner.Locate("pandoc.exe") is null)
        {
            return;
        }

        var md = Path.Combine(_dir, "doc.md");
        var docx = Path.Combine(_dir, "doc.docx");
        await File.WriteAllTextAsync(md, SampleMarkdown);

        await _converter.ConvertAsync(md, ".md", ".docx", docx, CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(docx);
        Assert.True(bytes.Length > 4 && bytes[0] == 0x50 && bytes[1] == 0x4B, "docx should be a zip container");
    }

    [Fact]
    public void CanConvert_RespectsMatrix()
    {
        Assert.True(_converter.CanConvert(".md", ".docx"));
        Assert.False(_converter.CanConvert(".png", ".jpg"));
    }
}
