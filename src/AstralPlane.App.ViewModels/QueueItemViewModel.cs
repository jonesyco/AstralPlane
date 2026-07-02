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

/// <summary>One file in the conversion queue.</summary>
public sealed partial class QueueItemViewModel : ObservableObject
{
    public QueueItemViewModel(string sourcePath, InputCategory category)
    {
        SourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
        Category = category;
        _status = category == InputCategory.Unsupported ? QueueItemStatus.Unsupported : QueueItemStatus.Ready;
    }

    public string SourcePath { get; }
    public string FileName { get; }
    public InputCategory Category { get; }

    [ObservableProperty] private QueueItemStatus _status;
    [ObservableProperty] private string? _message;
}
