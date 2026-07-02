using AstralPlane.Core;

namespace AstralPlane.Core.Tests;

public class FormatRegistryTests
{
    [Fact]
    public void ExposesFiveOutputFormats()
    {
        var formats = FormatRegistry.OutputFormats.Select(f => f.Format).ToList();
        Assert.Equal(
            new[] { OutputFormat.Jpg, OutputFormat.Png, OutputFormat.WebP, OutputFormat.Avif, OutputFormat.Heic },
            formats);
    }

    [Theory]
    [InlineData(OutputFormat.Jpg, ".jpg", true, LosslessSupport.Never)]
    [InlineData(OutputFormat.Png, ".png", false, LosslessSupport.Always)]
    [InlineData(OutputFormat.WebP, ".webp", true, LosslessSupport.Toggle)]
    [InlineData(OutputFormat.Avif, ".avif", true, LosslessSupport.Toggle)]
    [InlineData(OutputFormat.Heic, ".heic", true, LosslessSupport.Never)]
    public void OutputCapabilitiesMatchSpec(OutputFormat format, string ext, bool quality, LosslessSupport lossless)
    {
        var info = FormatRegistry.GetOutput(format);
        Assert.Equal(ext, info.FileExtension);
        Assert.Equal(quality, info.SupportsQuality);
        Assert.Equal(lossless, info.Lossless);
    }

    [Fact]
    public void JpgAndModernFormatsCarryMetadata_PngIsLimited()
    {
        Assert.True(FormatRegistry.GetOutput(OutputFormat.Jpg).SupportsMetadata);
        Assert.True(FormatRegistry.GetOutput(OutputFormat.WebP).SupportsMetadata);
        Assert.True(FormatRegistry.GetOutput(OutputFormat.Avif).SupportsMetadata);
        // PNG has no EXIF segment; we do not claim full metadata support.
        Assert.False(FormatRegistry.GetOutput(OutputFormat.Png).SupportsMetadata);
    }

    [Fact]
    public void RawAndRasterInputExtensionsAreRecognized()
    {
        Assert.True(FormatRegistry.IsSupportedInputExtension(".arw"));
        Assert.True(FormatRegistry.IsSupportedInputExtension(".CR3"));  // case-insensitive
        Assert.True(FormatRegistry.IsSupportedInputExtension(".jpg"));
        Assert.True(FormatRegistry.IsSupportedInputExtension(".heic")); // HEIC is input-only
        Assert.False(FormatRegistry.IsSupportedInputExtension(".txt"));
    }

    [Fact]
    public void ClassifiesInputExtensionCategory()
    {
        Assert.Equal(InputCategory.Raw, FormatRegistry.CategorizeExtension(".nef"));
        Assert.Equal(InputCategory.Raster, FormatRegistry.CategorizeExtension(".png"));
        Assert.Equal(InputCategory.Unsupported, FormatRegistry.CategorizeExtension(".txt"));
    }
}
