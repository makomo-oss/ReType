using ReType.Core;
using Xunit;

namespace ReType.Tests;

public class RenameDetectorTests
{
    private static readonly RenameDetector Detector = new();

    [Fact]
    public void SamePath_IsIgnored()
    {
        Assert.Equal(RenameDecisionKind.Ignore, Detector.Detect(@"C:\t\a.jpg", @"C:\t\a.jpg").Kind);
    }

    [Fact]
    public void PlainRename_SameExtension_IsIgnored()
    {
        var decision = Detector.Detect(@"C:\t\old name.jpg", @"C:\t\new name.jpg");
        Assert.Equal(RenameDecisionKind.Ignore, decision.Kind);
    }

    [Fact]
    public void ExtensionChange_SupportedPair_Converts()
    {
        var decision = Detector.Detect(@"C:\t\photo.jpg", @"C:\t\photo.webp");
        Assert.Equal(RenameDecisionKind.Convert, decision.Kind);
        Assert.Equal(".jpg", decision.FromExtension);
        Assert.Equal(".webp", decision.ToExtension);
    }

    [Fact]
    public void ExtensionAlias_JpgToJpeg_IsIgnored()
    {
        Assert.Equal(RenameDecisionKind.Ignore, Detector.Detect(@"C:\t\photo.jpg", @"C:\t\photo.jpeg").Kind);
    }

    [Fact]
    public void CrossCategoryChange_IsUnsupported()
    {
        Assert.Equal(RenameDecisionKind.Unsupported, Detector.Detect(@"C:\t\photo.jpg", @"C:\t\photo.mp3").Kind);
    }

    [Fact]
    public void UnknownTargetExtension_IsLeftAlone()
    {
        // A target extension ReType does not know is not a conversion request;
        // never roll back (programs rename files to unknown extensions too).
        Assert.Equal(RenameDecisionKind.Ignore, Detector.Detect(@"C:\t\photo.jpg", @"C:\t\photo.xyz").Kind);
    }

    [Theory]
    [InlineData(@"C:\t\setup.exe.crdownload", @"C:\t\setup.exe")]  // Chrome download finish
    [InlineData(@"C:\t\setup.exe.tmp", @"C:\t\setup.exe")]         // Edge download finish
    [InlineData(@"C:\t\setup.exe.part", @"C:\t\setup.exe")]        // Firefox download finish
    [InlineData(@"C:\t\setup.exe.download", @"C:\t\setup.exe")]    // Safari download finish
    [InlineData(@"C:\t\photo.jpg", @"C:\t\photo.jpg.tmp")]         // editor atomic save
    [InlineData(@"C:\t\notes.docx", @"C:\t\notes.wbk")]            // Word backup rename
    [InlineData(@"C:\t\data.tmp", @"C:\t\data.jpg")]               // extraction/save via temp
    public void TransientExtensionRename_IsIgnored(string oldPath, string newPath)
    {
        Assert.Equal(RenameDecisionKind.Ignore, Detector.Detect(oldPath, newPath).Kind);
    }

    [Fact]
    public void UnknownSourceExtension_IsIgnored()
    {
        // ReType only converts formats it understands; unknown sources are untouched.
        Assert.Equal(RenameDecisionKind.Ignore, Detector.Detect(@"C:\t\data.qqq", @"C:\t\data.md").Kind);
    }

    [Fact]
    public void HiddenExtensionPattern_Converts()
    {
        // With extensions hidden, renaming photo.jpg to "photo.webp" yields photo.webp.jpg on disk.
        var decision = Detector.Detect(@"C:\t\photo.jpg", @"C:\t\photo.webp.jpg");
        Assert.Equal(RenameDecisionKind.Convert, decision.Kind);
        Assert.Equal(".jpg", decision.FromExtension);
        Assert.Equal(".webp", decision.ToExtension);
        Assert.Equal(@"C:\t\photo.webp.jpg", decision.NewPath);
    }

    [Fact]
    public void HiddenExtensionPattern_UnsupportedStem_IsIgnored()
    {
        // "my.photo.jpg" is a plain rename, not a hidden-extension conversion request.
        Assert.Equal(RenameDecisionKind.Ignore, Detector.Detect(@"C:\t\photo.jpg", @"C:\t\my.photo.jpg").Kind);
    }

    [Fact]
    public void AudioContainerChange_Converts()
    {
        var decision = Detector.Detect(@"C:\t\song.wav", @"C:\t\song.mp3");
        Assert.Equal(RenameDecisionKind.Convert, decision.Kind);
    }
}
