using System.Text;

namespace AstralPlane.Core;

/// <summary>Container type identified from a file's leading bytes.</summary>
public enum SniffedFormat
{
    Unknown,
    Jpeg,
    Png,
    Gif,
    Bmp,
    Tiff,
    WebP,
    Heif,   // ISO-BMFF with a HEIC/HEIF brand
    Avif,   // ISO-BMFF with the AVIF brand
    Cr3,    // ISO-BMFF with Canon's "crx " brand (RAW)
}

/// <summary>Result of classifying an input file by extension and content.</summary>
public sealed record InputClassification(
    InputCategory Category,
    SniffedFormat Sniffed,
    bool ExtensionMatchesContent);

/// <summary>
/// Classifies an input file by extension AND magic-byte sniffing. Content wins
/// on mismatch; when content is unrecognizable, the extension is the fallback.
/// </summary>
public static class FormatDetector
{
    private const int HeaderBytes = 32;

    public static InputClassification Detect(string path)
    {
        byte[] header = ReadHeader(path);
        return Detect(path, header);
    }

    public static InputClassification Detect(string fileName, ReadOnlySpan<byte> header)
    {
        string ext = Path.GetExtension(fileName);
        SniffedFormat sniffed = Sniff(header);
        InputCategory byExtension = FormatRegistry.CategorizeExtension(ext);

        // Content recognized: it is authoritative for category.
        if (sniffed != SniffedFormat.Unknown)
        {
            InputCategory byContent = sniffed switch
            {
                SniffedFormat.Cr3 => InputCategory.Raw,
                // A TIFF container is a RAW only when the extension says so
                // (ARW/CR2/NEF/DNG... are all TIFF-based); otherwise it is a plain TIFF.
                SniffedFormat.Tiff => byExtension == InputCategory.Raw ? InputCategory.Raw : InputCategory.Raster,
                _ => InputCategory.Raster,
            };
            bool matches = ExtensionAgreesWith(ext, sniffed);
            return new InputClassification(byContent, sniffed, matches);
        }

        // Content not recognized: trust a known extension, else unsupported.
        return new InputClassification(byExtension, SniffedFormat.Unknown, ExtensionMatchesContent: true);
    }

    private static SniffedFormat Sniff(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return SniffedFormat.Jpeg;
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 &&
            h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return SniffedFormat.Png;
        if (h.Length >= 6 && (h[..6].SequenceEqual("GIF87a"u8) || h[..6].SequenceEqual("GIF89a"u8))) return SniffedFormat.Gif;
        if (h.Length >= 2 && h[0] == 0x42 && h[1] == 0x4D) return SniffedFormat.Bmp;
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return SniffedFormat.WebP;

        // ISO base media file format: "ftyp" box at offset 4, brand at offset 8.
        if (h.Length >= 12 && h[4..8].SequenceEqual("ftyp"u8))
        {
            string brand = Encoding.ASCII.GetString(h[8..12]);
            return brand switch
            {
                "avif" or "avis" => SniffedFormat.Avif,
                "crx " => SniffedFormat.Cr3,
                "heic" or "heix" or "heif" or "mif1" or "msf1" or "hevc" => SniffedFormat.Heif,
                _ => SniffedFormat.Heif, // unknown ftyp brand: treat as HEIF-family container
            };
        }

        if (h.Length >= 4 &&
            ((h[0] == 0x49 && h[1] == 0x49 && h[2] == 0x2A && h[3] == 0x00) ||   // little-endian TIFF
             (h[0] == 0x4D && h[1] == 0x4D && h[2] == 0x00 && h[3] == 0x2A)))     // big-endian TIFF
            return SniffedFormat.Tiff;

        return SniffedFormat.Unknown;
    }

    private static bool ExtensionAgreesWith(string ext, SniffedFormat sniffed)
    {
        ext = ext.ToLowerInvariant();
        return sniffed switch
        {
            SniffedFormat.Jpeg => ext is ".jpg" or ".jpeg",
            SniffedFormat.Png => ext is ".png",
            SniffedFormat.Gif => ext is ".gif",
            SniffedFormat.Bmp => ext is ".bmp",
            SniffedFormat.WebP => ext is ".webp",
            SniffedFormat.Tiff => ext is ".tif" or ".tiff" || FormatRegistry.CategorizeExtension(ext) == InputCategory.Raw,
            SniffedFormat.Avif => ext is ".avif",
            SniffedFormat.Heif => ext is ".heic" or ".heif",
            SniffedFormat.Cr3 => ext is ".cr3",
            _ => false,
        };
    }

    private static byte[] ReadHeader(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> buffer = stackalloc byte[HeaderBytes];
        int read = stream.Read(buffer);
        return buffer[..read].ToArray();
    }
}
