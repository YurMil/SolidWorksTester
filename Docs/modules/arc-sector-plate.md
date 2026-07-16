# Arc-sector flat plate module

[← Documentation hub](../README.md) · [Flat-plate sub-kinds](../drawing/flat-plate-subkinds.md) · [Flat plate pipeline (P-01)](../drawing/pipeline-flat-plate.md)

Specialised annotation for **annular-sector / ring-segment** flat plates: two concentric arcs plus radial end faces (e.g. EST-P61728 `PLATE - 4 x 1022 x 1661.7`). Not a hollow tube, not a rounded-end sliver.

**Folder:** `ArcSector/`  
**Namespace:** `SolidWorksTester.ArcSector`  
**Sub-kind:** `FlatPlateSubKind.ArcSector`

---

## Activation (priority)

Resolved in `FlatPlateSubKindResolver` **before** property `Generic` / `RoundedEnd` when drawing or geometry hits:

| Source | Type | Notes |
| --- | --- | --- |
| Drawing | `ArcSectorViewAnalyzer.DetectFromDrawing` / `TryGetProfile` | ≥2 large concentric partial arcs + ≥1 radial edge |
| Geometry | `ArcSectorModelAnalyzer.IsArcSectorPlate` | Thin plate + concentric large cylinder faces |
| Property | `FlatPlateSubKind` = `ARC` / `SECTOR` / `ARC_SECTOR` / `ANNULAR` / `RING_SEGMENT` | Via `PropertyPartClassification` |
| Router | `DrawingPipelineRouter` | Generic + geometry ArcSector → forced ArcSector |

Even when EST Name maps to `PLATE` → `Generic`, drawing detection still overrides to ArcSector.

---

## Module map (algorithm branches)

Orchestrator: `ArcSectorDimensionPipeline` — calls independent branches so each can evolve alone.

| File | Class | Branch responsibility |
| --- | --- | --- |
| `ArcSectorDimensionPipeline.cs` | `ArcSectorDimensionPipeline` | Entry: primary view + side thickness |
| `ArcSectorProfile.cs` | `ArcSectorProfile` | Shared geometry record (arcs, radii, center, radials) |
| `ArcSectorViewAnalyzer.cs` | `ArcSectorViewAnalyzer` | Drawing-time profile detect |
| `ArcSectorModelAnalyzer.cs` | `ArcSectorModelAnalyzer` | Model-snapshot detect |
| `ArcSectorRadii.cs` | `ArcSectorRadii` | **R_in / R_out** radial dims |
| `ArcSectorAngle.cs` | `ArcSectorAngle` | Sector sweep angle between radial ends |
| `ArcSectorStripWidth.cs` | `ArcSectorStripWidth` | Strip on radial end (**R_out − R_in**, e.g. 153 mm) |
| `ArcSectorOverall.cs` | `ArcSectorOverall` | Bounding-box W/H (outline pick + arc Max) |
| `ArcSectorHoles.cs` | `ArcSectorHoles` | Hole Ø + **two** position coords + center mark |
| `ArcSectorDimHelpers.cs` | `ArcSectorDimHelpers` | Shared offsets / `SelectByID2` picks |

---

## Primary-view dimension order

```
1. ArcSectorRadii          → R_in, R_out
2. ArcSectorAngle          → sector angle (if 2 radials)
3. ArcSectorStripWidth     → radial end strip (always, independent of angle)
4. ArcSectorOverall        → Overall_W / Overall_H (bbox)
5. ArcSectorHoles          → SmartDimHoles + radial/arc (or outline) positions
```

Side / non-primary orthographic views: `SmartDimThickness` only (`AddSideViewOnly`).

---

## Typical log markers

```
Arc-sector plate mode: R_in/R_out, angle or radial strip, bbox, hole Ø + 2 coords, thickness.
  [ArcSector] R_in=1022.0 mm, R_out=1175.0 mm on Drawing View2.
  [ArcSector] ArcSector_R_In R1022.0 mm …
  [ArcSector] ArcSector_R_Out R1175.0 mm …
  [ArcSector] Sector angle …
  [ArcSector] Radial strip 153.0 mm (R_out−R_in) …
  [Holes] ⌀50.0mm × 1 pcs
  [ArcSector] Hole 1: position from radial end.
  [ArcSector] Hole 1: position from inner arc.
```

---

## Wiring

| Layer | Hook |
| --- | --- |
| Enum | `FlatPlateSubKind.ArcSector` |
| Classify | `FlatPlateClassifier` → `ArcSectorModelAnalyzer` |
| Resolve | `FlatPlateSubKindResolver.TryResolveArcSectorOverride` + `BuildFromSubKind` |
| Dims | `FlatPlateDimRouter` → `ArcSectorDimensionPipeline` |
| Context | `UsesDiscStyleThickness`, `SkipsModelImport` |

---

## Adding a new ArcSector algorithm branch

1. Add `ArcSector/YourBranch.cs` with static `Add(SmartDimHelper, …, ArcSectorProfile, …)`.
2. Call it from `ArcSectorDimensionPipeline.AddForPrimaryView` in the intended order.
3. Use unique `DimensionedFeatures` keys (`ArcSector_*`).
4. Document the branch in this file’s module map.
5. Update [flat-plate-subkinds.md](../drawing/flat-plate-subkinds.md) if behaviour is user-visible.

---

## Contrast with related sub-kinds

| Sub-kind | Geometry | Primary dims |
| --- | --- | --- |
| `RoundDisc` | Full circular face | OD, centerlines |
| `RoundedEnd` | One large arc + chord | Overall, outer arc Ø, tip |
| **`ArcSector`** | **Two concentric arcs + radials** | **R_in/R_out, angle, strip, bbox, hole×2** |
| `Generic` | Rectangular / irregular | SmartDim A–E + symmetry CLs |

---

## See also

- [Flat-plate sub-kinds (traceability)](../drawing/flat-plate-subkinds.md)
- [Round flat plate](round-flat-plate.md)
- [Baffle plate pipeline](../drawing/baffle-plate-pipeline.md)
- [Pipeline router](../drawing/pipeline-router.md)
