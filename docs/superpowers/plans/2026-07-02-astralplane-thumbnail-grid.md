# Thumbnail Grid & Selection UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the AstralPlane queue's plain list with a thumbnail grid that has a grid⇄list toggle, per-tile selection driving conversion, bulk actions, and a clarified upscaling control.

**Architecture:** Three-project layering is unchanged: `AstralPlane.Core` (engine, untouched) ← `AstralPlane.App.ViewModels` (WinUI-free, unit-tested) ← `AstralPlane.App` (WinUI 3, manual-smoke). All logic that can be tested lives in the ViewModels project against an `IThumbnailProvider` abstraction faked in tests; the WinUI app supplies the real Shell-backed provider and the XAML.

**Tech Stack:** C# / .NET 10, CommunityToolkit.Mvvm 8.4.2 (`[ObservableProperty]` source generators), WinUI 3 (Windows App SDK 2.2.0), xUnit 2.9.3.

## Global Constraints

- `AstralPlane.Core` is **not modified** by any task. Upscaling already works via `ResizeSpec.DontUpscale`.
- ViewModels project (`AstralPlane.App.ViewModels`) must remain **free of any WinUI/Windows.UI type** — it targets `net10.0`, not `net10.0-windows`. `byte[]` is the thumbnail exchange type, never `BitmapImage`.
- Use CommunityToolkit.Mvvm patterns already in the codebase: `partial class : ObservableObject`, `[ObservableProperty] private T _field;`, `[NotifyPropertyChangedFor(nameof(X))]`. Generated properties are PascalCase of the field minus the leading underscore.
- Tests are xUnit `[Fact]`; the `Xunit` namespace is globally imported (see test csproj `<Using Include="Xunit" />`). Fakes are `private sealed class` nested in the test class, matching the existing `FakeProbe`/`FakeConverter` style.
- Run ViewModel tests with: `dotnet test tests/AstralPlane.App.Tests/AstralPlane.App.Tests.csproj` (project is x64/win-x64).
- Commit after each task's tests pass.

---

## File Structure

**Modified (ViewModels — tested):**
- `src/AstralPlane.App.ViewModels/QueueItemViewModel.cs` — add `ThumbnailState` enum, `IsSelected`, `Thumbnail`, `ThumbnailState`, and view-helper bools.
- `src/AstralPlane.App.ViewModels/MainViewModel.cs` — `QueueViewMode` enum, `ViewMode`, selection-driven `CanConvert`/`ConvertAsync`, `SelectAll`/`DeselectAll`/`RemoveSelected`/`SelectedCount`, `EnsureThumbnailAsync`, per-item change subscription, `IThumbnailProvider` injection.
- `src/AstralPlane.App.ViewModels/ConversionOptionsViewModel.cs` — `AllowUpscale` replacing the `DontUpscale`-only surface.

**Created (ViewModels — tested):**
- `src/AstralPlane.App.ViewModels/IThumbnailProvider.cs` — the abstraction.

**Created / modified (App — manual smoke, build-verified):**
- `src/AstralPlane.App/ShellThumbnailProvider.cs` *(new)* — `IThumbnailProvider` over `StorageFile.GetThumbnailAsync`, with throttling.
- `src/AstralPlane.App/Converters.cs` — add `BytesToImageConverter`.
- `src/AstralPlane.App/MainPage.xaml` + `.xaml.cs` — `ItemsView`, grid/list templates, toolbar actions, lazy thumbnail wiring.

**Modified (tests):**
- `tests/AstralPlane.App.Tests/QueueItemViewModelTests.cs` *(new)*
- `tests/AstralPlane.App.Tests/MainViewModelTests.cs`, `MainViewModelConvertTests.cs` — extend.
- `tests/AstralPlane.App.Tests/ConversionOptionsViewModelTests.cs` — extend.

---

## Task 1: QueueItemViewModel — selection & thumbnail state

**Files:**
- Modify: `src/AstralPlane.App.ViewModels/QueueItemViewModel.cs`
- Test: `tests/AstralPlane.App.Tests/QueueItemViewModelTests.cs` *(new)*

