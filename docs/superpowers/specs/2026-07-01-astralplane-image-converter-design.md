# AstralPlane — Image Conversion Tool

**Date:** 2026-07-01
**Status:** Approved design (pre-implementation)

## Summary

AstralPlane is a Windows 11 desktop application that batch-converts images
between formats. It accepts common camera RAW formats and standard raster
formats as input, and writes to modern web/photo formats as output. It is a
*converter*, not an editor: RAW files are handled by extracting their embedded
full-size JPEG preview rather than developing them, and there is no editing UI
beyond resize and quality controls.

## Goals

- Convert camera RAW (ARW, CR2/CR3, NEF, DNG, ORF, RAF, RW2, and other
  LibRaw-supported formats) to JPG, PNG, WebP, AVIF, or HEIC.
- Convert standard raster formats (JPG, PNG, WebP, TIFF, BMP, GIF, HEIC, AVIF)
  to JPG, PNG, WebP, AVIF, or HEIC.
- Batch / folder conversion with a queue and progress.
- Quality / compression control, resize/scale, and EXIF/XMP metadata handling.
- Never overwrite existing files.

## Non-Goals (v1)

- RAW development controls (white balance, exposure, tone). Embedded preview
  only.
- Image editing (crop, filters, color grading).
- Multi-frame / animated output. Animated GIF input converts the first frame
  only.
- CLI, web, or non-Windows targets. (Architecture keeps the engine UI-free so
  these remain possible later.)

## Technology

- **UI:** WinUI 3 (Windows App SDK), MVVM. Windows 11 target.
- **Engine:** [Magick.NET](https://github.com/dlemstra/Magick.NET) — one library
  covering RAW decode plus all standard raster read/write, including WebP,
  AVIF, and HEIC via bundled delegates (`libwebp`, `libaom`/`libheif`).
- **RAW preview extraction:** primary path via Magick.NET; **LibRaw fallback**
  (thumbnail/preview API) if Magick.NET cannot cleanly extract the full-size
  embedded preview. This is the top technical risk (see Risks).
- **Language:** C#, .NET (latest LTS supported by Windows App SDK).
- **Tests:** xUnit.

## Format Support

| Direction | Formats |
|-----------|---------|
| **Input** | RAW: ARW, CR2, CR3, NEF, DNG, ORF, RAF, RW2, and other LibRaw-supported RAW. Raster: JPG, PNG, WebP, TIFF, BMP, GIF, HEIC, AVIF |
| **Output** | JPG, PNG, WebP, AVIF, HEIC |

Per-format output capabilities (drive which option controls are shown):

| Format | Quality | Lossless |
|--------|---------|----------|
| JPG    | yes     | no       |
| PNG    | no      | always   |
| WebP   | yes     | toggle   |
| AVIF   | yes     | toggle   |
| HEIC   | yes     | (delegate-dependent) |

## Architecture

Two projects. The engine has **no** UI dependency; the UI depends on the
engine and never the reverse.

### `AstralPlane.Core` (class library)

- **`FormatRegistry`** — authoritative list of supported input/output formats
  and their capabilities (quality? lossless?).
- **`FormatDetector`** — classifies an input file by extension **and**
  magic-byte sniffing (content wins on mismatch).
- **`IInputLoader`** — turns a source file into an in-memory image. Two
  implementations behind one interface, selected by `FormatDetector`:
  - `RawPreviewLoader` — extracts embedded full-size preview; falls back to
    full develop if no usable preview exists.
  - `RasterLoader` — loads standard raster via Magick.NET.
- **`ConversionOptions`** — target format, quality, lossless toggle, resize
  spec, metadata policy, output-location mode.
- **`ConversionEngine`** — takes a loaded image + `ConversionOptions`, applies
  resize and metadata policy, encodes to the target format, writes the file.
- **`OutputPathPlanner`** — builds the timestamped destination subfolder and
  per-file output names/extensions.
- **`BatchRunner`** — iterates a job's items with bounded parallelism
  (`Environment.ProcessorCount`), reports progress via `IProgress<>`, isolates
  per-file failures, honors a `CancellationToken`.

### `AstralPlane.App` (WinUI 3, MVVM)

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

**Fixtures:** small `tests/fixtures/` set (tiny JPG/PNG/WebP/TIFF/BMP/GIF + one
small HEIC/AVIF + one sample ARW), kept small enough to live in the repo.

## Risks

1. **RAW embedded-preview extraction via Magick.NET** — Magick.NET's RAW path
   normally *develops* the file rather than exposing the embedded preview.
   Mitigation: validate a preview-extraction approach early (spike); if
   unavailable, use LibRaw's thumbnail/preview API directly as the
   `RawPreviewLoader` implementation. Develop remains the fallback.
2. **AVIF/HEIC delegate availability** in the chosen Magick.NET distribution —
   mitigated by the startup delegate probe and disabling unavailable outputs.
3. **HEIC encoding licensing** — documented; AVIF recommended as the
   unencumbered modern option.

## Project Layout

```text
AstralPlane/
|-- AstralPlane.sln
|-- src/
|   |-- AstralPlane.Core/        # conversion engine (no UI)
|   `-- AstralPlane.App/         # WinUI 3 desktop app (MVVM)
|-- tests/
|   |-- AstralPlane.Core.Tests/  # xUnit
|   `-- fixtures/                # small sample images
|-- docs/
|   `-- superpowers/specs/       # this design + future specs
`-- README.md
```
