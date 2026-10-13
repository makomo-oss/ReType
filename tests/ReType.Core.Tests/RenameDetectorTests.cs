using ReType.Core;
using Xunit;

namespace ReType.Core.Tests;

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
    public void UnknownExtension_IsUnsupported()
    {
        Assert.Equal(RenameDecisionKind.Unsupported, Detector.Detect(@"C:\t\photo.jpg", @"C:\t\photo.xyz").Kind);
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
