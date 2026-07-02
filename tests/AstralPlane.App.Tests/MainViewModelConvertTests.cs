using AstralPlane.App.ViewModels;
using AstralPlane.Core;

namespace AstralPlane.App.Tests;

public class MainViewModelConvertTests
{
    private sealed class FakeProbe : IFormatCapabilityProbe
    {
        public bool CanWrite(OutputFormat format) => format != OutputFormat.Heic;
        public IReadOnlyList<OutputFormat> WritableOutputs => [];
    }

    private sealed class FakeConverter(Func<string, bool> failFor) : IItemConverter
    {
        public void Convert(string source, string output, ConversionOptions options)
        {
            if (failFor(source)) throw new InvalidOperationException("boom");
        }
    }

    private static MainViewModel NewVm()
    {
        var options = new ConversionOptionsViewModel(new FakeProbe()); // SameAsSource by default
        return new MainViewModel(options, classifier: p => FormatRegistry.CategorizeExtension(Path.GetExtension(p)));
    }

    [Fact]
    public async Task MarksItemsDoneAndFailedAndClearsRunning()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\good.png", @"C:\pics\bad.png", @"C:\pics\notes.txt"]);

        await vm.ConvertAsync(new FakeConverter(src => src.EndsWith("bad.png")));

        Assert.Equal(QueueItemStatus.Done, vm.Queue.Single(i => i.FileName == "good.png").Status);
        Assert.Equal(QueueItemStatus.Failed, vm.Queue.Single(i => i.FileName == "bad.png").Status);
        // Unsupported items are never converted.
        Assert.Equal(QueueItemStatus.Unsupported, vm.Queue.Single(i => i.FileName == "notes.txt").Status);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task FailedItemCarriesErrorMessage()
    {
        var vm = NewVm();
        vm.AddFiles([@"C:\pics\bad.png"]);

        await vm.ConvertAsync(new FakeConverter(_ => true));

        Assert.Contains("boom", vm.Queue.Single().Message);
    }
}
