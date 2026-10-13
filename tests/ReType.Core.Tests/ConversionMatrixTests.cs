using ReType.Core;
using Xunit;

namespace ReType.Core.Tests;

public class ConversionMatrixTests
{
    [Theory]
    [InlineData(".jpg", ".png")]
    [InlineData(".png", ".webp")]
    [InlineData(".mp4", ".mkv")]
    [InlineData(".wav", ".mp3")]
    [InlineData(".md", ".docx")]
    public void SameCategoryPair_IsSupported(string from, string to)
    {
        Assert.True(ConversionMatrix.IsSupported(from, to));
    }

    [Theory]
    [InlineData(".jpg", ".jpg")]
    [InlineData(".jpg", ".jpeg")]
    [InlineData(".tif", ".tiff")]
    [InlineData(".htm", ".html")]
    public void SameOrAliasedFormat_IsNotAConversion(string from, string to)
    {
        Assert.False(ConversionMatrix.IsSupported(from, to));
    }

    [Theory]
    [InlineData(".jpg", ".mp3")]
    [InlineData(".md", ".png")]
    [InlineData(".jpg", ".xyz")]
    [InlineData(".abc", ".png")]
    public void CrossCategoryOrUnknown_IsUnsupported(string from, string to)
    {
        Assert.False(ConversionMatrix.IsSupported(from, to));
    }

    [Fact]
    public void Extensions_AreCaseInsensitive()
    {
        Assert.True(ConversionMatrix.IsSupported(".JPG", ".WEBP"));
    }

    [Theory]
    [InlineData(".png", ConversionCategory.Image)]
    [InlineData(".mp4", ConversionCategory.Video)]
    [InlineData(".flac", ConversionCategory.Audio)]
    [InlineData(".docx", ConversionCategory.Document)]
    [InlineData(".xyz", null)]
    public void CategoryOf_ResolvesKnownExtensions(string ext, ConversionCategory? expected)
    {
        Assert.Equal(expected, ConversionMatrix.CategoryOf(ext));
    }
}
