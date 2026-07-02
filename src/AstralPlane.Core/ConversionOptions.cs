namespace AstralPlane.Core;

/// <summary>What to do with EXIF/XMP metadata on the output.</summary>
public enum MetadataPolicy
{
    Preserve,
    Strip,
}

/// <summary>One set of options applied to an entire batch.</summary>
public sealed record ConversionOptions
{
    private readonly int _quality = 85;

    public required OutputFormat TargetFormat { get; init; }

    /// <summary>Encoder quality, 1–100. Ignored for formats without a quality control.</summary>
    public int Quality
    {
        get => _quality;
        init
        {
            if (value is < 1 or > 100)
                throw new ArgumentOutOfRangeException(nameof(Quality), value, "Quality must be between 1 and 100.");
            _quality = value;
        }
    }

    public bool Lossless { get; init; }
    public ResizeSpec Resize { get; init; } = ResizeSpec.None;
    public MetadataPolicy Metadata { get; init; } = MetadataPolicy.Preserve;
    public OutputLocationMode OutputLocation { get; init; } = OutputLocationMode.ChosenFolder;
    public string? ChosenFolder { get; init; }
    public bool RecurseSubfolders { get; init; }

    public static ConversionOptions For(OutputFormat format) => new() { TargetFormat = format };
}
