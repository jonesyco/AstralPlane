using AstralPlane.Core;
using ImageMagick;

namespace AstralPlane.Core.Tests;

public class FormatCapabilityProbeTests
{
    private readonly IFormatCapabilityProbe _probe = new MagickFormatCapabilityProbe();

    [Fact]
    public void AvifIsWritable_ItIsOurRequiredModernFormat()
    {
        Assert.True(_probe.CanWrite(OutputFormat.Avif));
    }

    [Theory]
    [InlineData(OutputFormat.Jpg)]
    [InlineData(OutputFormat.Png)]
    [InlineData(OutputFormat.WebP)]
    public void CoreRasterFormatsAreWritable(OutputFormat format)
    {
        Assert.True(_probe.CanWrite(format));
    }

    [Fact]
    public void HeicOutputIsUnavailableInThisBuild()
    {
        // The shipped Magick.NET Windows build reads HEIC but cannot encode it.
        // The probe surfaces this so the UI can disable HEIC output.
        Assert.False(_probe.CanWrite(OutputFormat.Heic));
    }

    [Fact]
    public void ProbeAgreesWithUnderlyingMagickNet()
    {
        foreach (var info in FormatRegistry.OutputFormats)
        {
            bool expected = MagickFormatInfo.Create(info.MagickFormat)?.SupportsWriting ?? false;
            Assert.Equal(expected, _probe.CanWrite(info.Format));
        }
    }
}
