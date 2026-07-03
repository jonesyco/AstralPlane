# AstralPlane — Thumbnail Grid & Selection UI Update

**Date:** 2026-07-02
**Status:** Approved design (pre-implementation)
**Builds on:** `2026-07-01-astralplane-image-converter-design.md`

## Summary

An update to the AstralPlane WinUI app's queue UI. The queue currently renders
as a plain list of filename + status rows. This change replaces it with a
**thumbnail grid**, adds a **grid ⇄ list view toggle**, and makes **per-tile
selection govern what gets converted** (all tiles selected by default; deselect
to exclude from the batch). It also clarifies the existing upscaling control.

The conversion engine (`AstralPlane.Core`) is **unchanged**. All work is in the
WinUI app and its (WinUI-free) view-model library.

## Goals

- Show each queued file as a thumbnail tile (image + filename + status badge).
- Let the user switch between a **grid** (visual) and a **list** (dense) view.
- Make selection drive conversion: every tile is selected by default; a
  deselected tile is excluded from the batch without being removed.
- Support multi-select bulk actions: **select all / deselect all / remove
  selected**.
- Clarify upscaling: the ability already exists; surface it with a clearer
  affordance.

## Non-Goals

- No changes to the conversion engine, formats, resize math, or output paths.
- No larger single-image preview surface (deferred).
- No Magick-based thumbnail generation in v1 (Shell thumbnails only; see
  Thumbnail Strategy for the documented fallback).
- No per-file conversion options — options remain one set for the batch.

## Upscaling Clarification

Upscaling is **already supported** and requires no engine change. The engine
enlarges to the requested dimensions using ImageMagick's high-quality default
resample (Lanczos), aspect ratio preserved — there is no stretching/distortion.
It is gated by the `ResizeSpec.DontUpscale` flag, exposed today as a
**"Don't upscale"** checkbox that defaults to *checked* (clamping enlargement).

This update reframes that control as **"Allow upscaling"**, defaulting to
*unchecked* — behaviourally identical to today, but clearer. The view model maps
`AllowUpscale` to `DontUpscale = !AllowUpscale`; `AstralPlane.Core` is untouched.

## Thumbnail Strategy

Thumbnails come from the **Windows Shell** via
`StorageFile.GetThumbnailAsync` — the same thumbnails Explorer shows:

- **Fast and OS-cached** (milliseconds), correctly oriented, and uniform across
  RAW, HEIC, and raster inputs — avoiding the ~2s cost of developing a RAW just
  to fill a grid cell.
- Generated **lazily** as tiles are realized (the queue control virtualizes),
  on a **throttled** background path, with a placeholder shown until loaded.
- Each thumbnail is loaded **at most once** per queue item and cached on the
  item's view model for the session.

**RAW codec dependency:** Shell thumbnails for RAW rely on the installed Windows
Raw Image Extension. If it is absent, or a file is unreadable, the provider
returns null and the tile shows a **format-type placeholder glyph**. Thumbnail
failure **never** blocks or fails conversion. A Magick-develop fallback is
possible in a later iteration but is out of scope here.

Thumbnailing enters the app through an `IThumbnailProvider` abstraction so the
view models remain unit-testable with a fake.

## Selection Semantics

- Each tile carries an `IsSelected` state, **default true**, rendered as a
  per-tile checkbox.
- Selection is **independent of the grid/list control's own selection
  highlight** — it is a bound view-model property, so it behaves identically in
  both views and is trivially testable.
- **Convert processes only Ready ∧ selected tiles.** A deselected tile is
  skipped for this batch but stays in the queue.
- `CanConvert` is true only when not running and at least one tile is both Ready
  and selected.
- **Unsupported** tiles are shown dimmed with an "unsupported" badge and are not
  selectable/convertible regardless of selection state.
- Bulk actions: **Select all**, **Deselect all**, **Remove selected**
  (removes the selected tiles from the queue and frees their paths so they can
  be re-added).

## Architecture

Three-project layering is unchanged: Core (engine, no UI) ← ViewModels
(WinUI-free) ← App (WinUI). This change touches only the ViewModels and App.

### `AstralPlane.Core` — unchanged

Upscaling already works via `ResizeSpec.DontUpscale`; no code change.

### `AstralPlane.App.ViewModels` (WinUI-free)

- **`IThumbnailProvider`** *(new)* — `Task<byte[]?> GetAsync(string path,
  int pixelSize, CancellationToken ct)`. Returns encoded thumbnail bytes, or
  null when no thumbnail is available. Faked in tests.
- **`QueueItemViewModel`** *(extended)* — adds:
  - `IsSelected` (`bool`, default **true**)
  - `Thumbnail` (`byte[]?`)
  - `ThumbnailState` (`Pending` | `Loaded` | `Unavailable`)
- **`MainViewModel`** *(extended)* — adds:
  - `ViewMode` (`QueueViewMode { Grid, List }`, default **Grid**)
  - `SelectAll()`, `DeselectAll()`, `RemoveSelected()`, `SelectedCount`
  - `EnsureThumbnailAsync(QueueItemViewModel item)` — loads the item's thumbnail
    once via the injected `IThumbnailProvider`, setting `ThumbnailState` to
    `Loaded` or `Unavailable`; a no-op if already attempted.
  - `CanConvert` and `ConvertAsync` now filter to **Ready ∧ IsSelected**.
  - Subscribes to items' `IsSelected` changes to refresh `CanConvert` /
    `SelectedCount`.
  - Takes an optional `IThumbnailProvider` (constructor injection; existing
    `classifier` parameter preserved).

