using ImageMagick;

namespace AstralPlane.Core;

/// <summary>Reports which output formats the loaded Magick.NET build can actually encode.</summary>
public interface IFormatCapabilityProbe
{
    bool CanWrite(OutputFormat format);

    /// <summary>The subset of <see cref="FormatRegistry.OutputFormats"/> that can be written.</summary>
    IReadOnlyList<OutputFormat> WritableOutputs { get; }
}

/// <summary>
/// Startup delegate probe (the spec's safety net): queries Magick.NET's
/// supported-format list so unavailable outputs (e.g. HEIC encoding) are
/// disabled in the UI rather than failing mid-conversion.
/// </summary>
public sealed class MagickFormatCapabilityProbe : IFormatCapabilityProbe
{
    private readonly Dictionary<OutputFormat, bool> _writable;

    public MagickFormatCapabilityProbe()
    {
        _writable = FormatRegistry.OutputFormats.ToDictionary(
            info => info.Format,
            info => MagickFormatInfo.Create(info.MagickFormat)?.SupportsWriting ?? false);
    }

    public bool CanWrite(OutputFormat format) =>
        _writable.TryGetValue(format, out bool can) && can;

    public IReadOnlyList<OutputFormat> WritableOutputs =>
        _writable.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
}