**Interfaces:**
- Consumes: existing `QueueItemStatus` enum, `InputCategory` (from Core).
- Produces:
  - `enum ThumbnailState { Pending, Loaded, Unavailable }`
  - `QueueItemViewModel.IsSelected` (`bool`, default **true** for supported, **false** for `Unsupported`)
  - `QueueItemViewModel.Thumbnail` (`byte[]?`)
  - `QueueItemViewModel.ThumbnailState` (`ThumbnailState`, default `Pending`)
  - `QueueItemViewModel.ShowThumbnail` (`bool` — `ThumbnailState == Loaded`)
  - `QueueItemViewModel.ShowPlaceholder` (`bool` — `ThumbnailState != Loaded`)
  - `QueueItemViewModel.IsSelectable` (`bool` — `Status != Unsupported`)
  - `QueueItemViewModel.IsUnsupported` (`bool` — `Status == Unsupported`)
  - `QueueItemViewModel.ThumbnailRequested` (`internal bool` — load-once guard, non-observable)

- [ ] **Step 1: Write the failing tests**

Create `tests/AstralPlane.App.Tests/QueueItemViewModelTests.cs`:

```csharp
using System.ComponentModel;
using AstralPlane.App.ViewModels;
using AstralPlane.Core;

namespace AstralPlane.App.Tests;

public class QueueItemViewModelTests
{
    [Fact]
    public void SupportedItemIsSelectedByDefault()
    {
        var item = new QueueItemViewModel(@"C:\pics\a.png", InputCategory.Raster);
        Assert.True(item.IsSelected);
        Assert.True(item.IsSelectable);
        Assert.False(item.IsUnsupported);
    }

    [Fact]
    public void UnsupportedItemIsNotSelectedAndNotSelectable()
    {
        var item = new QueueItemViewModel(@"C:\pics\notes.txt", InputCategory.Unsupported);
        Assert.False(item.IsSelected);
        Assert.False(item.IsSelectable);
        Assert.True(item.IsUnsupported);
    }

    [Fact]
    public void ThumbnailStateDefaultsToPendingAndDrivesVisibilityHelpers()
    {
        var item = new QueueItemViewModel(@"C:\pics\a.png", InputCategory.Raster);
        Assert.Equal(ThumbnailState.Pending, item.ThumbnailState);
        Assert.False(item.ShowThumbnail);
        Assert.True(item.ShowPlaceholder);

        item.ThumbnailState = ThumbnailState.Loaded;
        Assert.True(item.ShowThumbnail);
        Assert.False(item.ShowPlaceholder);
    }

    [Fact]
    public void TogglingIsSelectedRaisesChangeNotification()
    {
        var item = new QueueItemViewModel(@"C:\pics\a.png", InputCategory.Raster);
        var raised = new List<string?>();
        ((INotifyPropertyChanged)item).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.IsSelected = false;

        Assert.Contains(nameof(QueueItemViewModel.IsSelected), raised);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AstralPlane.App.Tests/AstralPlane.App.Tests.csproj`
Expected: FAIL — `IsSelectable`, `IsUnsupported`, `ThumbnailState`, `ShowThumbnail`, `ShowPlaceholder` do not exist (compile errors).

- [ ] **Step 3: Implement the extensions**

