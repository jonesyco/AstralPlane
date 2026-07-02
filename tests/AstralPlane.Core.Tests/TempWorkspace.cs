using ImageMagick;

namespace AstralPlane.Core.Tests;

/// <summary>
/// A disposable temp directory with helpers to generate small fixture images
/// programmatically, so tests need no committed binary blobs.
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    public string Root { get; }

    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "astralplane-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string PathFor(string relative) => System.IO.Path.Combine(Root, relative);

    /// <summary>Writes a solid-color image of the given size and format; returns its path.</summary>
    public string CreateImage(string fileName, MagickFormat format, uint width = 32, uint height = 24)
    {
        string path = PathFor(fileName);
        using var image = new MagickImage(MagickColors.CornflowerBlue, width, height);
        image.Write(path, format);
        return path;
    }

    /// <summary>Writes a two-frame animated GIF; returns its path.</summary>
    public string CreateAnimatedGif(string fileName, uint width = 16, uint height = 16)
    {
        string path = PathFor(fileName);
        using var frames = new MagickImageCollection
        {
            new MagickImage(MagickColors.Red, width, height),
            new MagickImage(MagickColors.Green, width, height),
        };
        foreach (var f in frames) f.AnimationDelay = 50;
        frames.Write(path, MagickFormat.Gif);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
