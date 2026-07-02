# AstralPlane — Image Conversion Tool

**Date:** 2026-07-01
**Status:** Approved design (pre-implementation)

## Summary

AstralPlane is a Windows 11 desktop application that batch-converts images
between formats. It accepts common camera RAW formats and standard raster
formats as input, and writes to modern web/photo formats as output. It is a
*converter*, not an editor: RAW files are **developed to full resolution** via
Magick.NET using the camera's white balance (default demosaic — no white
balance / exposure / tone controls), and there is no editing UI beyond resize
and quality controls.

> **Implementation update (2026-07-01, from Spike 1):** the original plan was
> to extract each RAW's embedded full-size JPEG preview. A spike showed
> embedded previews are *not* full-size (Sony ARW embeds 1616×1080 = 27% of
> sensor; Olympus ORF 3200×2400 = 69%), while Magick.NET develops to true full
> resolution in ~2s. AstralPlane therefore **develops** RAW rather than
> extracting previews, which also removed the LibRaw dependency. Loading RAW
> and raster now share one `MagickInputLoader`.

## Goals

- Convert camera RAW (ARW, CR2/CR3, NEF, DNG, ORF, RAF, RW2, and other
  Magick.NET/LibRaw-supported formats) to JPG, PNG, WebP, or AVIF.
- Convert standard raster formats (JPG, PNG, WebP, TIFF, BMP, GIF, HEIC, AVIF)
  to JPG, PNG, WebP, or AVIF. (HEIC is input-only — see Format Support.)
- Batch / folder conversion with a queue and progress.
- Quality / compression control, resize/scale, and EXIF/XMP metadata handling.
- Never overwrite existing files.

## Non-Goals (v1)

- RAW development controls (white balance, exposure, tone). RAW is developed
  with camera white balance and default demosaic; no adjustment UI.
- Image editing (crop, filters, color grading).
- Multi-frame / animated output. Animated GIF input converts the first frame
  only.
- CLI, web, or non-Windows targets. (Architecture keeps the engine UI-free so
  these remain possible later.)

## Technology