Replace the entire contents of `src/AstralPlane.App.ViewModels/QueueItemViewModel.cs` with:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AstralPlane.App.Tests/AstralPlane.App.Tests.csproj`
Expected: PASS (all existing tests still pass too).

- [ ] **Step 5: Commit**

```bash
git add src/AstralPlane.App.ViewModels/QueueItemViewModel.cs tests/AstralPlane.App.Tests/QueueItemViewModelTests.cs
git commit -m "feat(vm): add selection and thumbnail state to QueueItemViewModel"
```

---

## Task 2: MainViewModel — selection-driven convert, bulk actions, view mode, thumbnails

**Files:**
- Create: `src/AstralPlane.App.ViewModels/IThumbnailProvider.cs`
- Modify: `src/AstralPlane.App.ViewModels/MainViewModel.cs`
- Test: `tests/AstralPlane.App.Tests/MainViewModelTests.cs`, `tests/AstralPlane.App.Tests/MainViewModelConvertTests.cs`

**Interfaces:**
- Consumes: `QueueItemViewModel` (Task 1) incl. `IsSelected`, `IsSelectable`, `ThumbnailState`, `Thumbnail`, `ThumbnailRequested`; existing `IItemConverter`, `ConversionOptionsViewModel`.
- Produces:
  - `interface IThumbnailProvider { Task<byte[]?> GetAsync(string path, int pixelSize, CancellationToken ct); }`
  - `enum QueueViewMode { Grid, List }`
  - `MainViewModel.ViewMode` (`QueueViewMode`, default `Grid`); `MainViewModel.ToggleViewMode()`
  - `MainViewModel.SelectedCount` (`int` — selectable items with `IsSelected`)
  - `MainViewModel.SelectAll()`, `DeselectAll()`, `RemoveSelected()`
  - `MainViewModel.EnsureThumbnailAsync(QueueItemViewModel item)` (`Task`)
  - Constructor gains optional `IThumbnailProvider? thumbnailProvider = null` (existing `classifier` param preserved)
  - `CanConvert` now requires a Ready **and** selected item.

- [ ] **Step 1: Write the failing tests**

Create `src/AstralPlane.App.ViewModels/IThumbnailProvider.cs`:

```csharp
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
```

Add these tests to `tests/AstralPlane.App.Tests/MainViewModelTests.cs` (inside the existing class; add `using System.Threading;` and `using System.Threading.Tasks;` at the top if not already present):

```csharp
    private sealed class FakeThumbnailProvider(byte[]? result) : IThumbnailProvider
    {
        public int Calls { get; private set; }
        public Task<byte[]?> GetAsync(string path, int pixelSize, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private static MainViewModel NewVmWith(IThumbnailProvider provider) =>
        new(new ConversionOptionsViewModel(new FakeProbe()),
            classifier: path => FormatRegistry.CategorizeExtension(Path.GetExtension(path)),
            thumbnailProvider: provider);

    [Fact]
    public void ViewModeDefaultsToGridAndToggles()
    {
        var vm = NewVm();
        Assert.Equal(QueueViewMode.Grid, vm.ViewMode);
        vm.ToggleViewMode();
        Assert.Equal(QueueViewMode.List, vm.ViewMode);
        vm.ToggleViewMode();
        Assert.Equal(QueueViewMode.Grid, vm.ViewMode);
    }

    [Fact]
    public void CanConvertRequiresAReadyAndSelectedItem()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\a.png"]);
        Assert.True(vm.CanConvert);

        vm.Queue.Single().IsSelected = false;
        Assert.False(vm.CanConvert);

        vm.Queue.Single().IsSelected = true;
        Assert.True(vm.CanConvert);
    }

    [Fact]
    public void SelectAllAndDeselectAllAffectSelectableItemsAndCount()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\a.png", @"C:\pics\b.jpg", @"C:\pics\notes.txt"]);
        Assert.Equal(2, vm.SelectedCount); // two supported, unsupported excluded

        vm.DeselectAll();
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.CanConvert);

        vm.SelectAll();
        Assert.Equal(2, vm.SelectedCount); // unsupported stays unselectable
        Assert.True(vm.CanConvert);
    }

    [Fact]
    public void RemoveSelectedRemovesSelectedItemsAndFreesPaths()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\a.png", @"C:\pics\b.jpg"]);
        vm.Queue.Single(i => i.FileName == "b.jpg").IsSelected = false;

        vm.RemoveSelected();

        Assert.Single(vm.Queue);
        Assert.Equal("b.jpg", vm.Queue.Single().FileName);

        // a.png was removed and its path freed, so it can be re-added.
        vm.AddFiles([@"C:\pics\a.png"]);
        Assert.Equal(2, vm.Queue.Count);
    }

    [Fact]
    public async Task EnsureThumbnailAsyncSetsLoadedAndDoesNotRefetch()
    {
        var provider = new FakeThumbnailProvider([1, 2, 3]);
        var vm = NewVmWith(provider);
        vm.AddFiles([@"C:\pics\a.png"]);
        var item = vm.Queue.Single();

        await vm.EnsureThumbnailAsync(item);
        Assert.Equal(ThumbnailState.Loaded, item.ThumbnailState);
        Assert.Equal([1, 2, 3], item.Thumbnail);

        await vm.EnsureThumbnailAsync(item); // second call is a no-op
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task EnsureThumbnailAsyncSetsUnavailableWhenProviderReturnsNull()
    {
        var provider = new FakeThumbnailProvider(null);
        var vm = NewVmWith(provider);
        vm.AddFiles([@"C:\pics\a.png"]);
        var item = vm.Queue.Single();

        await vm.EnsureThumbnailAsync(item);

        Assert.Equal(ThumbnailState.Unavailable, item.ThumbnailState);
        Assert.Null(item.Thumbnail);
    }
```

Add this test to `tests/AstralPlane.App.Tests/MainViewModelConvertTests.cs` (inside the existing class):

```csharp
    [Fact]
    public async Task ConvertProcessesOnlySelectedReadyItems()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\keep.png", @"C:\pics\skip.png"]);
        vm.Queue.Single(i => i.FileName == "skip.png").IsSelected = false;

        var converted = new List<string>();
        await vm.ConvertAsync(new RecordingConverter(converted));

        Assert.Contains(@"C:\pics\keep.png", converted);
        Assert.DoesNotContain(@"C:\pics\skip.png", converted);
        // Deselected item is left untouched (still Ready), not marked Skipped/Done.
        Assert.Equal(QueueItemStatus.Ready, vm.Queue.Single(i => i.FileName == "skip.png").Status);
        Assert.Equal(QueueItemStatus.Done, vm.Queue.Single(i => i.FileName == "keep.png").Status);
    }
