using AstralPlane.App.ViewModels;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace AstralPlane_App;

/// <summary>
/// <see cref="IThumbnailProvider"/> backed by the Windows Shell
/// (<see cref="StorageFile.GetThumbnailAsync"/>) — the same thumbnails Explorer
/// shows. Correctly oriented, OS-cached, and uniform across RAW/HEIC/raster.
/// Returns null when no thumbnail is available (missing codec, unreadable file,
/// or any error); thumbnail failure never affects conversion. Concurrency is
/// throttled so large batches do not storm the shell.
/// </summary>
public sealed class ShellThumbnailProvider : IThumbnailProvider
{
    private readonly SemaphoreSlim _throttle = new(4);

    public async Task<byte[]?> GetAsync(string path, int pixelSize, CancellationToken ct)
    {
        await _throttle.WaitAsync(ct);
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(path);
            using StorageItemThumbnail? thumb = await file.GetThumbnailAsync(
                ThumbnailMode.SingleItem, (uint)pixelSize, ThumbnailOptions.ResizeThumbnail);

            if (thumb is null || thumb.Size == 0)
                return null;

            using var reader = new DataReader(thumb);
            uint size = (uint)thumb.Size;
            await reader.LoadAsync(size);
            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
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
}
