using AstralPlane.Core;
using ImageMagick;

namespace AstralPlane.Core.Tests;

public class MetadataPolicyTests
{
    private static MagickImage ImageWithExif()
    {
        var image = new MagickImage(MagickColors.SteelBlue, 16, 16);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "AstralPlaneCam");
        image.SetProfile(exif);
        return image;
    }

    [Fact]
    public void PreserveKeepsExifThroughEncode()
    {
        using var image = ImageWithExif();
        MagickMetadata.Apply(image, MetadataPolicy.Preserve);

        byte[] jpg = image.ToByteArray(MagickFormat.Jpeg);
        using var reloaded = new MagickImage(jpg);
        var make = reloaded.GetExifProfile()?.GetValue(ExifTag.Make);
        Assert.Equal("AstralPlaneCam", make?.Value);
    }

    [Fact]
    public void StripRemovesAllMetadata()
    {
        using var image = ImageWithExif();
        MagickMetadata.Apply(image, MetadataPolicy.Strip);

        byte[] jpg = image.ToByteArray(MagickFormat.Jpeg);
        using var reloaded = new MagickImage(jpg);
        Assert.Null(reloaded.GetExifProfile());
    }
}
