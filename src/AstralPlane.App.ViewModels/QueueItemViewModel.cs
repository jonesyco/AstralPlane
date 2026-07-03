using AstralPlane.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AstralPlane.App.ViewModels;

public enum QueueItemStatus
{
    Ready,
    Unsupported,
    Converting,
    Done,
    Failed,
    Skipped,
}

/// <summary>Loading state of a queue item's thumbnail.</summary>
public enum ThumbnailState
{
    Pending,
    Loaded,
    Unavailable,
}

/// <summary>One file in the conversion queue.</summary>
public sealed partial class QueueItemViewModel : ObservableObject
{
    public QueueItemViewModel(string sourcePath, InputCategory category)
    {
        SourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
        Category = category;
        // Uppercased extension without the leading dot, e.g. "CR2", "JPEG". "—" when none.
        string ext = Path.GetExtension(sourcePath).TrimStart('.');
        TypeLabel = ext.Length > 0 ? ext.ToUpperInvariant() : "—";
        _status = category == InputCategory.Unsupported ? QueueItemStatus.Unsupported : QueueItemStatus.Ready;
        // Unsupported items are never part of a batch, so they start deselected.
        _isSelected = category != InputCategory.Unsupported;
    }

    public string SourcePath { get; }
    public string FileName { get; }
    public InputCategory Category { get; }

    /// <summary>The source image's current type, e.g. "JPEG" or "CR2". Known immediately.</summary>
    public string TypeLabel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectable))]
    [NotifyPropertyChangedFor(nameof(IsUnsupported))]
    private QueueItemStatus _status;

    [ObservableProperty] private string? _message;

    /// <summary>Whether this tile is included in the next conversion batch.</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>Encoded thumbnail bytes, or null until loaded / when unavailable.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowThumbnail))]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    private byte[]? _thumbnail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowThumbnail))]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    private ThumbnailState _thumbnailState;

    /// <summary>Source image width in pixels, or null until measured.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DimensionsLabel))]
    [NotifyPropertyChangedFor(nameof(DetailsLine))]
    private int? _pixelWidth;

    /// <summary>Source image height in pixels, or null until measured.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DimensionsLabel))]
    [NotifyPropertyChangedFor(nameof(DetailsLine))]
    private int? _pixelHeight;

    /// <summary>Source file size in bytes, or null until measured.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileSizeLabel))]
    [NotifyPropertyChangedFor(nameof(DetailsLine))]
    private long? _fileSizeBytes;

    /// <summary>Load-once guard so a re-realized tile does not refetch. Not observable.</summary>
    internal bool ThumbnailRequested { get; set; }

    public bool ShowThumbnail => ThumbnailState == ThumbnailState.Loaded;
    public bool ShowPlaceholder => ThumbnailState != ThumbnailState.Loaded;
    public bool IsSelectable => Status != QueueItemStatus.Unsupported;
    public bool IsUnsupported => Status == QueueItemStatus.Unsupported;

    /// <summary>Pixel dimensions like "4000 × 3000", or empty until measured.</summary>
    public string DimensionsLabel =>
        PixelWidth is { } w && PixelHeight is { } h ? $"{w} × {h}" : string.Empty;

    /// <summary>Human-readable file size like "12.4 MB", or empty until measured.</summary>
    public string FileSizeLabel => FileSizeBytes is { } bytes ? FormatBytes(bytes) : string.Empty;

    /// <summary>Type, dimensions, and size joined for the list view, e.g. "JPEG · 4000 × 3000 · 2.1 MB".
    /// Dimensions and size are omitted until measured.</summary>
    public string DetailsLine
    {
        get
        {
            var parts = new List<string> { TypeLabel };
            if (DimensionsLabel.Length > 0) parts.Add(DimensionsLabel);
            if (FileSizeLabel.Length > 0) parts.Add(FileSizeLabel);
            return string.Join(" · ", parts);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} {units[unit]}" : $"{size:0.#} {units[unit]}";
    }
}
