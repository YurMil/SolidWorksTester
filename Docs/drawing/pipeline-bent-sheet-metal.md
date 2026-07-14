# Bent sheet metal pipeline (P-02)

[← Pipelines overview](pipelines-overview.md)

**Class:** `BentSheetMetalDrawingPipeline`  
**Trigger:** `PartModelKind.BentSheetMetal` — sheet metal feature present **and** at least one bend feature.

---

## View layout

| View | Name | Content |
| --- | --- | --- |
| 1–3 | `Drawing View1`–`3` | Standard orthographic |
| 4 | `Drawing View4` | **Flat pattern** from View Palette |

---

## Flat pattern creation

1. Locate front view (`Drawing View1`) and read `ReferencedConfiguration`.
2. `drawing.GenerateViewPaletteViews(partPath)` — builds palette for the part.
3. `GetDrawingPaletteViewNames()` — search for name containing:
   - `"flat pattern"` (English), or
   - `"развертка"` / `"развёртка"` (Russian templates)
4. `CreateDrawViewFromPaletteView` at `DrawingViewLayout.FlatPatternX/Y`.
5. Rename to `Drawing View4`.

If palette flat pattern is missing, the pipeline logs a warning and continues with three views only.

---

## Dimension strategy

### Orthographic views (1–3)

| Module | Role |
| --- | --- |
| `SmartDimOverall` | Envelope dimensions |
| `SmartDimThickness` | Sheet gauge |
| `SmartDimHoles` | Hole Ø |
| `SmartDimHolePositions` | Hole locations |
| `SmartDimCutouts` | Cutouts |
| `SmartDimBends` | Bend lines / angles where detectable |

### Flat pattern view (4)

| Module | Role |
| --- | --- |
| `SmartDimFlatBendLines` | Flat pattern bend annotations |

Deduper runs on **all** views including flat pattern (no isometric exclude).

---

## Post-processing

Same shared steps as P-01: deduper → auto-arrange → scale → rebuild.

---

## See also

- [Part classification](part-classification.md) — bend feature types
- [SmartDim module G](../smartdim/modules.md#module-g--smartdimflatbendlines)
- [Flat plate pipeline](pipeline-flat-plate.md) — no flat pattern case
