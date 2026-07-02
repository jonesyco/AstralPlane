using ImageMagick;

namespace AstralPlane.Core.Tests;

// Phase 1 smoke tests: confirm the Magick.NET-Q16-x64 native library loads and
// works inside the test host, and that the delegates we depend on are present.
public class MagickSmokeTests
{
    [Fact]
    public void CanCreateEncodeAndDecodeAnImage()
    {
        using var image = new MagickImage(MagickColors.CornflowerBlue, 8, 8);
        byte[] png = image.ToByteArray(MagickFormat.Png);

        using var roundTrip = new MagickImage(png);
        Assert.Equal(8u, roundTrip.Width);
        Assert.Equal(8u, roundTrip.Height);
        Assert.Equal(MagickFormat.Png, roundTrip.Format);
    }

    [Theory]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Avif)]
    public void RequiredOutputFormatsSupportWriting(MagickFormat format)
    {
        IMagickFormatInfo? info = MagickFormatInfo.Create(format);
        Assert.NotNull(info);
        Assert.True(info!.SupportsWriting, $"{format} should support writing in this build.");
    }
}
