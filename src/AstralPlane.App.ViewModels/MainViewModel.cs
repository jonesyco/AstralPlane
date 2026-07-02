using System.Collections.ObjectModel;
using AstralPlane.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AstralPlane.App.ViewModels;

/// <summary>
/// Top-level view model: manages the file queue and conversion options.
/// Free of WinUI types so it can be unit-tested.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly Func<string, InputCategory> _classifier;
    private readonly HashSet<string> _knownPaths = new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel(ConversionOptionsViewModel options, Func<string, InputCategory>? classifier = null)
    {
        Options = options;
        _classifier = classifier ?? (path => FormatDetector.Detect(path).Category);
    }

    public ConversionOptionsViewModel Options { get; }

    public ObservableCollection<QueueItemViewModel> Queue { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConvert))]
    private bool _isRunning;

    public bool CanConvert => !IsRunning && Queue.Any(i => i.Status == QueueItemStatus.Ready);

    public bool HasItems => Queue.Count > 0;

    public bool HasOutputFolder => !string.IsNullOrEmpty(LastOutputFolder);

    /// <summary>Adds files to the queue, de-duplicating by full path and classifying each.</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            string fullPath = Path.GetFullPath(path);
            if (!_knownPaths.Add(fullPath))
                continue; // already queued

            Queue.Add(new QueueItemViewModel(fullPath, _classifier(fullPath)));
        }
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(HasItems));
    }

    /// <summary>Enumerates a folder (optionally recursively) and adds the files.</summary>
    public void AddFolder(string folder, bool recurse)
    {
        var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        AddFiles(Directory.EnumerateFiles(folder, "*", option));
    }

    public void Clear()
    {
        Queue.Clear();
        _knownPaths.Clear();
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(HasItems));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputFolder))]
    private string? _lastOutputFolder;
    [ObservableProperty] private string? _completionSummary;

    /// <summary>
    /// Converts all Ready items using the given converter (ConversionEngine in
    /// the app; a fake in tests). Runs off the caller's thread via BatchRunner;
    /// item statuses are updated from the final result for determinism.
    /// </summary>
    public async Task ConvertAsync(IItemConverter converter, CancellationToken cancellationToken = default)
    {
        var readyItems = Queue.Where(i => i.Status == QueueItemStatus.Ready).ToList();
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