```

And add this fake to `MainViewModelConvertTests.cs` (nested, next to `FakeConverter`):

```csharp
    private sealed class RecordingConverter(List<string> converted) : IItemConverter
    {
        public void Convert(string source, string output, ConversionOptions options)
        {
            lock (converted) converted.Add(source);
        }
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AstralPlane.App.Tests/AstralPlane.App.Tests.csproj`
Expected: FAIL — `ViewMode`, `ToggleViewMode`, `SelectedCount`, `SelectAll`, `DeselectAll`, `RemoveSelected`, `EnsureThumbnailAsync`, and the `thumbnailProvider` constructor parameter do not exist.

- [ ] **Step 3: Implement MainViewModel changes**

Replace the entire contents of `src/AstralPlane.App.ViewModels/MainViewModel.cs` with:

```csharp
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

    [ObservableProperty] private QueueViewMode _viewMode = QueueViewMode.Grid;

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

        byte[]? bytes;
        try
        {
            bytes = await _thumbnailProvider.GetAsync(item.SourcePath, ThumbnailPixelSize, CancellationToken.None);
        }
        catch
        {
            bytes = null; // thumbnail failure never affects conversion
        }

        if (bytes is { Length: > 0 })
        {
            item.Thumbnail = bytes;
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AstralPlane.App.Tests/AstralPlane.App.Tests.csproj`
Expected: PASS (all new and existing tests).

- [ ] **Step 5: Commit**

```bash
git add src/AstralPlane.App.ViewModels/IThumbnailProvider.cs src/AstralPlane.App.ViewModels/MainViewModel.cs tests/AstralPlane.App.Tests/MainViewModelTests.cs tests/AstralPlane.App.Tests/MainViewModelConvertTests.cs
git commit -m "feat(vm): selection-driven convert, bulk actions, view mode, thumbnail loading"
```

---

## Task 3: ConversionOptionsViewModel — Allow upscaling

**Files:**
- Modify: `src/AstralPlane.App.ViewModels/ConversionOptionsViewModel.cs`
- Test: `tests/AstralPlane.App.Tests/ConversionOptionsViewModelTests.cs`

**Interfaces:**
- Consumes: existing `ResizeSpec`, `BuildOptions()`.
- Produces: `ConversionOptionsViewModel.AllowUpscale` (`bool`, default **false**). `DontUpscale` field is removed; `BuildResize()` uses `!AllowUpscale`.

- [ ] **Step 1: Update the existing failing test and add a new one**

In `tests/AstralPlane.App.Tests/ConversionOptionsViewModelTests.cs`, replace the `BuildOptionsMapsLongEdgeResize` test with:

```csharp
    [Fact]
    public void AllowUpscaleDefaultsFalseSoResizeClampsEnlargement()
    {
        var vm = NewVm();
        Assert.False(vm.AllowUpscale);

        vm.SelectedFormat = OutputFormat.Jpg;
        vm.ResizeMode = ResizeMode.LongEdge;
        vm.ResizeLongEdge = 1200;

        var resize = vm.BuildOptions().Resize;
        Assert.Equal(ResizeMode.LongEdge, resize.Mode);
        Assert.Equal(1200, resize.Width);
        Assert.True(resize.DontUpscale); // AllowUpscale=false => DontUpscale=true
    }

    [Fact]
    public void AllowUpscaleMapsToInverseOfDontUpscale()
    {
        var vm = NewVm();
        vm.ResizeMode = ResizeMode.LongEdge;
        vm.ResizeLongEdge = 1200;

        vm.AllowUpscale = true;
        Assert.False(vm.BuildOptions().Resize.DontUpscale);

        vm.AllowUpscale = false;
        Assert.True(vm.BuildOptions().Resize.DontUpscale);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AstralPlane.App.Tests/AstralPlane.App.Tests.csproj`
Expected: FAIL — `AllowUpscale` does not exist.

- [ ] **Step 3: Implement the property**

In `src/AstralPlane.App.ViewModels/ConversionOptionsViewModel.cs`, replace this line:

```csharp
    [ObservableProperty] private bool _dontUpscale = true;
```

with:

```csharp
    // Reframed control: default off (clamp enlargement), behaviourally identical
    // to the previous "Don't upscale" default. Maps to ResizeSpec.DontUpscale = !AllowUpscale.
    [ObservableProperty] private bool _allowUpscale;
```

Then in the same file, replace the `BuildResize()` method body's `DontUpscale` references:

```csharp
    private ResizeSpec BuildResize() => ResizeMode switch
    {
        ResizeMode.LongEdge => ResizeSpec.LongEdge(ResizeLongEdge, !AllowUpscale),
        ResizeMode.Percentage => ResizeSpec.Percentage(ResizePercent, !AllowUpscale),
        ResizeMode.Box => ResizeSpec.Box(ResizeWidth, ResizeHeight, !AllowUpscale),
        _ => ResizeSpec.None,
    };
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AstralPlane.App.Tests/AstralPlane.App.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AstralPlane.App.ViewModels/ConversionOptionsViewModel.cs tests/AstralPlane.App.Tests/ConversionOptionsViewModelTests.cs
git commit -m "feat(vm): reframe upscaling as AllowUpscale (default off)"
```

---

## Task 4: App — ShellThumbnailProvider and BytesToImageConverter

**Files:**
- Create: `src/AstralPlane.App/ShellThumbnailProvider.cs`
- Modify: `src/AstralPlane.App/Converters.cs`

**Interfaces:**
- Consumes: `IThumbnailProvider` (Task 2).
- Produces: `ShellThumbnailProvider : IThumbnailProvider`; `BytesToImageConverter : IValueConverter` (`byte[]` → `BitmapImage`).

*No unit tests — these depend on WinUI/WinRT types not referenced by the test project. Verified by build and manual smoke in Task 5.*

- [ ] **Step 1: Create the Shell thumbnail provider**

Create `src/AstralPlane.App/ShellThumbnailProvider.cs`:

```csharp
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
```

- [ ] **Step 2: Add the bytes→image converter**

In `src/AstralPlane.App/Converters.cs`, add these using directives at the top (below the existing `using` lines):

```csharp
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
```

Then append this class before the closing of the file (after `InverseBoolConverter`):

```csharp
/// <summary>Maps encoded thumbnail bytes to a BitmapImage for an Image source. Null → null.</summary>
public sealed class BytesToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
            return null;

        var image = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            writer.StoreAsync().GetAwaiter().GetResult();
            writer.FlushAsync().GetAwaiter().GetResult();
            writer.DetachStream();
        }
        stream.Seek(0);
        image.SetSource(stream);
        return image;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
```

Add the WinRT streams using needed for `DataWriter`/`InMemoryRandomAccessStream` at the top of `Converters.cs`:

```csharp
using Windows.Storage.Streams;
```

- [ ] **Step 3: Build the app project**

Run: `dotnet build src/AstralPlane.App/AstralPlane.App.csproj -c Debug`
Expected: BUILD SUCCEEDED (0 errors). Warnings acceptable.

- [ ] **Step 4: Commit**

```bash
git add src/AstralPlane.App/ShellThumbnailProvider.cs src/AstralPlane.App/Converters.cs
git commit -m "feat(app): Shell thumbnail provider and bytes-to-image converter"
```

---

## Task 5: App — MainPage thumbnail grid, view toggle, selection & bulk actions

**Files:**
- Modify: `src/AstralPlane.App/MainPage.xaml`
- Modify: `src/AstralPlane.App/MainPage.xaml.cs`

**Interfaces:**
- Consumes: `MainViewModel` (`ViewMode`, `ToggleViewMode`, `SelectAll`, `DeselectAll`, `RemoveSelected`, `SelectedCount`, `EnsureThumbnailAsync`); `QueueItemViewModel` (`IsSelected`, `ShowThumbnail`, `ShowPlaceholder`, `IsSelectable`, `IsUnsupported`, `Thumbnail`, `FileName`, `Status`, `Message`); `ShellThumbnailProvider`; `BytesToImageConverter`.
- Produces: the updated queue UI. Manual smoke only.

- [ ] **Step 1: Inject the thumbnail provider into the view model**

In `src/AstralPlane.App/MainPage.xaml.cs`, replace the `ViewModel` initializer:

```csharp
    public MainViewModel ViewModel { get; } =
        new(new ConversionOptionsViewModel(new MagickFormatCapabilityProbe()));
```

with:

```csharp
    public MainViewModel ViewModel { get; } =
        new(new ConversionOptionsViewModel(new MagickFormatCapabilityProbe()),
            thumbnailProvider: new ShellThumbnailProvider());
```

- [ ] **Step 2: Add toolbar + queue event handlers to code-behind**

In `src/AstralPlane.App/MainPage.xaml.cs`, add these handlers inside the `MainPage` class (after `Clear_Click`):

```csharp
    private void ToggleView_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleViewMode();

    private void SelectAll_Click(object sender, RoutedEventArgs e) => ViewModel.SelectAll();

    private void DeselectAll_Click(object sender, RoutedEventArgs e) => ViewModel.DeselectAll();

    private void RemoveSelected_Click(object sender, RoutedEventArgs e) => ViewModel.RemoveSelected();

    // Lazy, per-tile thumbnail load: fires as a tile is realized by the virtualized
    // ItemsView. EnsureThumbnailAsync is a no-op after the first attempt per item.
    private async void Tile_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QueueItemViewModel item })
            await ViewModel.EnsureThumbnailAsync(item);
    }
