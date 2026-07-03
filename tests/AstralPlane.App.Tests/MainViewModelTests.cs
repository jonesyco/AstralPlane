using AstralPlane.App.ViewModels;
using AstralPlane.Core;

namespace AstralPlane.App.Tests;

public class MainViewModelTests
{
    private sealed class FakeProbe : IFormatCapabilityProbe
    {
        public bool CanWrite(OutputFormat format) => format != OutputFormat.Heic;
        public IReadOnlyList<OutputFormat> WritableOutputs => [];
    }

    private sealed class FakeThumbnailProvider(ThumbnailResult? result) : IThumbnailProvider
    {
        public int Calls { get; private set; }
        public Task<ThumbnailResult?> GetAsync(string path, int pixelSize, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    // Classify by extension only, so no disk access is needed.
    private static MainViewModel NewVm() =>
        new(new ConversionOptionsViewModel(new FakeProbe()),
            classifier: path => FormatRegistry.CategorizeExtension(Path.GetExtension(path)));

    private static MainViewModel NewVmWith(IThumbnailProvider provider) =>
        new(new ConversionOptionsViewModel(new FakeProbe()),
            classifier: path => FormatRegistry.CategorizeExtension(Path.GetExtension(path)),
            thumbnailProvider: provider);

    [Fact]
    public void AddFilesClassifiesSupportedAndUnsupported()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\a.png", @"C:\pics\notes.txt", @"C:\pics\raw.arw"]);

        Assert.Equal(QueueItemStatus.Ready, vm.Queue.Single(i => i.FileName == "a.png").Status);
        Assert.Equal(QueueItemStatus.Unsupported, vm.Queue.Single(i => i.FileName == "notes.txt").Status);
        Assert.Equal(InputCategory.Raw, vm.Queue.Single(i => i.FileName == "raw.arw").Category);
    }

    [Fact]
    public void DeduplicatesByFullPath()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\a.png"]);
        vm.AddFiles([@"C:\pics\a.png"]); // same file again

        Assert.Single(vm.Queue);
    }

    [Fact]
    public void CanConvertRequiresAtLeastOneReadyItem()
    {
        var vm = NewVm();
        Assert.False(vm.CanConvert);

        vm.AddFiles([@"C:\pics\notes.txt"]); // unsupported only
        Assert.False(vm.CanConvert);

        vm.AddFiles([@"C:\pics\a.png"]); // now has a Ready item
        Assert.True(vm.CanConvert);
    }

    [Fact]
    public void CanConvertIsFalseWhileRunning()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\a.png"]);
        Assert.True(vm.CanConvert);

        vm.IsRunning = true;
        Assert.False(vm.CanConvert);
    }

    [Fact]
    public void ClearEmptiesTheQueue()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\a.png", @"C:\pics\b.jpg"]);
        vm.Clear();
        Assert.Empty(vm.Queue);
    }

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
    public void IsListViewMirrorsAndSetsViewMode()
    {
        var vm = NewVm();
        Assert.False(vm.IsListView);

        var raised = new List<string?>();
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsListView = true;
        Assert.Equal(QueueViewMode.List, vm.ViewMode);
        Assert.True(vm.IsListView);
        Assert.Contains(nameof(MainViewModel.IsListView), raised);

        vm.ViewMode = QueueViewMode.Grid;
        Assert.False(vm.IsListView);
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
        var provider = new FakeThumbnailProvider(new ThumbnailResult([1, 2, 3], 4000, 3000, 1_500_000));
        var vm = NewVmWith(provider);
        vm.AddFiles([@"C:\pics\a.png"]);
        var item = vm.Queue.Single();

        await vm.EnsureThumbnailAsync(item);
        Assert.Equal(ThumbnailState.Loaded, item.ThumbnailState);
        Assert.Equal([1, 2, 3], item.Thumbnail);
        Assert.Equal(4000, item.PixelWidth);
        Assert.Equal(3000, item.PixelHeight);
        Assert.Equal("4000 × 3000", item.DimensionsLabel);
        Assert.Equal("1.4 MB", item.FileSizeLabel);

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
}
