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
        _status = category == InputCategory.Unsupported ? QueueItemStatus.Unsupported : QueueItemStatus.Ready;
        // Unsupported items are never part of a batch, so they start deselected.
        _isSelected = category != InputCategory.Unsupported;
    }

    public string SourcePath { get; }
    public string FileName { get; }
    public InputCategory Category { get; }

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

    /// <summary>Load-once guard so a re-realized tile does not refetch. Not observable.</summary>
    internal bool ThumbnailRequested { get; set; }

    public bool ShowThumbnail => ThumbnailState == ThumbnailState.Loaded;
    public bool ShowPlaceholder => ThumbnailState != ThumbnailState.Loaded;
    public bool IsSelectable => Status != QueueItemStatus.Unsupported;
    public bool IsUnsupported => Status == QueueItemStatus.Unsupported;
}
