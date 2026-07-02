using ImageMagick;

namespace AstralPlane.Core;

/// <summary>Maps <see cref="ConversionOptions"/> onto a <see cref="MagickImage"/> before writing.</summary>
public static class MagickEncoding
{
    public static void Apply(MagickImage image, ConversionOptions options)
    {
        OutputFormatInfo info = FormatRegistry.GetOutput(options.TargetFormat);
        image.Format = info.MagickFormat;

        bool lossless = info.Lossless switch
        {
            LosslessSupport.Always => true,
            LosslessSupport.Toggle => options.Lossless,
            _ => false, // Never
        };

        if (info.SupportsQuality)
            image.Quality = (uint)(lossless ? 100 : options.Quality);

        if (info.Lossless == LosslessSupport.Toggle && lossless)
        {
            image.Settings.SetDefine(info.MagickFormat, "lossless", "true");
            if (info.Format == OutputFormat.Avif)
                image.Settings.SetDefine(MagickFormat.Heic, "lossless", "true"); // AVIF uses the heif coder
        }
    }
}
