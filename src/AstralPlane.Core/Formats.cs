using ImageMagick;

namespace AstralPlane.Core;

/// <summary>Formats AstralPlane can write.</summary>
public enum OutputFormat
{
    Jpg,
    Png,
    WebP,
    Avif,
    Heic,
}

/// <summary>How a format handles lossless encoding.</summary>
public enum LosslessSupport
{
    /// <summary>Always lossy (JPEG).</summary>
    Never,
    /// <summary>Always lossless (PNG).</summary>
    Always,
    /// <summary>User can choose lossy or lossless (WebP, AVIF).</summary>
    Toggle,
}

/// <summary>Broad classification of an input file.</summary>
public enum InputCategory
{
    Unsupported,
    Raster,
    Raw,
}

/// <summary>Capabilities of an output format, driving which option controls are shown.</summary>
public sealed record OutputFormatInfo(
    OutputFormat Format,
    string FileExtension,
    MagickFormat MagickFormat,
    bool SupportsQuality,
    LosslessSupport Lossless,
    bool SupportsMetadata);
