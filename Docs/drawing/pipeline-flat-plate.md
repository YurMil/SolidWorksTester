# Flat plate pipeline (P-01)

[← Pipelines overview](pipelines-overview.md)

**Class:** `FlatPlateDrawingPipeline`  
**Trigger:** `PartModelKind.FlatPlate` — sheet metal **without** bend features, or non-sheet parts classified as flat.

---

## View layout

| View | Name | Content |
| --- | --- | --- |
| 1 | `Drawing View1` | Front |
| 2 | `Drawing View2` | Top |
| 3 | `Drawing View3` | Right |
| 4 | `Drawing View4` | **Isometric** (no flat pattern) |

Flat parts without bends do not use the sheet-metal flat pattern view.

---

## Dimension strategy

### Standard rectangular flat plate

For each orthographic view (skips View4):

1. `SmartDimOverall` — overall width/height
2. `SmartDimThickness` — gauge (parallel edges)
3. `SmartDimHoles` — hole diameters
4. `SmartDimHolePositions` — hole location dims
5. `SmartDimCutouts` — cutout/notch sizes

### Round flat plate mode

Activated when:

- `PartAnalysisResult.IsRoundFlatProfile` is true (bbox heuristic), **or**
- `RoundFlatPlateViewAnalyzer.DetectFromDrawing` finds circular face views

Per orthographic view:

- `RoundFlatPlateDimensions.AddForView` — OD, centerlines, holes (outer Ø excluded)

Once after all views:

- `RoundFlatPlateDimensions.AddThicknessOnce` → `RoundFlatPlateThickness`

See [Round flat plate module](../modules/round-flat-plate.md).

---

## Post-processing

- **Deduper** excludes `Drawing View4` (isometric has no driven dims).
- Auto-arrange and scale adjustment run on all views.

---

## Logging markers

```
Using flat-plate drawing pipeline (3 views + isometric, no flat pattern).
Round flat plate mode: OD, centerlines, side-view thickness.
```

---

## See also

- [Part classification](part-classification.md) — `IsRoundFlatDisc`
- [SmartDim modules](../smartdim/modules.md)
- [Pipeline bent sheet metal](pipeline-bent-sheet-metal.md) — contrast with flat pattern
