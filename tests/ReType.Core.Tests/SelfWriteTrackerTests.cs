using ReType.Core.Jobs;

namespace ReType.Core.Tests;

public class SelfWriteTrackerTests
{
    [Fact]
    public void AddThenContains()
    {
        var tracker = new SelfWriteTracker();
        tracker.Add(@"C:\watched\photo.jpg");

        Assert.True(tracker.Contains(@"C:\watched\photo.jpg"));
        Assert.False(tracker.Contains(@"C:\watched\photo.png"));
    }

    [Fact]
    public void ComparisonIsCaseInsensitiveAndIgnoresTrailingSeparator()
    {
        var tracker = new SelfWriteTracker();
        tracker.Add(@"C:\watched\Photo.JPG");

        Assert.True(tracker.Contains(@"c:\watched\photo.jpg"));
        Assert.True(tracker.Contains(@"C:\watched\photo.jpg\"));
    }
}
