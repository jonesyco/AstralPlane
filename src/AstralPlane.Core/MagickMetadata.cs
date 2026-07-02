using ImageMagick;

namespace AstralPlane.Core;

/// <summary>Applies the batch's metadata policy to an image before encoding.</summary>
public static class MagickMetadata
{
    public static void Apply(MagickImage image, MetadataPolicy policy)
    {
        if (policy == MetadataPolicy.Strip)
        {
            // Remove all profiles (EXIF/XMP/ICC/IPTC) and comments.
            image.Strip();
        }
        // Preserve: metadata already carried on the loaded image is written
        // out by the encoder for formats that support it.
    }
}
