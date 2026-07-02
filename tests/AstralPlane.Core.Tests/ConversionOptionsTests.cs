using AstralPlane.Core;
using ImageMagick;

namespace AstralPlane.Core.Tests;

public class ConversionOptionsTests
{
    private static MagickImage NewImage() => new(MagickColors.Red, 4, 4);

    [Fact]
    public void MapsFormatAndQuality()
    {
        var options = ConversionOptions.For(OutputFormat.Jpg) with { Quality = 72 };
        using var image = NewImage();
        MagickEncoding.Apply(image, options);

        Assert.Equal(MagickFormat.Jpeg, image.Format);
        Assert.Equal(72u, image.Quality);
    }

    [Fact]
    public void WebPLosslessSetsDefineAndMaxQuality()
    {
        var options = ConversionOptions.For(OutputFormat.WebP) with { Lossless = true };
        using var image = NewImage();
        MagickEncoding.Apply(image, options);

        Assert.Equal("true", image.Settings.GetDefine(MagickFormat.WebP, "lossless"));
        Assert.Equal(100u, image.Quality);
    }

    [Fact]
    public void PngIsAlwaysLosslessRegardlessOfFlag()
    {
        var options = ConversionOptions.For(OutputFormat.Png) with { Lossless = false };
        using var image = NewImage();
        MagickEncoding.Apply(image, options);

        Assert.Equal(MagickFormat.Png, image.Format);
    }

    [Fact]
    public void JpgIgnoresLosslessFlag()
    {
        // JPEG is never lossless; the flag must not push quality to 100.
        var options = ConversionOptions.For(OutputFormat.Jpg) with { Quality = 80, Lossless = true };
        using var image = NewImage();
        MagickEncoding.Apply(image, options);

        Assert.Equal(80u, image.Quality);
    }

    [Fact]
    public void RejectsQualityOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConversionOptions.For(OutputFormat.Jpg) with { Quality = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => ConversionOptions.For(OutputFormat.Jpg) with { Quality = 101 });
    }

    [Fact]
    public void DefaultsAreSensible()
    {
        var options = ConversionOptions.For(OutputFormat.Avif);
        Assert.Equal(OutputFormat.Avif, options.TargetFormat);
        Assert.InRange(options.Quality, 1, 100);
        Assert.False(options.Lossless);
        Assert.Equal(ResizeMode.None, options.Resize.Mode);
        Assert.Equal(MetadataPolicy.Preserve, options.Metadata);
    }
}
