# Flat plate pipeline (P-01)

[← Pipelines overview](pipelines-overview.md) · [Sub-kinds matrix](flat-plate-subkinds.md)

**Class:** `FlatPlateDrawingPipeline`  
**Trigger:** `PartModelKind.FlatPlate` — sheet metal **without** bend features, or non-sheet parts classified as flat (including EST `PLATE` overriding cylindrical geometry).

Nested dimension strategy: `FlatPlateSubKindResolver` → `FlatPlateDimRouter` (see [flat-plate-subkinds.md](flat-plate-subkinds.md)).

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

## Dimension strategies by sub-kind

### Generic

For each orthographic view (skips isometric):

1. `SmartDimOverall` — overall W/H on primary flat-lying view (partials: Vertical / Horizontal / Trapezoid / Helpers)
2. `SmartDimSymmetryCenterlines` — H/V centerlines when bilateral symmetry detected (primary only)
3. `SmartDimThickness` — gauge
4. `SmartDimFillets` / `SmartDimChamfers`
5. `SmartDimHoles` / `SmartDimHolePositions` / `SmartDimCutouts`

Optional: model sketch import when `CanImportSketchDimensions`.

### RoundDisc

- `RoundFlatPlateDimensions` — OD, centerlines, holes (outer Ø excluded)
- `RoundFlatPlateThickness` once after views

See [Round flat plate](../modules/round-flat-plate.md).

### RoundedEnd

- Primary: `RoundedFlatPlateDimensions` — overall, outer arc, tip, holes
- Sides: thickness

### ArcSector

- Primary: `ArcSectorDimensionPipeline` — R_in/R_out, angle, strip width, bbox, hole Ø + 2 coords
- Sides: thickness

See [Arc-sector plate](../modules/arc-sector-plate.md).

### FlangeGasket / BafflePlate

Dedicated domain pipelines (`FlangeGasket/`, `BafflePlate/`). See [baffle-plate-pipeline.md](baffle-plate-pipeline.md).

---

## Post-processing

- **Deduper** excludes isometric (`Drawing View4` when used as iso).
- Auto-arrange and `SheetLayoutNormalizer` (via `AdjustSheetScaleIfNeeded`).
- EST quality validate against Dim2/Dim3 hints when present.

---

## Logging markers

```
Using flat-plate drawing pipeline (3 views + isometric, no flat pattern). [P-01 FlatPlate]
Generic flat plate mode: overall, thickness, holes.
Round flat plate mode: OD, centerlines, side-view thickness.
Arc-sector plate mode: R_in/R_out, angle or radial strip, bbox, hole Ø + 2 coords, thickness.
Primary flat view: Drawing View2
```

---

## See also

- [Flat-plate sub-kinds (traceability)](flat-plate-subkinds.md)
- [Part classification](part-classification.md)
- [SmartDim modules](../smartdim/modules.md)
- [Pipeline bent sheet metal](pipeline-bent-sheet-metal.md)
