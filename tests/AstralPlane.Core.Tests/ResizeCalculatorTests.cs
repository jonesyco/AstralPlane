using AstralPlane.Core;

namespace AstralPlane.Core.Tests;

public class ResizeCalculatorTests
{
    [Fact]
    public void NoneReturnsSourceDimensions()
    {
        var size = ResizeCalculator.Compute(4000, 3000, ResizeSpec.None);
        Assert.Equal(new PixelSize(4000, 3000), size);
    }

    [Fact]
    public void LongEdgeScalesLandscapePreservingAspect()
    {
        var size = ResizeCalculator.Compute(4000, 3000, ResizeSpec.LongEdge(1000, dontUpscale: false));
        Assert.Equal(new PixelSize(1000, 750), size);
    }

    [Fact]
    public void LongEdgeScalesPortraitPreservingAspect()
    {
        var size = ResizeCalculator.Compute(3000, 4000, ResizeSpec.LongEdge(1000, dontUpscale: false));
        Assert.Equal(new PixelSize(750, 1000), size);
    }

    [Fact]
    public void LongEdgeDoesNotUpscaleWhenForbidden()
    {
        var size = ResizeCalculator.Compute(800, 600, ResizeSpec.LongEdge(1000, dontUpscale: true));
        Assert.Equal(new PixelSize(800, 600), size);
    }

    [Fact]
    public void LongEdgeUpscalesWhenAllowed()
    {
        var size = ResizeCalculator.Compute(800, 600, ResizeSpec.LongEdge(1000, dontUpscale: false));
        Assert.Equal(new PixelSize(1000, 750), size);
    }

    [Fact]
    public void PercentageScalesBothDimensions()
    {
        var size = ResizeCalculator.Compute(4000, 3000, ResizeSpec.Percentage(50, dontUpscale: false));
        Assert.Equal(new PixelSize(2000, 1500), size);
    }

    [Fact]
    public void PercentageOverHundredClampedWhenNoUpscale()
    {
        var size = ResizeCalculator.Compute(4000, 3000, ResizeSpec.Percentage(200, dontUpscale: true));
        Assert.Equal(new PixelSize(4000, 3000), size);
    }

    [Fact]
    public void BoxFitsWithinBoundsPreservingAspect()
    {
        var size = ResizeCalculator.Compute(4000, 3000, ResizeSpec.Box(1000, 1000, dontUpscale: false));
        Assert.Equal(new PixelSize(1000, 750), size);
    }

    [Fact]
    public void BoxUsesLimitingDimension()
    {
        var size = ResizeCalculator.Compute(4000, 3000, ResizeSpec.Box(1000, 2000, dontUpscale: false));
        Assert.Equal(new PixelSize(1000, 750), size);
    }

    [Fact]
    public void BoxDoesNotUpscaleWhenForbidden()
    {
        var size = ResizeCalculator.Compute(500, 400, ResizeSpec.Box(1000, 1000, dontUpscale: true));
        Assert.Equal(new PixelSize(500, 400), size);
    }

    [Fact]
    public void NeverReturnsZeroDimensions()
    {
        var size = ResizeCalculator.Compute(10, 4000, ResizeSpec.Percentage(1, dontUpscale: false));
        Assert.True(size.Width >= 1 && size.Height >= 1);
    }
}
