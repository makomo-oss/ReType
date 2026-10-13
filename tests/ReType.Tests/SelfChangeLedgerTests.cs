using ReType.Core;
using Xunit;

namespace ReType.Tests;

public class SelfChangeLedgerTests
{
    [Fact]
    public void RecordedPath_IsContained()
    {
        var ledger = new SelfChangeLedger();
        var path = Path.Combine(Path.GetTempPath(), "retype-ledger-a.txt");
        ledger.Record(path);
        Assert.True(ledger.Contains(path));
    }

    [Fact]
    public void UnknownPath_IsNotContained()
    {
        var ledger = new SelfChangeLedger();
        Assert.False(ledger.Contains(Path.Combine(Path.GetTempPath(), "never-recorded.txt")));
    }

    [Fact]
    public void Comparison_IsCaseInsensitive()
    {
        var ledger = new SelfChangeLedger();
        var path = Path.Combine(Path.GetTempPath(), "ReType-Case.TXT");
        ledger.Record(path);
        Assert.True(ledger.Contains(path.ToLowerInvariant()));
    }

    [Fact]
    public void ExpiredEntry_IsNotContained()
    {
        var ledger = new SelfChangeLedger(TimeSpan.FromMilliseconds(-1));
        var path = Path.Combine(Path.GetTempPath(), "retype-expired.txt");
        ledger.Record(path);
        Assert.False(ledger.Contains(path));
    }
}
