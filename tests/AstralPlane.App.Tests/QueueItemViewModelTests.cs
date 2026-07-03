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
    public void TypeLabelIsUppercasedExtension()
    {
        Assert.Equal("PNG", new QueueItemViewModel(@"C:\pics\a.png", InputCategory.Raster).TypeLabel);
        Assert.Equal("CR2", new QueueItemViewModel(@"C:\pics\shot.CR2", InputCategory.Raw).TypeLabel);
        Assert.Equal("—", new QueueItemViewModel(@"C:\pics\noext", InputCategory.Unsupported).TypeLabel);
    }

    [Fact]
    public void DimensionAndSizeLabelsAreEmptyUntilMeasured()
    {
        var item = new QueueItemViewModel(@"C:\pics\a.png", InputCategory.Raster);
        Assert.Equal(string.Empty, item.DimensionsLabel);
        Assert.Equal(string.Empty, item.FileSizeLabel);

        item.PixelWidth = 1920;
        item.PixelHeight = 1080;
        item.FileSizeBytes = 840 * 1024;
        Assert.Equal("1920 × 1080", item.DimensionsLabel);
        Assert.Equal("840 KB", item.FileSizeLabel);
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
