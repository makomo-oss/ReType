using ReType.Core.Updates;
using Xunit;

namespace ReType.Tests;

public class UpdateCheckerTests
{
    private const string SampleReleaseJson = """
        {
          "tag_name": "v0.2.0",
          "name": "ReType 0.2.0",
          "assets": [
            { "name": "other-file.zip", "browser_download_url": "https://example.invalid/other.zip" },
            { "name": "ReType-Setup-0.2.0.exe", "browser_download_url": "https://example.invalid/ReType-Setup-0.2.0.exe" }
          ]
        }
        """;

    [Fact]
    public void Parse_FindsInstallerAsset()
    {
        var release = ReleaseInfo.Parse(SampleReleaseJson);

        Assert.NotNull(release);
        Assert.Equal("v0.2.0", release!.TagName);
        Assert.Equal(new Version(0, 2, 0), release.Version);
        Assert.Equal("ReType-Setup-0.2.0.exe", release.AssetName);
        Assert.Equal("https://example.invalid/ReType-Setup-0.2.0.exe", release.DownloadUrl);
    }

    [Fact]
    public void Parse_NoInstallerAsset_ReturnsNull()
    {
        const string json = """
            { "tag_name": "v1.0.0", "assets": [ { "name": "source.zip", "browser_download_url": "https://x/y" } ] }
            """;
        Assert.Null(ReleaseInfo.Parse(json));
    }

    [Theory]
    [InlineData("v0.1.2", 0, 1, 2)]
    [InlineData("0.1.2", 0, 1, 2)]
    [InlineData("release-7", null)]
    public void ParseVersion_HandlesTagFormats(string tag, int major, int? minor = null, int? build = null)
    {
        var version = ReleaseInfo.ParseVersion(tag);
        if (minor is null)
        {
            Assert.Null(version);
        }
        else
        {
            Assert.Equal(new Version(major, minor.Value, build ?? 0), version);
        }
    }

    [Fact]
    public void IsUpdateAvailable_ComparesVersions()
    {
        var newer = new ReleaseInfo("v0.2.0", new Version(0, 2, 0), "https://x/y.exe", "y.exe");
        var same = new ReleaseInfo("v0.1.1", new Version(0, 1, 1), "https://x/y.exe", "y.exe");

        Assert.True(UpdateChecker.IsUpdateAvailable(newer, new Version(0, 1, 1)));
        Assert.False(UpdateChecker.IsUpdateAvailable(same, new Version(0, 1, 1)));
        Assert.False(UpdateChecker.IsUpdateAvailable(null, new Version(0, 1, 1)));
    }
}
