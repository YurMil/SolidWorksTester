# Cylindrical pipeline (P-03)

[← Pipelines overview](pipelines-overview.md)

**Class:** `CylindricalDrawingPipeline`  
**Trigger:** `PartModelKind.Cylindrical` — revolve/sweep/loft-heavy geometry or dominant cylindrical faces.

---

## View layout

| View | Name | Content |
| --- | --- | --- |
| 1–3 | `Drawing View1`–`3` | Orthographic (end + side views) |
| 4 | `Drawing View4` | **Isometric** |

No flat pattern. Annotation logic lives in `Cylindrical/*` rather than standard SmartDim modules A–E.

---

## Dimension strategy

Uses dedicated cylindrical modules:

| Module | Typical role |
| --- | --- |
| `CylindricalDimCenterlines` | Axis / center marks |
| `CylindricalDimSizes` | OD, ID, length |
| `CylindricalDimHoles` | Hole features on shell |
| `CylindricalDimImport` | Model-imported annotation hints |

**End-face view:** `CylindricalDimSizes.EndFaceViewName = "Drawing View2"` — top view often shows circular end.

Primary view selection skips View4 when choosing the main cylindrical side view.

---

## Post-processing

- Deduper excludes `"Drawing View4"` (isometric).
- Auto-arrange and scale adjustment.

---

## Hollow vs solid

`PartAnalysisResult.IsHollow` is set when two distinct cylinder radii differ by > 0.5 mm — typical tube/pipe. Hole detection avoids treating the bore as a generic hole feature when multiple radii exist.

---

## See also

- [Cylindrical annotations](../modules/cylindrical-annotations.md)
- [Part classification](part-classification.md)
- [Flat plate pipeline](pipeline-flat-plate.md)
