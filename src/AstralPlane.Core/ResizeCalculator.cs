namespace AstralPlane.Core;

public enum ResizeMode
{
    None,
    LongEdge,
    Percentage,
    Box,
}

public readonly record struct PixelSize(int Width, int Height);

/// <summary>How a batch should be resized. Off by default.</summary>
public sealed record ResizeSpec(ResizeMode Mode, int Width, int Height, double Percent, bool DontUpscale)
{
    public static ResizeSpec None { get; } = new(ResizeMode.None, 0, 0, 0, false);

    public static ResizeSpec LongEdge(int pixels, bool dontUpscale) =>
        new(ResizeMode.LongEdge, pixels, pixels, 0, dontUpscale);

    public static ResizeSpec Percentage(double percent, bool dontUpscale) =>
        new(ResizeMode.Percentage, 0, 0, percent, dontUpscale);

    public static ResizeSpec Box(int width, int height, bool dontUpscale) =>
        new(ResizeMode.Box, width, height, 0, dontUpscale);
}

/// <summary>Pure resize math: computes target dimensions, preserving aspect ratio.</summary>
public static class ResizeCalculator
{
    public static PixelSize Compute(int sourceWidth, int sourceHeight, ResizeSpec spec)
    {
        double scale = spec.Mode switch
        {
            ResizeMode.None => 1.0,
            ResizeMode.LongEdge => (double)spec.Width / Math.Max(sourceWidth, sourceHeight),
            ResizeMode.Percentage => spec.Percent / 100.0,
            ResizeMode.Box => Math.Min((double)spec.Width / sourceWidth, (double)spec.Height / sourceHeight),
            _ => 1.0,
        };

        if (spec.DontUpscale && scale > 1.0)
            scale = 1.0;

        int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return new PixelSize(width, height);
    }
}
