using System.Windows.Forms;
using ReType.App;
using Xunit;

namespace ReType.Tests;

public class DropFormTests
{
    [Fact]
    public void SetFiles_Image_OffersConvertibleTargetsOnly()
    {
        using var form = new DropForm((_, _) => Task.FromResult<string?>(null));
        Assert.True(form.AllowDrop);

        form.SetFiles([@"C:\t\photo.jpg"]);

        var items = form.TargetFormatsForTest;
        Assert.Contains(".webp", items);
        Assert.Contains(".png", items);
        Assert.DoesNotContain(".jpg", items); // same format is not a conversion
    }

    [Fact]
    public void SetFiles_MixedCategories_OfferNothing()
    {
        using var form = new DropForm((_, _) => Task.FromResult<string?>(null));
        form.SetFiles([@"C:\t\photo.jpg", @"C:\t\song.wav"]);

        Assert.Empty(form.TargetFormatsForTest);
    }

    [Fact]
    public void SetFiles_UnknownExtension_DisablesConvert()
    {
        using var form = new DropForm((_, _) => Task.FromResult<string?>(null));
        form.SetFiles([@"C:\t\archive.xyz"]);

        Assert.Empty(form.TargetFormatsForTest);
        Assert.False(form.ConvertEnabledForTest);
    }
}