```

- [ ] **Step 3: Replace the queue region and toolbar in MainPage.xaml**

In `src/AstralPlane.App/MainPage.xaml`, add the converter to `Page.Resources` (inside the existing `<Page.Resources>` block):

```xml
        <local:BytesToImageConverter x:Key="BytesToImage" />
```

Replace the entire toolbar `StackPanel` (the `<!-- Toolbar -->` block, `Grid.Row="0" Grid.Column="0"`) with:

```xml
        <!-- Toolbar -->
        <StackPanel Grid.Row="0" Grid.Column="0" Orientation="Horizontal" Spacing="8">
            <Button Content="Add files…" Click="AddFiles_Click"
                    IsEnabled="{x:Bind ViewModel.IsRunning, Mode=OneWay, Converter={StaticResource InverseBool}}" />
            <Button Content="Add folder…" Click="AddFolder_Click"
                    IsEnabled="{x:Bind ViewModel.IsRunning, Mode=OneWay, Converter={StaticResource InverseBool}}" />
            <AppBarSeparator />
            <Button Content="Select all" Click="SelectAll_Click"
                    IsEnabled="{x:Bind ViewModel.IsRunning, Mode=OneWay, Converter={StaticResource InverseBool}}" />
            <Button Content="Deselect all" Click="DeselectAll_Click"
                    IsEnabled="{x:Bind ViewModel.IsRunning, Mode=OneWay, Converter={StaticResource InverseBool}}" />
            <Button Content="Remove selected" Click="RemoveSelected_Click"
                    IsEnabled="{x:Bind ViewModel.IsRunning, Mode=OneWay, Converter={StaticResource InverseBool}}" />
            <AppBarSeparator />
            <ToggleButton Content="Grid / List" Click="ToggleView_Click"
                          IsEnabled="{x:Bind ViewModel.IsRunning, Mode=OneWay, Converter={StaticResource InverseBool}}" />
            <Button Content="Clear" Click="Clear_Click"
                    IsEnabled="{x:Bind ViewModel.IsRunning, Mode=OneWay, Converter={StaticResource InverseBool}}" />
        </StackPanel>
