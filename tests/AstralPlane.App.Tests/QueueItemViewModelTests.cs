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
