using AstralPlane.App.ViewModels;
using AstralPlane.Core;

namespace AstralPlane.App.Tests;

public class ConversionOptionsViewModelTests
{
    // Test probe: everything writable except HEIC (matches the real build).
    private sealed class FakeProbe : IFormatCapabilityProbe
    {
        public bool CanWrite(OutputFormat format) => format != OutputFormat.Heic;
        public IReadOnlyList<OutputFormat> WritableOutputs =>
            FormatRegistry.OutputFormats.Select(f => f.Format).Where(CanWrite).ToList();
    }

    private static ConversionOptionsViewModel NewVm() => new(new FakeProbe());

    [Fact]
    public void HeicOptionIsDisabledWithReason_AvifEnabled()
    {
        var vm = NewVm();
        var heic = vm.Formats.Single(f => f.Format == OutputFormat.Heic);
        var avif = vm.Formats.Single(f => f.Format == OutputFormat.Avif);

        Assert.False(heic.IsEnabled);
        Assert.False(string.IsNullOrWhiteSpace(heic.DisabledReason));
        Assert.True(avif.IsEnabled);
    }

    [Fact]
    public void DefaultSelectionIsEnabledAndShowsQuality()
    {
        var vm = NewVm();
        Assert.True(vm.Formats.Single(f => f.Format == vm.SelectedFormat).IsEnabled);
        Assert.Equal(OutputFormat.Jpg, vm.SelectedFormat);
        Assert.True(vm.ShowQuality);
        Assert.False(vm.ShowLossless);
    }

    [Fact]
    public void PngHidesQualityAndLossless()
    {
        var vm = NewVm();
        vm.SelectedFormat = OutputFormat.Png;
        Assert.False(vm.ShowQuality);
        Assert.False(vm.ShowLossless);
    }

    [Fact]
    public void WebPShowsQualityAndLosslessToggle()
    {
        var vm = NewVm();
        vm.SelectedFormat = OutputFormat.WebP;
        Assert.True(vm.ShowQuality);
        Assert.True(vm.ShowLossless);
    }

    [Fact]
    public void BuildOptionsReflectsSelections()
    {
        var vm = NewVm();
        vm.SelectedFormat = OutputFormat.WebP;
        vm.Quality = 60;
        vm.Lossless = true;

        ConversionOptions options = vm.BuildOptions();
        Assert.Equal(OutputFormat.WebP, options.TargetFormat);
        Assert.Equal(60, options.Quality);
        Assert.True(options.Lossless);
        Assert.Equal(ResizeMode.None, options.Resize.Mode);
        Assert.Equal(MetadataPolicy.Preserve, options.Metadata);
    }

    [Fact]
    public void BuildOptionsMapsLongEdgeResize()
    {
        var vm = NewVm();
        vm.SelectedFormat = OutputFormat.Jpg;
        vm.ResizeMode = ResizeMode.LongEdge;
        vm.ResizeLongEdge = 1200;
        vm.DontUpscale = true;

        var resize = vm.BuildOptions().Resize;
        Assert.Equal(ResizeMode.LongEdge, resize.Mode);
        Assert.Equal(1200, resize.Width);
        Assert.True(resize.DontUpscale);
    }
}