```

Replace the entire queue `<Border ...>` block (the `<!-- Queue + drop zone -->` block containing the old `ListView`) with:

```xml
        <!-- Queue + drop zone -->
        <Border Grid.Row="1" Grid.Column="0"
                AllowDrop="True" DragOver="Queue_DragOver" Drop="Queue_Drop"
                BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}" BorderThickness="1"
                CornerRadius="8" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}">
            <Grid>
                <TextBlock Text="Drop images or folders here, or use “Add files…”."
                           HorizontalAlignment="Center" VerticalAlignment="Center"
                           Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                           Visibility="{x:Bind ViewModel.HasItems, Mode=OneWay, Converter={StaticResource BoolToVisibility}, ConverterParameter=invert}" />

                <ScrollViewer Padding="4">
                    <ItemsRepeater x:Name="QueueRepeater"
                                   ItemsSource="{x:Bind ViewModel.Queue, Mode=OneWay}">
                        <ItemsRepeater.Layout>
                            <UniformGridLayout MinItemWidth="180" MinItemHeight="200"
                                               MinColumnSpacing="8" MinRowSpacing="8" />
                        </ItemsRepeater.Layout>
                        <ItemsRepeater.ItemTemplate>
                            <DataTemplate x:DataType="vm:QueueItemViewModel">
                                <Grid Loaded="Tile_Loaded" Width="176" Padding="8" RowSpacing="6"
                                      CornerRadius="6"
                                      Background="{ThemeResource CardBackgroundFillColorSecondaryBrush}"
                                      Opacity="{x:Bind IsUnsupported, Converter={StaticResource UnsupportedToOpacity}}">
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height="140" />
                                        <RowDefinition Height="Auto" />
                                        <RowDefinition Height="Auto" />
                                    </Grid.RowDefinitions>

                                    <!-- Thumbnail or placeholder -->
                                    <Grid Grid.Row="0">
                                        <Image Stretch="Uniform"
                                               Source="{x:Bind Thumbnail, Mode=OneWay, Converter={StaticResource BytesToImage}}"
                                               Visibility="{x:Bind ShowThumbnail, Mode=OneWay, Converter={StaticResource BoolToVisibility}}" />
                                        <FontIcon Glyph="&#xE91B;" FontSize="40"
                                                  HorizontalAlignment="Center" VerticalAlignment="Center"
                                                  Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                                                  Visibility="{x:Bind ShowPlaceholder, Mode=OneWay, Converter={StaticResource BoolToVisibility}}" />
                                        <CheckBox HorizontalAlignment="Right" VerticalAlignment="Top"
                                                  IsChecked="{x:Bind IsSelected, Mode=TwoWay}"
                                                  Visibility="{x:Bind IsSelectable, Converter={StaticResource BoolToVisibility}}" />
                                    </Grid>

                                    <TextBlock Grid.Row="1" Text="{x:Bind FileName}"
                                               TextTrimming="CharacterEllipsis" MaxLines="1" />
                                    <TextBlock Grid.Row="2" Text="{x:Bind Status, Mode=OneWay}"
                                               FontSize="12"
                                               Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                                </Grid>
                            </DataTemplate>
                        </ItemsRepeater.ItemTemplate>
                    </ItemsRepeater>
                </ScrollViewer>
            </Grid>
        </Border>
