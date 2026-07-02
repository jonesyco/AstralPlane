using ImageMagick;

namespace AstralPlane.Core;

/// <summary>
/// Converts a single loaded image to the target format: load → resize →
/// metadata policy → encode → write. UI-free and stateless.
/// </summary>
public sealed class ConversionEngine(IInputLoader loader) : IItemConverter
{
    private readonly IInputLoader _loader = loader;

    public void Convert(string sourcePath, string outputPath, ConversionOptions options)
    {
        using MagickImage image = _loader.Load(sourcePath);

        ApplyResize(image, options.Resize);
        MagickMetadata.Apply(image, options.Metadata);
        MagickEncoding.Apply(image, options);

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        image.Write(outputPath);
    }

    private static void ApplyResize(MagickImage image, ResizeSpec spec)
    {
        if (spec.Mode == ResizeMode.None)
            return;

        PixelSize target = ResizeCalculator.Compute((int)image.Width, (int)image.Height, spec);
        if (target.Width == (int)image.Width && target.Height == (int)image.Height)
            return;

        image.Resize(new MagickGeometry((uint)target.Width, (uint)target.Height)
        {
            IgnoreAspectRatio = true, // dimensions are already aspect-correct
        });
    }
}
