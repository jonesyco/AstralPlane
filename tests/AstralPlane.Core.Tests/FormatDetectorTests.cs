using AstralPlane.Core;

namespace AstralPlane.Core.Tests;

public class FormatDetectorTests
{
    // Minimal magic-byte headers for each container.
    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0];
    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static byte[] Gif() => "GIF89a"u8.ToArray();
    private static byte[] Bmp() => [0x42, 0x4D, 0, 0, 0, 0, 0, 0];
    private static byte[] TiffLE() => [0x49, 0x49, 0x2A, 0x00, 0, 0, 0, 0];
    private static byte[] Webp() => [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8];
    private static byte[] Ftyp(string brand) => [0, 0, 0, 0x18, .. "ftyp"u8, .. System.Text.Encoding.ASCII.GetBytes(brand)];

    [Theory]
    [InlineData(".png", InputCategory.Raster)]
    [InlineData(".jpg", InputCategory.Raster)]
    [InlineData(".arw", InputCategory.Raw)]
    public void MatchingExtensionAndContentClassifyCorrectly(string ext, InputCategory expected)
    {
        byte[] header = ext == ".arw" ? TiffLE() : (ext == ".png" ? Png() : Jpeg());
        var result = FormatDetector.Detect("file" + ext, header);
        Assert.Equal(expected, result.Category);
        Assert.True(result.ExtensionMatchesContent);
    }

    [Fact]
    public void ContentWinsOverExtensionOnMismatch()
    {
        // Named .png but the bytes are JPEG.
        var result = FormatDetector.Detect("mislabeled.png", Jpeg());
        Assert.Equal(InputCategory.Raster, result.Category);
        Assert.Equal(SniffedFormat.Jpeg, result.Sniffed);
        Assert.False(result.ExtensionMatchesContent);
    }

    [Fact]
    public void RecognizedContentWithUnknownExtensionIsSupported()
    {
        // A PNG saved with a .txt extension is still a supported raster (content wins).
        var result = FormatDetector.Detect("photo.txt", Png());
        Assert.Equal(InputCategory.Raster, result.Category);
        Assert.Equal(SniffedFormat.Png, result.Sniffed);
    }

    [Fact]
    public void TiffContainerWithRawExtensionIsRaw()
    {
        // Most RAWs are TIFF-based; extension disambiguates RAW from plain TIFF.
        Assert.Equal(InputCategory.Raw, FormatDetector.Detect("shot.nef", TiffLE()).Category);
        Assert.Equal(InputCategory.Raster, FormatDetector.Detect("scan.tif", TiffLE()).Category);
    }

    [Fact]
    public void IsoBmffBrandsAreClassified()
    {
        Assert.Equal(SniffedFormat.Avif, FormatDetector.Detect("x.avif", Ftyp("avif")).Sniffed);
        Assert.Equal(SniffedFormat.Heif, FormatDetector.Detect("x.heic", Ftyp("heic")).Sniffed);
        Assert.Equal(InputCategory.Raw, FormatDetector.Detect("x.cr3", Ftyp("crx ")).Category);
    }

    [Theory]
    [InlineData(".gif")]
    [InlineData(".bmp")]
    [InlineData(".webp")]
    public void OtherRasterContainersDetected(string ext)
    {
        byte[] header = ext switch { ".gif" => Gif(), ".bmp" => Bmp(), _ => Webp() };
        Assert.Equal(InputCategory.Raster, FormatDetector.Detect("f" + ext, header).Category);
    }

    [Fact]
    public void UnknownContentAndUnknownExtensionIsUnsupported()
    {
        var result = FormatDetector.Detect("notes.txt", "hello world!!"u8.ToArray());
        Assert.Equal(InputCategory.Unsupported, result.Category);
    }

    [Fact]
    public void UnknownContentFallsBackToKnownExtension()
    {
        // Bytes not recognizable, but extension is a known raster — let the loader try.
        var result = FormatDetector.Detect("image.jpg", "hello world!!"u8.ToArray());
        Assert.Equal(InputCategory.Raster, result.Category);
    }
}
