using AstralPlane.App.ViewModels;
using ImageMagick;

namespace AstralPlane_App;

/// <summary>
/// <see cref="IThumbnailProvider"/> that renders thumbnails with Magick.NET — the same
/// engine used for conversion — so every supported input (RAW, HEIC, AVIF, and ordinary
/// raster) produces a real preview rather than relying on OS shell codecs. The single
/// decode also yields the source's original pixel dimensions and file size. Returns null
/// when a file cannot be decoded; thumbnail failure never affects conversion. Concurrency
/// is throttled so large batches do not storm the decoder.
/// </summary>
public sealed class MagickThumbnailProvider : IThumbnailProvider
{
    private readonly SemaphoreSlim _throttle = new(4);

    public async Task<ThumbnailResult?> GetAsync(string path, int pixelSize, CancellationToken ct)
    {
        await _throttle.WaitAsync(ct);
        try
        {
            return await Task.Run(() => Render(path, pixelSize), ct);
        }
        catch
        {
            return null;
        }
        finally
        {
            _throttle.Release();
        }
    }

    private static ThumbnailResult? Render(string path, int pixelSize)
    {
        var settings = new MagickReadSettings();
        // Develop RAW with the camera's white balance, matching MagickInputLoader.
        settings.SetDefine(MagickFormat.Dng, "use-camera-wb", "true");

        using var image = new MagickImage(path, settings);
        image.AutoOrient(); // honor EXIF orientation before measuring/resizing

        int width = (int)image.Width;
        int height = (int)image.Height;

        image.Thumbnail(new MagickGeometry((uint)pixelSize, (uint)pixelSize));
        image.Strip();
        image.Format = MagickFormat.Png;
        byte[] bytes = image.ToByteArray();

        long fileSize = new FileInfo(path).Length;
        return new ThumbnailResult(bytes, width, height, fileSize);
    }
}
