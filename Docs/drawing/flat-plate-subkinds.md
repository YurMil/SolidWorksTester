# Flat-plate sub-kinds — traceability matrix

[← Documentation hub](../README.md) · [Flat plate pipeline (P-01)](pipeline-flat-plate.md) · [Pipeline router](pipeline-router.md)

P-01 nesting: after `DrawingPipelineId.FlatPlate`, dimension strategy is chosen by `FlatPlateSubKind`.

```
DrawingPipelineRouter
  → FlatPlateDrawingPipeline
    → FlatPlateSubKindResolver.Resolve  → FlatPlateDimContext
      → FlatPlateDimRouter.ApplyAll / ApplyForView
```

---

## Sub-kind catalog

| `FlatPlateSubKind` | Code folder / modules | Resolve / detect | Doc |
| --- | --- | --- | --- |
| `Unknown` | — | Fallback → Generic | — |
| `Generic` | `SmartDim/Modules/*` + `SmartDimSymmetryCenterlines` | Default / EST `PLATE` | [SmartDim modules](../smartdim/modules.md) |
| `RoundDisc` | `RoundFlatPlate/` | Bbox disc **or** `RoundFlatPlateViewAnalyzer` | [round-flat-plate.md](../modules/round-flat-plate.md) |
| `RoundedEnd` | `RoundFlatPlate/Rounded*` | `IsRoundedEndFlatProfile` **or** `RoundedFlatPlateViewAnalyzer` | [round-flat-plate.md](../modules/round-flat-plate.md) |
| `FlangeGasket` | `FlangeGasket/` | Geometry / drawing / description | Flange docs in code + EST catalog |
| `BafflePlate` | `BafflePlate/` | Dense hole array / EST baffle | [baffle-plate-pipeline.md](baffle-plate-pipeline.md) |
| `ArcSector` | `ArcSector/` | Concentric arcs **or** model analyzer | [arc-sector-plate.md](../modules/arc-sector-plate.md) |

---

## Resolve priority (`FlatPlateSubKindResolver`)

Order matters — earlier overrides win when applicable:

1. **Baffle override** (`TryResolveBaffleOverride`)
2. **Flange override** (`TryResolveFlangeOverride`)
3. **ArcSector override** (`TryResolveArcSectorOverride`) — even if properties say `Generic`
4. Forced sub-kind from `DrawingRouteDecision` / non-geometry property classification
5. Drawing / geometry fallbacks: RoundDisc → ArcSector → RoundedEnd → Generic

---

## Dimension router matrix (`FlatPlateDimRouter`)

| Sub-kind | Primary view | Side views | Model sketch import |
| --- | --- | --- | --- |
| Generic | Overall, symmetry CLs, holes, fillet/chamfer, cutouts | Thickness | If `CanImportSketchDimensions` |
| RoundDisc | `RoundFlatPlateDimensions` | Disc-style thickness once | Skipped |
| RoundedEnd | `RoundedFlatPlateDimensions` | Thickness | Allowed |
| FlangeGasket | Flange pipeline | Thickness | Pipeline-specific |
| BafflePlate | Import + baffle modules | Thickness | Import-first |
| ArcSector | `ArcSectorDimensionPipeline` branches | Thickness | Skipped |

Context flags: `FlatPlateDimContext.UsesDiscStyleThickness`, `SkipsModelImport`.

---

## Property / EST string map

Parsed in `PropertyPartClassification.TryParseFlatPlateSubKind`:

| Raw token (examples) | Sub-kind |
| --- | --- |
| `DISC`, `ROUND`, `ROUND_DISC` | RoundDisc |
| `ROUNDED`, `ROUNDED_END` | RoundedEnd |
| `FLANGE`, `GASKET`, `FLANGE_GASKET` | FlangeGasket |
| `BAFFLE`, `BAFFLE_PLATE`, `PERFORATED`, `TUBE_SHEET` | BafflePlate |
| `ARC`, `SECTOR`, `ARC_SECTOR`, `ANNULAR`, `RING_SEGMENT` | ArcSector |
| `GENERIC`, `PLATE` | Generic |

Geometry may still upgrade Generic → ArcSector / Flange / Baffle at resolve time.

---

## Classification sources

| Field on `PartAnalysisResult` | Meaning |
| --- | --- |
| `FlatPlateSubKind` | Final merged sub-kind |
| `GeometryFlatPlateSubKind` | From `FlatPlateClassifier` only |
| `FlatPlateSubKindSource` | Geometry vs CustomProperty / EST |
| `IsRoundFlatProfile` / `IsRoundedEndFlatProfile` | Legacy geometry flags feeding RoundDisc / RoundedEnd |

Classifier order (`FlatPlateClassifier`): Baffle → Flange → RoundDisc → RoundedEnd → **ArcSector** → Generic.

---

## Shared SmartDim structure (Generic path)

| Area | Files |
| --- | --- |
| Overall (partials) | `SmartDimOverall.cs`, `.Vertical`, `.Horizontal`, `.Trapezoid`, `.Helpers` |
| Symmetry centerlines | `SmartDimSymmetryCenterlines.cs` |
| Helper dims (partials) | `SmartDimHelper.Dimensions*.cs` (Create / Query / Text / Delete) |
| Fillets / Chamfers | `SmartDimFillets.cs`, `SmartDimChamfers.cs` |

---

## See also

- [Part classification](part-classification.md)
- [EST Name catalog](../analysis/est-name-catalog.md)
- [Project structure](../architecture/project-structure.md)
- [Adding a pipeline](../development/adding-a-pipeline.md) — nested sub-kind section