```

Add the two converters used above to `Page.Resources` — `UnsupportedToOpacity`. Add to `<Page.Resources>`:

```xml
        <local:BoolToOpacityConverter x:Key="UnsupportedToOpacity" />
```

- [ ] **Step 4: Add the layout-swap on ViewMode and the opacity converter**

The `ItemsRepeater.Layout` is switched in code-behind (a converter cannot cleanly return `AttachedLayout` instances with correct settings). In `src/AstralPlane.App/MainPage.xaml.cs`, add to the constructor after `InitializeComponent();`:

```csharp
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ViewMode))
                ApplyLayout();
        };
        ApplyLayout();
```

`UniformGridLayout` and `StackLayout` live in `Microsoft.UI.Xaml.Controls`, which `MainPage.xaml.cs` already imports; `QueueViewMode` lives in `AstralPlane.App.ViewModels`, also already imported. No new usings are needed. Add this method to the `MainPage` class:

```csharp
    private void ApplyLayout()
    {
        QueueRepeater.Layout = ViewModel.ViewMode == QueueViewMode.Grid
            ? new UniformGridLayout
            {
                MinItemWidth = 180,
                MinItemHeight = 200,
                MinColumnSpacing = 8,
                MinRowSpacing = 8,
            }
            : new StackLayout { Spacing = 4 };
    }
