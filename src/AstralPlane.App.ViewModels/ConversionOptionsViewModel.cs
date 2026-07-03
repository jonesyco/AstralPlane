using AstralPlane.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AstralPlane.App.ViewModels;

/// <summary>A selectable output format, with availability from the delegate probe.</summary>
public sealed record OutputFormatOption(OutputFormat Format, string DisplayName, bool IsEnabled, string? DisabledReason);

/// <summary>
/// Batch conversion options for the UI. Which controls are shown is driven by
/// the selected format's capabilities (<see cref="FormatRegistry"/>); which
/// formats are selectable is driven by the encoder probe.
/// </summary>
public sealed partial class ConversionOptionsViewModel : ObservableObject
{
    private const string HeicDisabledReason =
        "HEIC encoding is not available in this build (patent-licensing). Use AVIF for a modern format.";

    public ConversionOptionsViewModel(IFormatCapabilityProbe probe)
    {
        Formats = FormatRegistry.OutputFormats
            .Select(info =>
            {
                bool enabled = probe.CanWrite(info.Format);
                string? reason = enabled ? null
                    : info.Format == OutputFormat.Heic ? HeicDisabledReason
                    : $"{info.Format} encoding is not available in this build.";
                return new OutputFormatOption(info.Format, info.Format.ToString().ToUpperInvariant(), enabled, reason);
            })
            .ToList();

        // Default to the first enabled format.
        _selectedFormat = Formats.First(f => f.IsEnabled).Format;
    }

    public IReadOnlyList<OutputFormatOption> Formats { get; }

    /// <summary>Formats that can actually be written (shown in the picker).</summary>
    public IReadOnlyList<OutputFormatOption> AvailableFormats => Formats.Where(f => f.IsEnabled).ToList();

    public bool HasUnavailableFormats => UnavailableFormatsNote is not null;

    /// <summary>A note explaining any disabled formats, or null if all are available.</summary>
    public string? UnavailableFormatsNote
    {
        get
        {
            var disabled = Formats.Where(f => !f.IsEnabled).ToList();
            return disabled.Count == 0
                ? null
                : string.Join(" ", disabled.Select(d => d.DisabledReason));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowQuality))]
    [NotifyPropertyChangedFor(nameof(ShowLossless))]
    private OutputFormat _selectedFormat;

    [ObservableProperty] private int _quality = 85;
    [ObservableProperty] private bool _lossless;

    [ObservableProperty] private ResizeMode _resizeMode = ResizeMode.None;
    [ObservableProperty] private int _resizeLongEdge = 2000;
    [ObservableProperty] private int _resizeWidth = 1920;
    [ObservableProperty] private int _resizeHeight = 1080;
    [ObservableProperty] private double _resizePercent = 50;
    // Reframed control: default off (clamp enlargement), behaviourally identical
    // to the previous "Don't upscale" default. Maps to ResizeSpec.DontUpscale = !AllowUpscale.
    [ObservableProperty] private bool _allowUpscale;

    [ObservableProperty] private MetadataPolicy _metadata = MetadataPolicy.Preserve;
    [ObservableProperty] private OutputLocationMode _outputLocation = OutputLocationMode.SameAsSource;
    [ObservableProperty] private string? _chosenFolder;
    [ObservableProperty] private bool _recurseSubfolders;

    // View-support lists for enum-bound pickers.
    public IReadOnlyList<ResizeMode> ResizeModes { get; } = Enum.GetValues<ResizeMode>();
    public IReadOnlyList<MetadataPolicy> MetadataPolicies { get; } = Enum.GetValues<MetadataPolicy>();
    public IReadOnlyList<OutputLocationMode> OutputLocationModes { get; } = Enum.GetValues<OutputLocationMode>();

    private OutputFormatInfo SelectedInfo => FormatRegistry.GetOutput(SelectedFormat);

    public bool ShowQuality => SelectedInfo.SupportsQuality;
    public bool ShowLossless => SelectedInfo.Lossless == LosslessSupport.Toggle;

    public ConversionOptions BuildOptions() => new()
    {
        TargetFormat = SelectedFormat,
        Quality = Quality,
        Lossless = Lossless,
        Resize = BuildResize(),
        Metadata = Metadata,
        OutputLocation = OutputLocation,
        ChosenFolder = ChosenFolder,
        RecurseSubfolders = RecurseSubfolders,
    };

    private ResizeSpec BuildResize() => ResizeMode switch
    {
        ResizeMode.LongEdge => ResizeSpec.LongEdge(ResizeLongEdge, !AllowUpscale),
        ResizeMode.Percentage => ResizeSpec.Percentage(ResizePercent, !AllowUpscale),
        ResizeMode.Box => ResizeSpec.Box(ResizeWidth, ResizeHeight, !AllowUpscale),
        _ => ResizeSpec.None,
    };
}
