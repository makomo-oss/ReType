using ReType.Core;

namespace ReType.Core.Tests;

public class FormatCatalogTests
{
    [Theory]
    [InlineData(".png", FileFamily.Image)]
    [InlineData("jpg", FileFamily.Image)]
    [InlineData(".avif", FileFamily.Image)]
    [InlineData(".mp4", FileFamily.Video)]
    [InlineData(".mkv", FileFamily.Video)]
    [InlineData(".mp3", FileFamily.Audio)]
    [InlineData(".m4a", FileFamily.Audio)]
    [InlineData(".md", FileFamily.Document)]
    [InlineData(".pdf", FileFamily.Document)]
    public void KnownExtensions_MapToFamilies(string extension, FileFamily expected)
    {
        Assert.Equal(expected, FormatCatalog.GetFamily(extension));
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".zip")]
    [InlineData("")]
    public void UnknownExtensions_MapToUnknown(string extension)
    {
        Assert.Equal(FileFamily.Unknown, FormatCatalog.GetFamily(extension));
    }

    [Fact]
    public void ExtensionsOf_ReturnsDotPrefixedExtensions()
    {
        var image = FormatCatalog.ExtensionsOf(FileFamily.Image);
        Assert.Contains(".png", image);
        Assert.Contains(".jpeg", image);
        Assert.All(image, e => Assert.StartsWith(".", e));
    }
}
