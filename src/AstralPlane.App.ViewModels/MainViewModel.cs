using System.Collections.ObjectModel;
using System.ComponentModel;
using AstralPlane.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AstralPlane.App.ViewModels;

/// <summary>How the queue is laid out in the UI.</summary>
public enum QueueViewMode
{
    Grid,
    List,
}

/// <summary>
/// Top-level view model: manages the file queue and conversion options.
/// Free of WinUI types so it can be unit-tested.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>Pixel size requested from the thumbnail provider.</summary>
    private const int ThumbnailPixelSize = 256;

    private readonly Func<string, InputCategory> _classifier;
    private readonly IThumbnailProvider? _thumbnailProvider;
    private readonly HashSet<string> _knownPaths = new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel(
        ConversionOptionsViewModel options,
        Func<string, InputCategory>? classifier = null,
        IThumbnailProvider? thumbnailProvider = null)
    {
        Options = options;
        _classifier = classifier ?? (path => FormatDetector.Detect(path).Category);
        _thumbnailProvider = thumbnailProvider;
    }

    public ConversionOptionsViewModel Options { get; }

    public ObservableCollection<QueueItemViewModel> Queue { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConvert))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListView))]
    private QueueViewMode _viewMode = QueueViewMode.Grid;

    /// <summary>Two-way view of <see cref="ViewMode"/> for a ToggleSwitch (on = List).</summary>
    public bool IsListView
    {
        get => ViewMode == QueueViewMode.List;
        set => ViewMode = value ? QueueViewMode.List : QueueViewMode.Grid;
    }

    public void ToggleViewMode() =>
        ViewMode = ViewMode == QueueViewMode.Grid ? QueueViewMode.List : QueueViewMode.Grid;

    public bool CanConvert =>
        !IsRunning && Queue.Any(i => i.Status == QueueItemStatus.Ready && i.IsSelected);

    public bool HasItems => Queue.Count > 0;

    public bool HasOutputFolder => !string.IsNullOrEmpty(LastOutputFolder);

    /// <summary>Number of selectable items currently selected for conversion.</summary>
    public int SelectedCount => Queue.Count(i => i.IsSelectable && i.IsSelected);

    /// <summary>Adds files to the queue, de-duplicating by full path and classifying each.</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            string fullPath = Path.GetFullPath(path);
            if (!_knownPaths.Add(fullPath))
                continue; // already queued

            var item = new QueueItemViewModel(fullPath, _classifier(fullPath));
            item.PropertyChanged += OnItemPropertyChanged;
            Queue.Add(item);
        }
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>Enumerates a folder (optionally recursively) and adds the files.</summary>
    public void AddFolder(string folder, bool recurse)
    {
        var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        AddFiles(Directory.EnumerateFiles(folder, "*", option));
    }

    public void Clear()
    {
        foreach (var item in Queue)
            item.PropertyChanged -= OnItemPropertyChanged;
        Queue.Clear();
        _knownPaths.Clear();
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>Selects every selectable (supported) item.</summary>
    public void SelectAll()
    {
        foreach (var item in Queue.Where(i => i.IsSelectable))
            item.IsSelected = true;
    }

    /// <summary>Deselects every item.</summary>
    public void DeselectAll()
    {
        foreach (var item in Queue)
            item.IsSelected = false;
    }

    /// <summary>Removes selected items from the queue and frees their paths for re-adding.</summary>
    public void RemoveSelected()
    {
        var toRemove = Queue.Where(i => i.IsSelected).ToList();
        foreach (var item in toRemove)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
            Queue.Remove(item);
            _knownPaths.Remove(item.SourcePath);
        }
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// Loads an item's thumbnail once via the injected provider, setting
    /// <see cref="ThumbnailState"/> to Loaded or Unavailable. A no-op if already
    /// attempted or when no provider is configured.
    /// </summary>
    public async Task EnsureThumbnailAsync(QueueItemViewModel item)
    {
        if (_thumbnailProvider is null || item.ThumbnailRequested)
            return;
        item.ThumbnailRequested = true;

        ThumbnailResult? result;
        try
        {
            result = await _thumbnailProvider.GetAsync(item.SourcePath, ThumbnailPixelSize, CancellationToken.None);
        }
        catch
        {
            result = null; // thumbnail failure never affects conversion
        }

        if (result is { Bytes.Length: > 0 })
        {
            item.Thumbnail = result.Bytes;
            item.PixelWidth = result.PixelWidth;
            item.PixelHeight = result.PixelHeight;
            item.FileSizeBytes = result.FileSizeBytes;
            item.ThumbnailState = ThumbnailState.Loaded;
        }
        else
        {
            item.ThumbnailState = ThumbnailState.Unavailable;
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QueueItemViewModel.IsSelected) or nameof(QueueItemViewModel.Status))
        {
            OnPropertyChanged(nameof(CanConvert));
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputFolder))]
    private string? _lastOutputFolder;
    [ObservableProperty] private string? _completionSummary;

    /// <summary>
    /// Converts Ready ∧ selected items using the given converter (ConversionEngine
    /// in the app; a fake in tests). Runs off the caller's thread via BatchRunner;
    /// item statuses are updated from the final result for determinism.
    /// </summary>
    public async Task ConvertAsync(IItemConverter converter, CancellationToken cancellationToken = default)
    {
        var readyItems = Queue.Where(i => i.Status == QueueItemStatus.Ready && i.IsSelected).ToList();
        if (readyItems.Count == 0)
            return;

        IsRunning = true;
        try
        {
            foreach (var item in readyItems)
                item.Status = QueueItemStatus.Converting;

            ConversionOptions options = Options.BuildOptions();
            var planner = new OutputPathPlanner(options.OutputLocation, options.ChosenFolder, TimeProvider.System);
            var runner = new BatchRunner(converter);
            var byPath = readyItems.ToDictionary(i => i.SourcePath, StringComparer.OrdinalIgnoreCase);

            var progress = new Progress<BatchProgress>(p => ApplyResult(byPath, p.Last));

            BatchResult result = await runner.RunAsync(
                readyItems.Select(i => i.SourcePath).ToList(), options, planner, progress, cancellationToken);

            foreach (var itemResult in result.Items)
                ApplyResult(byPath, itemResult);

            LastOutputFolder = result.Items.FirstOrDefault(i => i.OutputPath is not null) is { OutputPath: { } path }
                ? Path.GetDirectoryName(path)
                : null;
            CompletionSummary = $"{result.Succeeded} succeeded, {result.Failed} failed, {result.Skipped} skipped";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private static void ApplyResult(IReadOnlyDictionary<string, QueueItemViewModel> byPath, BatchItemResult result)
    {
        if (!byPath.TryGetValue(result.SourcePath, out var item))
            return;

        item.Status = result.Status switch
        {
            ItemStatus.Done => QueueItemStatus.Done,
            ItemStatus.Failed => QueueItemStatus.Failed,
            _ => QueueItemStatus.Skipped,
        };
        item.Message = result.Error;
    }
}
