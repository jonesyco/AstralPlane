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

    // Classify by extension only, so no disk access is needed.
    private static MainViewModel NewVm() =>
        new(new ConversionOptionsViewModel(new FakeProbe()),
            classifier: path => FormatRegistry.CategorizeExtension(Path.GetExtension(path)));

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
}
