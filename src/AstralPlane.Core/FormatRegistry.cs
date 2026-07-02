using ImageMagick;

namespace AstralPlane.Core;

/// <summary>
/// Authoritative list of supported input/output formats and their capabilities.
/// Pure data — actual encoder availability is verified separately by
/// <see cref="FormatCapabilityProbe"/>.
/// </summary>
public static class FormatRegistry
{
    private static readonly OutputFormatInfo[] _outputs =
    [
        new(OutputFormat.Jpg,  ".jpg",  MagickFormat.Jpeg, SupportsQuality: true,  LosslessSupport.Never,  SupportsMetadata: true),
        new(OutputFormat.Png,  ".png",  MagickFormat.Png,  SupportsQuality: false, LosslessSupport.Always, SupportsMetadata: false),
        new(OutputFormat.WebP, ".webp", MagickFormat.WebP, SupportsQuality: true,  LosslessSupport.Toggle, SupportsMetadata: true),
        new(OutputFormat.Avif, ".avif", MagickFormat.Avif, SupportsQuality: true,  LosslessSupport.Toggle, SupportsMetadata: true),
        new(OutputFormat.Heic, ".heic", MagickFormat.Heic, SupportsQuality: true,  LosslessSupport.Never,  SupportsMetadata: true),
    ];

    // Standard raster formats accepted as input.
    private static readonly HashSet<string> _rasterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".bmp", ".gif", ".heic", ".heif", ".avif",
    };

    // Camera RAW families accepted as input (developed via Magick.NET).
    private static readonly HashSet<string> _rawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".arw", ".cr2", ".cr3", ".nef", ".dng", ".orf", ".raf", ".rw2",
        ".pef", ".srw", ".raw", ".rwl", ".nrw", ".sr2",
    };

    public static IReadOnlyList<OutputFormatInfo> OutputFormats => _outputs;

    public static OutputFormatInfo GetOutput(OutputFormat format) =>
        _outputs.First(f => f.Format == format);

    public static bool IsSupportedInputExtension(string extension) =>
        CategorizeExtension(extension) != InputCategory.Unsupported;

    public static InputCategory CategorizeExtension(string extension)
    {
        if (_rawExtensions.Contains(extension)) return InputCategory.Raw;
        if (_rasterExtensions.Contains(extension)) return InputCategory.Raster;
        return InputCategory.Unsupported;
    }
}
