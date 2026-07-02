using AstralPlane.Core;
using ImageMagick;

namespace AstralPlane.Core.Tests;

public class MagickInputLoaderTests
{
    private readonly IInputLoader _loader = new MagickInputLoader();

    [Theory]
    [InlineData("a.png", MagickFormat.Png)]
    [InlineData("a.jpg", MagickFormat.Jpeg)]
    [InlineData("a.webp", MagickFormat.WebP)]
    [InlineData("a.tiff", MagickFormat.Tiff)]
    [InlineData("a.bmp", MagickFormat.Bmp)]
    public void LoadsStandardRasterFormats(string name, MagickFormat format)
    {
        using var ws = new TempWorkspace();
        string path = ws.CreateImage(name, format, 40, 30);

        using MagickImage image = _loader.Load(path);
        Assert.Equal(40u, image.Width);
        Assert.Equal(30u, image.Height);
    }

    [Fact]
    public void LoadsFirstFrameOnlyOfAnimatedGif()
    {
        using var ws = new TempWorkspace();
        string path = ws.CreateAnimatedGif("anim.gif", 16, 16);

        using MagickImage image = _loader.Load(path);
        // A single MagickImage represents just the first frame.
        Assert.Equal(16u, image.Width);
        Assert.Equal(16u, image.Height);
    }

    [Fact]
    public void ThrowsOnCorruptFile()
    {
        using var ws = new TempWorkspace();
        string path = ws.PathFor("broken.png");
        File.WriteAllText(path, "this is not an image");

        Assert.ThrowsAny<Exception>(() => _loader.Load(path));
    }
}
