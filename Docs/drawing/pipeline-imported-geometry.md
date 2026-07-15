# Imported geometry pipeline (P-04)

[← Pipelines overview](pipelines-overview.md)

**Class:** `ImportedGeometryDrawingPipeline`  
**Trigger:** `PartModelKind.ImportedGeometry` — dumb solid from STEP/IGES/3D Interconnect with no native parametric features.

---

## Detection

Before pipeline dispatch, `PartModelAnalyzer` runs `ImportedGeometryDetector`:

| Signal | API |
| --- | --- |
| Feature type `BodyFeature`, `MBimport`, `SolidBody`, `ImportSolid` | `IFeature.GetTypeName2()` |
| Feature name `Imported*` | FeatureManager tree |
| 3D Interconnect link | `IFeature.Is3DInterconnectFeature` |

**Rule:** import feature(s) present **and** zero native solid-building features → `ImportedGeometry`.

**Multi-body:** `SolidBodyCount >= 2` → typically `ComplexBracket`, not `CylindricalLike`.

**CylindricalLike** requires `IsTrueCylindricalTube` (elongated bbox, hollow or large cylinder faces, not fillet-dominated).

---

## Shape recognition

`ImportedGeometryShapeRecognizer` reads `PartDoc.GetPartBox(true)` and face statistics:

| Shape | Heuristic |
| --- | --- |
| `ElongatedThinProfile` | Long axis ≥ 3× mid axis, or ≥ 6× short axis (channels, rails, extrusions) |
| `FlatPlateLike` | Two similar large dims + thin gauge (plate/disc) |
| `CylindricalLike` | True tube/rod only (`IsTrueCylindricalTube`) |
| `ComplexBracket` | Multi-body, bent bracket, fillet/hole cylinders |
| `BlockyPrismatic` | All bbox ratios &lt; ~2.5 |
| `Unknown` | Fallback |

Result stored in `PartAnalysisResult.ImportedShape` and bbox L/M/S (meters).

---

## View layout

Same as flat plate / cylindrical:

| View | Name | Content |
| --- | --- | --- |
| 1–3 | `Drawing View1`–`3` | Front, Top, Right |
| 4 | `Drawing View4` | Isometric |

HLV display applied via `DrawingViewDisplayHelper`.

---

## View role classification

`ImportedGeometryViewAnalyzer.Classify` maps orthographic views:

| Role | Selection |
| --- | --- |
| **Profile** | Smallest projected bbox area (cross-section) |
| **LengthPrimary** | View whose max span best matches longest bbox axis |
| **LengthSecondary** | Second-best length view |
| **General** | Remaining ortho views |

---

## Dimension strategy by shape

Implemented in `ImportedDimStrategy`:

### Elongated thin profile (e.g. BTJamb channel)

| View role | Modules |
| --- | --- |
| Profile | `SmartDimOverall`, holes (Ø + qty), hole positions, `ImportedDimSlots` |
| Length | `SmartDimOverall`, `CylindricalDimCenterlines` (centerline on side view) |
| General | `SmartDimOverall` |

### Flat plate-like

| View | Modules |
| --- | --- |
| Primary flat / profile | `SmartDimOverall` |
| All ortho | `SmartDimThickness`, holes, positions, slots |

### Complex bracket / multi-body (e.g. Door_L3_H1)

| View | Modules |
| --- | --- |
| Profile (smallest projection) | `SmartDimOverall`, holes, positions, slots |
| Overall (largest projection) | `SmartDimOverall`, holes, positions, slots |
| Other ortho views | Holes and positions only |

No centerlines, no cylindrical length heuristics.

### Cylindrical-like (true tube only, `IsTrueCylindricalTube`)

### Blocky / unknown

Primary view: overall dims; all views: holes, positions, slots.

---

## Modules

| Module | File | Role |
| --- | --- | --- |
| `ImportedGeometryDetector` | `Services/Analysis/ImportedGeometryDetector.cs` | Import feature detection |
| `ImportedGeometryShapeRecognizer` | `Services/Analysis/ImportedGeometryShapeRecognizer.cs` | Bbox shape classification |
| `ImportedGeometryViewAnalyzer` | `Imported/ImportedGeometryViewAnalyzer.cs` | Profile vs length view roles |
| `ImportedDimStrategy` | `Imported/ImportedDimStrategy.cs` | Shape → dimension dispatch |
| `ImportedDimSlots` | `Imported/ImportedDimSlots.cs` | Parallel-edge slot width (no feature tree) |

---

## Logging markers

```
Part type: Imported geometry (1 import feature(s), primary: Imported1).
  Recognized shape: ElongatedThinProfile.
  Bounding box (L×M×S): 3000.0 × 80.0 × 50.0 mm.
Using imported-geometry pipeline (shape: ElongatedThinProfile).
  Profile view: Drawing View1
  Length view: Drawing View2
```

---

## See also

- [Part classification](part-classification.md)
- [SmartDim modules](../smartdim/modules.md) — reused modules A, C, D
- [Cylindrical annotations](../modules/cylindrical-annotations.md) — centerlines/sizes for imported cylinders
