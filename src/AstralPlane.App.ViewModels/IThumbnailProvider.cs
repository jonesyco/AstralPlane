namespace AstralPlane.App.ViewModels;

/// <summary>
/// A rendered thumbnail plus the source image's measured dimensions and file size,
/// all produced in a single decode.
/// </summary>
public sealed record ThumbnailResult(byte[] Bytes, int PixelWidth, int PixelHeight, long FileSizeBytes);

/// <summary>
/// Supplies a rendered thumbnail (and source measurements) for a file path. The app
/// wires a Magick.NET-backed implementation; tests use a fake. Returns null when the
/// file cannot be decoded (unreadable file, unsupported content, or error).
/// </summary>
public interface IThumbnailProvider
{
    Task<ThumbnailResult?> GetAsync(string path, int pixelSize, CancellationToken ct);
}