- **UI:** WinUI 3 (Windows App SDK), MVVM. Windows 11 target.
- **Engine:** [Magick.NET](https://github.com/dlemstra/Magick.NET)
  (`Magick.NET-Q16-x64`) — one library covering RAW develop plus all standard
  raster read/write, with WebP/AVIF read+write and HEIC **read-only** via
  bundled delegates (`libwebp`, `libaom`; HEIC encode is not shipped).
- **RAW:** developed to full resolution via Magick.NET
  (`new MagickImage(rawPath)`) with the `dng:use-camera-wb` define. No LibRaw
  dependency (see Spike 1 update in Summary).
- **UI framework:** WinUI 3 via Windows App SDK 2.2.0, TFM
  `net10.0-windows10.0.26100.0`, x64. MVVM logic lives in a WinUI-free
  `AstralPlane.App.ViewModels` library (CommunityToolkit.Mvvm) so it is
  unit-testable.
- **Language:** C#, .NET 10.
- **Tests:** xUnit.

## Format Support

| Direction | Formats |
|-----------|---------|
| **Input** | RAW: ARW, CR2, CR3, NEF, DNG, ORF, RAF, RW2, and other Magick.NET-supported RAW. Raster: JPG, PNG, WebP, TIFF, BMP, GIF, HEIC, AVIF |
| **Output** | JPG, PNG, WebP, AVIF (**HEIC input-only** — encoding not available in the shipped build) |

**HEIC output is disabled.** The shipped Magick.NET Windows build reads HEIC but
cannot encode it (libheif/x265 encoding omitted for patent-licensing reasons).
`FormatCapabilityProbe` detects this at startup and the UI shows HEIC output
disabled with an explanatory note; AVIF (unencumbered, via libaom) is the
recommended modern output.

Per-format output capabilities (drive which option controls are shown):

| Format | Quality | Lossless | Availability |
|--------|---------|----------|--------------|
| JPG    | yes     | no       | always       |
| PNG    | no      | always   | always       |
| WebP   | yes     | toggle   | always       |
| AVIF   | yes     | toggle   | always       |
| HEIC   | yes     | no       | **disabled (no encoder)** |

## Architecture

Three projects. The engine has **no** UI dependency; the view-model library
depends on the engine; the WinUI app depends on both — never the reverse.

### `AstralPlane.Core` (class library)

- **`FormatRegistry`** — authoritative list of supported input/output formats
  and their capabilities (quality? lossless? metadata?).
- **`FormatCapabilityProbe`** — reflects Magick.NET's write support at startup
  so unavailable outputs (HEIC) are disabled in the UI.
- **`FormatDetector`** — classifies an input file by extension **and**
  magic-byte sniffing (content wins on mismatch).
- **`IInputLoader` / `MagickInputLoader`** — turns a source file into an
  in-memory image. One implementation: raster reads directly, RAW is developed
  to full resolution (Spike 1 collapsed the two planned loaders into one).
- **`ConversionOptions`** + **`MagickEncoding`** — target format, quality,
  lossless toggle, resize spec, metadata policy, output-location mode; mapped
  onto Magick encode settings.
- **`ResizeCalculator`** — pure resize math (long-edge / percentage / box,
  don't-upscale).
- **`MagickMetadata`** — preserve/strip EXIF/XMP.
- **`ConversionEngine`** (`IItemConverter`) — takes a source + output path +
  `ConversionOptions`; loads, resizes, applies metadata, encodes, writes.
- **`OutputPathPlanner`** — builds the timestamped destination subfolder and
  per-file output names/extensions.
- **`BatchRunner`** — iterates a job's items with bounded parallelism
  (`Environment.ProcessorCount`), reports progress via `IProgress<>`, isolates
  per-file failures, honors a `CancellationToken`.

### `AstralPlane.App.ViewModels` (class library, WinUI-free)

- **`MainViewModel`** — queue (add/dedupe-by-full-path, classify Ready/
  Unsupported), CanConvert gating, `ConvertAsync` driving `BatchRunner`.
- **`ConversionOptionsViewModel`** — format options from the capability probe,
  per-format quality/lossless visibility, resize/metadata/destination.
- **`QueueItemViewModel`** — per-file name/category/status/message.

### `AstralPlane.App` (WinUI 3)

- Drop zone (files/folder drag-and-drop) + "Add files / Add folder" buttons.
- Queue list with per-file status (Ready / Converting / Done / Failed /
  Unsupported / Skipped) and progress.
- Options panel: target format, quality/lossless (shown per format), resize,
  metadata, output-location toggle, recurse-subfolders toggle.
- Convert / Cancel buttons; overall progress; completion summary with "Open
  output folder".
- ViewModels wrap Core, run conversions off the UI thread, marshal progress
  back to the UI.

## Data Flow

1. **Add files:** user drops files/folder → folders enumerated (optional
   recursion) → each file classified by `FormatDetector` → queue items created
   (`Ready` or `Unsupported`). Duplicates de-duped by full path.
2. **Configure:** user sets one `ConversionOptions` for the batch. Options panel
   shows only controls valid for the chosen target format.
3. **Convert:** `OutputPathPlanner` creates one timestamped subfolder
   (`YYYY-MM-DD_HH-mm-ss`) at job start. `BatchRunner` processes items with
   bounded parallelism. Per item:
   1. `IInputLoader` loads source (RAW → embedded preview, fallback develop;
      raster → Magick.NET).
   2. Apply resize (if enabled; honor "don't upscale").
   3. Apply metadata policy (preserve/strip).
   4. Encode to target format with quality/lossless settings.
   5. Write into the timestamped folder; report progress.
4. **Complete:** summary (N succeeded, M failed, K skipped) + "Open output
   folder".

## Output Location & Naming

- **Modes:** "same folder as each source" *or* "a chosen output folder"
  (user's choice per job).
- In both modes, outputs are written into a **new timestamped subfolder**
  (`YYYY-MM-DD_HH-mm-ss`) created at job start. This guarantees no existing file
  is ever overwritten — collision handling is structural, not per-file.
- Output filename = source base name + new extension (e.g. `photo.arw` →
  `photo.jpg`). Within a single timestamped folder, a rare same-base-name clash
  (two sources with identical base names) is resolved with a `(1)` suffix.

## Resize

- Off by default.
- Modes: max long-edge (px), percentage, explicit W×H.
- "Don't upscale" is honored (never enlarge beyond source dimensions).

## Metadata

- **Preserve:** copy EXIF/XMP into the output where the target format supports
  it.
- **Strip:** remove all metadata.

## Error Handling & Edge Cases

- **Per-file isolation:** each item converts in its own try/catch. A corrupt or
  unreadable file marks *that* item `Failed` with a human-readable reason; the
  batch continues. Failures appear in the completion summary.
- **RAW preview fallback:** no usable embedded preview → full develop; if that
  also fails → item `Failed`. Never crashes the batch.
- **Missing encoders (AVIF/HEIC):** at startup the app probes Magick.NET's
  delegate list. If `libheif` is absent from the shipped build, AVIF/HEIC output
  options are disabled with an explanatory tooltip instead of failing
  mid-conversion. Goal is to ship a build that includes them; this is the safety
  net.
- **HEIC licensing:** AVIF (via aom) is unencumbered. HEIC *encoding* may carry
  patent-licensing considerations depending on the delegate; surfaced but
  documented.
- **Cancellation:** Cancel stops the batch via `CancellationToken`; already
  written files remain.
- **Empty/duplicate input:** duplicates de-duped by full path; empty queue
  disables Convert.
- **Long paths / permissions:** write failures become per-file `Failed` entries
  with the OS error message.
- **Animated GIF input:** first frame only (documented).

## Testing Strategy

**`AstralPlane.Core` (xUnit) — the bulk of testing since it is UI-free:**

- `FormatDetector`: extension + magic-byte classification; mismatched extension
  resolves by content; unknown files reported unsupported.
- `OutputPathPlanner`: timestamped folder name format; same-folder vs
  chosen-folder modes; extension swap; safe filenames; same-base-name suffixing.
- `ConversionOptions` → Magick settings mapping: quality, lossless, format
  flags.
- Resize math: long-edge, percentage, explicit W×H, "don't upscale".
- Metadata policy: preserve copies EXIF/XMP where supported; strip removes it
  (verified by reading the output).
- Conversion integration: small fixture images for each input family converted
  to each output format; assert output exists and is a valid, readable image of
  expected format/dimensions. One real sample ARW fixture verifies
  embedded-preview extraction and the develop fallback.
- `BatchRunner`: a batch containing a deliberately corrupt file still completes
  and reports exactly one failure; cancellation stops further items.

**`AstralPlane.App`:** ViewModel logic (queue add/dedupe, option-panel
visibility per format, enable/disable Convert) unit-tested where practical.
WinUI view wiring verified by manual smoke test — no automated UI tests in v1.

**Fixtures:** tiny raster images are generated programmatically at test time
(`TempWorkspace`) — no committed binaries. RAW files are large, so
`tests/fixtures/raw/` is gitignored; drop a RAW there to enable the develop
integration test (skipped when absent).

## Risks

1. **RAW handling** — *Resolved (Spike 1).* Embedded previews are not
   full-size, so AstralPlane develops RAW via Magick.NET to full resolution.
   LibRaw is not needed; it can be reintroduced only if a specific RAW format
   fails to develop.
2. **AVIF/HEIC delegate availability** — *Resolved.* The startup
   `FormatCapabilityProbe` reports writable formats; HEIC output is disabled
   (no encoder), AVIF is available.
3. **HEIC encoding licensing** — avoided by not shipping a HEIC encoder; AVIF
   (unencumbered) is the modern output.

## Project Layout

```text
AstralPlane/
|-- AstralPlane.slnx
|-- src/
|   |-- AstralPlane.Core/            # conversion engine (no UI)
|   |-- AstralPlane.App.ViewModels/  # MVVM logic (WinUI-free, testable)
|   `-- AstralPlane.App/             # WinUI 3 desktop app (views + wiring)
|-- tests/
|   |-- AstralPlane.Core.Tests/      # xUnit (engine)
|   |-- AstralPlane.App.Tests/       # xUnit (view models)
|   `-- fixtures/raw/                # gitignored RAW samples (optional)
|-- docs/
|   `-- superpowers/specs/           # this design + future specs
`-- README.md
```