```

Add the opacity converter to `src/AstralPlane.App/Converters.cs`:

```csharp
/// <summary>Maps a bool to opacity: true → dimmed (0.5), false → full (1.0). Used to dim unsupported tiles.</summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b && b ? 0.5 : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 5: Update the resize option label and add the Allow-upscaling checkbox in the options panel**

In `src/AstralPlane.App/MainPage.xaml`, replace this line in the Resize section:

```xml
                <CheckBox Content="Don't upscale"
                          IsChecked="{x:Bind ViewModel.Options.DontUpscale, Mode=TwoWay}" />
```

with:

```xml
                <CheckBox Content="Allow upscaling"
                          IsChecked="{x:Bind ViewModel.Options.AllowUpscale, Mode=TwoWay}" />
```

- [ ] **Step 6: Build the app**

Run: `dotnet build src/AstralPlane.App/AstralPlane.App.csproj -c Debug`
Expected: BUILD SUCCEEDED (0 errors). Fix any XAML/x:Bind compile errors (e.g. missing converter keys, wrong member names) before proceeding.

- [ ] **Step 7: Manual smoke test**

Run the app (`dotnet run --project src/AstralPlane.App/AstralPlane.App.csproj` or launch the built exe) and verify:
- Grid renders thumbnail tiles for added images; a placeholder glyph shows for files with no available thumbnail, and those files still convert.
- The Grid/List toggle switches the layout and preserves selection and loaded thumbnails.
- Deselecting a tile excludes it from the output; Select all / Deselect all / Remove selected behave as described; a removed file can be re-added.
- Unsupported files are dimmed with no checkbox and are never converted.
- The Resize control reads "Allow upscaling" and is unchecked by default.

- [ ] **Step 8: Commit**

```bash
git add src/AstralPlane.App/MainPage.xaml src/AstralPlane.App/MainPage.xaml.cs src/AstralPlane.App/Converters.cs
git commit -m "feat(app): thumbnail grid UI, view toggle, selection and bulk actions"
```

---

## Self-Review Notes

- **Spec coverage:** thumbnail tiles (Task 5), grid/list toggle (Tasks 2+5), selection-drives-convert (Task 2), bulk actions (Task 2+5), upscaling reframe (Task 3), `IThumbnailProvider` abstraction + fake (Task 2), Shell provider (Task 4), lazy per-tile load + throttle (Tasks 4+5), unsupported dimming/non-selectable (Tasks 1+5), thumbnail failure never blocks conversion (Task 2 `EnsureThumbnailAsync` catch + Task 4 provider catch). All spec "Testing Strategy" view-model bullets map to tests in Tasks 1–3.
- **ItemsView vs ItemsRepeater:** the plan uses `ItemsRepeater` (inside a `ScrollViewer`) rather than `ItemsView`. `ItemsRepeater` is the mature, well-documented virtualizing primitive with swappable `Layout` and per-element `Loaded` events — this is exactly the spec's Risk #2 low-cost fallback (selection lives on the view model, so the control choice is immaterial to behavior). If a reviewer requires `ItemsView` specifically, the tile template and layout-swap transfer directly.
- **Type consistency:** `EnsureThumbnailAsync`, `SelectedCount`, `ToggleViewMode`, `AllowUpscale`, `ShowThumbnail`/`ShowPlaceholder`/`IsSelectable`/`IsUnsupported` are named identically across producer tasks and their consumers in Task 5.
