# AstralPlane

A Windows 11 desktop app that batch-converts images between formats. It reads
common camera **RAW** and standard **raster** formats and writes to modern
web/photo formats. AstralPlane is a *converter, not an editor* — RAW files are
developed to full resolution with the camera's white balance; there are no
tone/exposure controls beyond resize and quality.

Built with **WinUI 3** (Windows App SDK) and **[Magick.NET](https://github.com/dlemstra/Magick.NET)**.

## Features

- **Batch conversion** with a drag-and-drop queue, per-file status, live
  progress, and cancellation.
- **RAW input**, developed to full sensor resolution (ARW, CR2, CR3, NEF, DNG,
  ORF, RAF, RW2, and other Magick.NET-supported formats).
- **Raster input** — JPG, PNG, WebP, TIFF, BMP, GIF, HEIC, AVIF.
- **Output** to JPG, PNG, WebP, or AVIF, with per-format quality / lossless
  controls.
- **Resize** by long-edge, percentage, or explicit W×H, with a "don't upscale"
  option.
- **Metadata** — preserve or strip EXIF/XMP.
- **Never overwrites**: every run writes into a fresh timestamped subfolder
  (`YYYY-MM-DD_HH-mm-ss`).

## Format support

| Direction | Formats |
|-----------|---------|
| **Input** | RAW: ARW, CR2, CR3, NEF, DNG, ORF, RAF, RW2, and other Magick.NET-supported RAW. Raster: JPG, PNG, WebP, TIFF, BMP, GIF, HEIC, AVIF |
| **Output** | JPG, PNG, WebP, AVIF |

| Output | Quality | Lossless |
|--------|---------|----------|
| JPG    | yes     | no       |
| PNG    | no      | always   |
| WebP   | yes     | toggle   |
| AVIF   | yes     | toggle   |

> **HEIC is input-only.** The shipped Magick.NET build reads HEIC but cannot
> encode it (libheif/x265 encode is omitted for patent-licensing reasons).
> HEIC output is auto-disabled in the UI with an explanatory note; **AVIF**
> (unencumbered, via libaom) is the recommended modern output. AstralPlane
> probes encoder availability at startup, so unavailable outputs are disabled
> up front rather than failing mid-conversion.

## Architecture

Three projects. The engine has **no** UI dependency; the view-model library
depends on the engine; the WinUI app depends on both — never the reverse.

```
AstralPlane/
├── src/
│   ├── AstralPlane.Core/            # conversion engine (no UI)
│   ├── AstralPlane.App.ViewModels/  # MVVM logic (WinUI-free, testable)
│   └── AstralPlane.App/             # WinUI 3 desktop app (views + wiring)
├── tests/
│   ├── AstralPlane.Core.Tests/      # xUnit (engine)
│   ├── AstralPlane.App.Tests/       # xUnit (view models)
│   └── fixtures/raw/                # gitignored RAW samples (optional)
└── docs/superpowers/specs/          # design spec
```

- **`AstralPlane.Core`** — `FormatRegistry` and `FormatCapabilityProbe`
  (what can be written), `FormatDetector` (extension + magic-byte sniffing;
  content wins on mismatch), `MagickInputLoader` (raster read + RAW develop),
  `ResizeCalculator`, `ConversionOptions`/`MagickEncoding`, `MagickMetadata`,
  `OutputPathPlanner` (timestamped, collision-safe destinations),
  `ConversionEngine`, and `BatchRunner` (bounded parallelism, per-file failure
  isolation, `CancellationToken`).
- **`AstralPlane.App.ViewModels`** — `MainViewModel`,
  `ConversionOptionsViewModel`, `QueueItemViewModel` (CommunityToolkit.Mvvm),
  free of WinUI types so they are unit-testable.
- **`AstralPlane.App`** — WinUI 3 views and wiring (pickers, drag-drop,
  running conversions off the UI thread).

## Requirements

- Windows 11 (min. build 10.0.17763.0), **x64**.
- [.NET 10 SDK](https://dotnet.microsoft.com/) (10.0.201 or later).

The app ships **unpackaged** (self-contained) — it runs as a plain `.exe` with
no MSIX identity or Developer Mode required.

## Build & run

```powershell
# Restore & build (x64)
dotnet build -p:Platform=x64

# Run the app
dotnet run --project src/AstralPlane.App -p:Platform=x64

# Run the tests
dotnet test
```

## Testing

The engine is UI-free and carries the bulk of the coverage: format detection,
output-path planning, resize math, options→Magick mapping, metadata
preserve/strip, end-to-end conversion round-trips, and batch
isolation/cancellation. View-model logic (queue add/dedupe, per-format option
visibility, Convert gating) is unit-tested; the WinUI views are verified by
manual smoke test.

Tiny raster fixtures are generated programmatically at test time — no binaries
are committed. RAW files are large, so `tests/fixtures/raw/` is gitignored:
drop a RAW there to enable the RAW develop / AVIF integration tests (they skip
when absent).

## License

See [LICENSE](LICENSE) if present. Note Magick.NET and its bundled delegates
(libwebp, libaom, etc.) carry their own licenses.