### `AstralPlane.App` (WinUI 3)

- **`ShellThumbnailProvider : IThumbnailProvider`** *(new)* — wraps
  `StorageFile.GetThumbnailAsync`, reading the returned stream into a `byte[]`;
  returns null on failure.
- **`BytesToImageConverter`** *(new)* — `byte[]` → `BitmapImage` for the tile
  `Image`.
- **MainPage** *(changed)* — the queue region becomes an **`ItemsView`** whose
  `Layout` toggles between `UniformGridLayout` (grid) and `StackLayout` (list),
  bound to `ViewMode`, over one shared `ItemsSource`.
  - **Tile template (grid):** thumbnail or placeholder glyph, filename, status
    badge, and a selection checkbox bound to `IsSelected`. Unsupported tiles
    dimmed with a badge, checkbox hidden/disabled.
  - **List template:** compact row (small thumbnail/glyph + filename + status +
    checkbox).
  - **Toolbar additions:** grid/list view toggle, Select all, Deselect all,
    Remove selected (alongside existing Add files / Add folder / Clear).
  - Thumbnails load lazily on container realization; concurrency throttled.
  - Drop zone / empty-state message preserved.

## Data Flow (changes only)

1. **Add files** (unchanged): files classified by `FormatDetector` into queue
   items — now created with `IsSelected = true` and `ThumbnailState = Pending`.
2. **Display:** as tiles realize, the view calls `EnsureThumbnailAsync`; the
   provider fetches a Shell thumbnail off the UI thread; on completion the tile
   shows the image (`Loaded`) or a placeholder (`Unavailable`).
3. **Select/deselect:** toggling a tile updates `IsSelected`; `CanConvert` and
   `SelectedCount` refresh.
4. **Convert:** processes only Ready ∧ selected items via the existing
   `BatchRunner`/`ConversionEngine` path; deselected items are left untouched.
5. **Remove selected:** removes selected tiles from the queue and frees their
   paths.

## Error Handling & Edge Cases

- **Thumbnail unavailable** (missing RAW codec, unreadable, or provider error):
  `ThumbnailState = Unavailable`, placeholder glyph shown; **conversion is
  unaffected**.
- **Deselect all:** Convert disabled (`CanConvert` false).
- **Unsupported tiles:** dimmed, badged, never selectable/convertible.
- **Large batches:** virtualization + lazy, throttled thumbnail loads + OS
  thumbnail cache keep the grid responsive; thumbnails are computed only for
  realized tiles.
- **View toggle:** switching grid ⇄ list preserves selection and loaded
  thumbnails (state lives on the view model, not the control).
- **Re-add after remove:** `RemoveSelected` frees paths from the dedupe set so a
  removed file can be added again.

## Testing Strategy

TDD, as with the original engine. View-model logic is the automated surface;
views are smoke-tested manually.

**`AstralPlane.App.Tests` (xUnit):**

- `QueueItemViewModel`: `IsSelected` defaults true; toggling raises change
  notification; `ThumbnailState` defaults `Pending`.
- `MainViewModel`:
  - Deselect-all → `CanConvert` false; select at least one Ready → true.
  - `ConvertAsync` converts only Ready ∧ selected items (fake `IItemConverter`);
    deselected items are not passed to the batch.
  - `RemoveSelected` removes exactly the selected items and frees their paths
    (re-adding the same path works).
  - `SelectAll` / `DeselectAll` / `SelectedCount` correctness.
  - `ViewMode` defaults to Grid and toggles.
  - `EnsureThumbnailAsync` sets `Loaded`/`Unavailable` from a fake provider and
    does not refetch a second time.
- Upscaling: `ConversionOptionsViewModel` maps `AllowUpscale` to
  `ResizeSpec.DontUpscale = !AllowUpscale`.

**Manual smoke:** grid renders thumbnails; view toggle switches layout;
deselecting a tile excludes it from output; remove-selected works; a file with
no available thumbnail shows the placeholder and still converts; unsupported
tiles are dimmed and non-selectable.

## Risks

1. **RAW thumbnail codec absent** — *Mitigated.* Placeholder fallback; thumbnail
   failure never affects conversion. Documented dependency on the Windows Raw
   Image Extension.
2. **`ItemsView` API maturity** — `ItemsView` with switchable `Layout` is the
   intended modern control. If it proves limiting, fall back to a `GridView` /
   `ListView` pair toggled by visibility over the same source; selection is on
   the view model either way, so the fallback is low-cost.
3. **Thumbnail load storms on huge batches** — *Mitigated.* Lazy per-realized
   tile + throttled concurrency + OS cache.

## Affected Files

- `src/AstralPlane.App.ViewModels/IThumbnailProvider.cs` *(new)*
- `src/AstralPlane.App.ViewModels/QueueItemViewModel.cs` — selection + thumbnail
  state
- `src/AstralPlane.App.ViewModels/MainViewModel.cs` — view mode, selection-driven
  convert, bulk actions, thumbnail loading
- `src/AstralPlane.App.ViewModels/ConversionOptionsViewModel.cs` — `AllowUpscale`
- `src/AstralPlane.App/ShellThumbnailProvider.cs` *(new)*
- `src/AstralPlane.App/Converters.cs` — `BytesToImageConverter`
- `src/AstralPlane.App/MainPage.xaml` + `.xaml.cs` — `ItemsView`, tile templates,
  toolbar actions, lazy thumbnail wiring
- `tests/AstralPlane.App.Tests/` — new/updated view-model tests
