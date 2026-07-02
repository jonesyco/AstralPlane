using AstralPlane.Core;
using ImageMagick;

namespace AstralPlane.Core.Tests;

public class RawDevelopTests
{
    private static readonly string[] RawExtensions =
        [".arw", ".cr2", ".cr3", ".nef", ".dng", ".orf", ".raf", ".rw2"];

    private static string? FindRawFixture()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "raw");
        if (!Directory.Exists(dir)) return null;
        return Directory.EnumerateFiles(dir)
            .FirstOrDefault(f => RawExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
    }

    [Fact]
    public void RawDecodeDelegateIsAvailable()
    {
        // ImageMagick decodes all RAW via the "dng:" coder; it must be present.
        var info = MagickFormatInfo.Create(MagickFormat.Dng);
        Assert.NotNull(info);
        Assert.True(info!.SupportsReading, "RAW (dng:) decode delegate must be available in this build.");
    }

    [SkippableFact]
    public void DevelopsRawToFullResolution()
    {
        string? raw = FindRawFixture();
        Skip.If(raw is null, "No RAW fixture present in tests/fixtures/raw (see README).");

        using MagickImage image = new MagickInputLoader().Load(raw!);

        // Spike 1 established RAW develop yields full sensor resolution; assert
        // it is a large image, not a tiny embedded thumbnail.
        Assert.True(image.Width >= 2000 && image.Height >= 1500,
            $"Developed RAW should be full-size; got {image.Width}x{image.Height}.");
    }

    [SkippableFact]
    public void ConvertsRawToAvifEndToEnd()
    {
        string? raw = FindRawFixture();
        Skip.If(raw is null, "No RAW fixture present in tests/fixtures/raw (see README).");

        using var ws = new TempWorkspace();
        string output = ws.PathFor("out.avif");
        var engine = new ConversionEngine(new MagickInputLoader());

        engine.Convert(raw!, output, ConversionOptions.For(OutputFormat.Avif) with
        {
            Resize = ResizeSpec.LongEdge(1600, dontUpscale: true),
        });

        Assert.True(File.Exists(output));
        using var result = new MagickImage(output);
        Assert.Equal(MagickFormat.Avif, result.Format);
        Assert.True(Math.Max(result.Width, result.Height) == 1600);
    }
}
