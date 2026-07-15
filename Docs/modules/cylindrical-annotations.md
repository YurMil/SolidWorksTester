# Cylindrical annotations

[← Documentation hub](../README.md) · [Cylindrical pipeline (P-03)](../drawing/pipeline-cylindrical.md)

**Folder:** `Cylindrical/`  
**Namespace:** `SolidWorksTester.Cylindrical`

Annotation modules for pipes, tubes, and revolved/swept cylindrical parts. **Not** part of SmartDim A–G.

---

## Modules

| Class | Purpose |
| --- | --- |
| `CylindricalViewAnalyzer` | End-face vs side-view detection (`IsCircleProfileInView` + bbox aspect) |
| `CylindricalDimCenterlines` | Center marks on end-face views; **centerlines** on side views |
| `CylindricalDimCenterlinesLegacy` | Parallel outer edges, bbox midline, cylindrical face pick |
| `CylindricalDimSizes` | OD, ID, overall length; end-face view handling |
| `CylindricalDimHoles` | Hole features on cylindrical shells |
| `CylindricalDimImport` | Propagate or align with model annotations where applicable |

---

## View conventions

| Constant | Value | Meaning |
| --- | --- | --- |
| `CylindricalDimSizes.EndFaceViewName` | `"Drawing View2"` | Top view often shows circular end section |
| Isometric | `"Drawing View4"` | Excluded from primary sizing loops |

Primary side view selection in `CylindricalDrawingPipeline` skips View4 when searching for the main cylindrical projection.

---

## Hollow parts

When `PartAnalysisResult.IsHollow` is true:

- Expect both OD and ID dimensions on appropriate views
- Bore cylinder must not be classified as a generic hole feature (see analyzer logic)

---

## Centerlines vs center marks

| View type | Detection | Annotation |
| --- | --- | --- |
| **End face** | Full circles with axis ⊥ view; bbox ≈ OD (not elongated) | `InsertCenterMark2` on OD (and ID if hollow) |
| **Side view** | Elongated bbox (length >> OD) | `InsertCenterLine2` between parallel outer edges |
| **Isometric** | View4 | Skipped |

Side-view centerline fallbacks (in order): outer parallel edges → longest parallel pair → bbox midline → cylindrical face pick.

---

## Version-specific behavior (2022–2026)

| SOLIDWORKS | Side-view centerlines | End-face center marks |
| --- | --- | --- |
| 2022–2026 | Outer-edge / bbox / face API (no silhouette) | Yes |

See [Version router](../solidworks-api/version-router.md).

---

## Extension

New cylindrical annotation types should:

1. Live under `Cylindrical/`
2. Accept `(SmartDimHelper or ISldWorks, IModelDoc2, IDrawingDoc, IView, log)`
3. Be invoked from `CylindricalDrawingPipeline.ApplyDimensions` in deterministic order

---

## See also

- [Part classification](../drawing/part-classification.md)
- [Pipelines overview](../drawing/pipelines-overview.md)
