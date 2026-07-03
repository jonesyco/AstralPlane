namespace AstralPlane.App.ViewModels;

/// <summary>
/// Supplies encoded thumbnail bytes for a file path. The app wires a Windows
/// Shell-backed implementation; tests use a fake. Returns null when no
/// thumbnail is available (missing codec, unreadable file, or error).
/// </summary>
public interface IThumbnailProvider
{
    Task<byte[]?> GetAsync(string path, int pixelSize, CancellationToken ct);
}
