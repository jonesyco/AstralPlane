using AstralPlane.Core;
using ImageMagick;

namespace AstralPlane.Core.Tests;

public class ConversionEngineTests
{
    private readonly ConversionEngine _engine = new(new MagickInputLoader());

    [Theory]
    [InlineData(OutputFormat.Jpg, MagickFormat.Jpeg)]
    [InlineData(OutputFormat.Png, MagickFormat.Png)]
    [InlineData(OutputFormat.WebP, MagickFormat.WebP)]
    [InlineData(OutputFormat.Avif, MagickFormat.Avif)]
    public void ConvertsToEachWritableOutputFormat(OutputFormat target, MagickFormat expected)
    {
        using var ws = new TempWorkspace();
        string source = ws.CreateImage("in.png", MagickFormat.Png, 64, 48);
        string output = ws.PathFor($"out{FormatRegistry.GetOutput(target).FileExtension}");

        _engine.Convert(source, output, ConversionOptions.For(target));

        Assert.True(File.Exists(output));
        using var result = new MagickImage(output);
        Assert.Equal(expected, result.Format);
        Assert.Equal(64u, result.Width);
        Assert.Equal(48u, result.Height);
    }

    [Fact]
    public void AppliesResize()
    {
        using var ws = new TempWorkspace();
        string source = ws.CreateImage("in.png", MagickFormat.Png, 100, 80);
        string output = ws.PathFor("out.jpg");

        var options = ConversionOptions.For(OutputFormat.Jpg) with
        {
            Resize = ResizeSpec.LongEdge(50, dontUpscale: false),
        };
        _engine.Convert(source, output, options);

        using var result = new MagickImage(output);
        Assert.Equal(50u, result.Width);
        Assert.Equal(40u, result.Height);
    }

    [Fact]
    public void StripsMetadataWhenRequested()
    {
        using var ws = new TempWorkspace();
        string source = ws.PathFor("in.jpg");
        using (var img = new MagickImage(MagickColors.Teal, 20, 20))
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Make, "AstralPlaneCam");
            img.SetProfile(exif);
            img.Write(source, MagickFormat.Jpeg);
        }
        string output = ws.PathFor("out.jpg");

        _engine.Convert(source, output, ConversionOptions.For(OutputFormat.Jpg) with { Metadata = MetadataPolicy.Strip });

        using var result = new MagickImage(output);
        Assert.Null(result.GetExifProfile());
    }

    [Fact]
    public void CreatesOutputDirectoryIfMissing()
    {
        using var ws = new TempWorkspace();
        string source = ws.CreateImage("in.png", MagickFormat.Png);
        string output = ws.PathFor(Path.Combine("nested", "sub", "out.jpg"));

        _engine.Convert(source, output, ConversionOptions.For(OutputFormat.Jpg));

        Assert.True(File.Exists(output));
    }

    [Fact]
    public void DoesNotUpscaleWhenForbidden()
    {
        using var ws = new TempWorkspace();
        string source = ws.CreateImage("in.png", MagickFormat.Png, 40, 30);
        string output = ws.PathFor("out.png");

        var options = ConversionOptions.For(OutputFormat.Png) with
        {
            Resize = ResizeSpec.LongEdge(1000, dontUpscale: true),
        };
        _engine.Convert(source, output, options);

        using var result = new MagickImage(output);
        Assert.Equal(40u, result.Width);
        Assert.Equal(30u, result.Height);
    }
}
