using ImageMagick;

namespace AstralPlane.Core;

/// <summary>Turns a source file into an in-memory image.</summary>
public interface IInputLoader
{
    /// <summary>Loads the image. Throws if the file cannot be read/decoded.</summary>
    MagickImage Load(string path);
}

/// <summary>
/// Loads any supported input via Magick.NET. Standard raster formats read
/// directly; RAW files are developed to full resolution using camera white
/// balance (per Spike 1 — embedded previews are not full-size). Animated GIFs
/// yield their first frame only, since a single <see cref="MagickImage"/> holds
/// one frame.
/// </summary>
public sealed class MagickInputLoader : IInputLoader
{
    public MagickImage Load(string path)
    {
        var settings = new MagickReadSettings();
        // Develop RAW with the camera's white balance for natural colors.
        settings.SetDefine(MagickFormat.Dng, "use-camera-wb", "true");
        return new MagickImage(path, settings);
    }
}
