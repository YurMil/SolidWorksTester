# SOLIDWORKS API reference by area

[← Documentation hub](../README.md) · [COM connection](com-connection.md)

This document maps **API areas used in this codebase** to their roles. It is not a full SOLIDWORKS API manual — use the official SOLIDWORKS API Help for complete signatures.

**Namespaces:** `SolidWorks.Interop.sldworks`, `SolidWorks.Interop.swconst`

---

## Application & documents

| API | Used in | Purpose |
| --- | --- | --- |
| `ISldWorks.OpenDoc6` | `PartModelAnalyzer`, drawing open | Silent part/drawing open; returns errors/warnings by ref |
| `ISldWorks.NewDocument` | `SheetMetalDrawingService` | Create drawing from `.DRWDOT` template |
| `ISldWorks.CloseDoc` | Analyzer, service | Close after analysis or save |
| `IModelDoc2.SaveAs` / `Save3` | `SheetMetalDrawingService` | Write `.SLDDRW` |
| `IModelDoc2.ForceRebuild3` | All pipelines | Rebuild drawing after views/dims |
| `IModelDoc2.Extension` | Throughout | Selection, units, view ops |

### Document type constants

| Constant | Value role |
| --- | --- |
| `swDocumentTypes_e.swDocPART` | Part analysis |
| `swDocumentTypes_e.swDocDRAWING` | Drawing processing |
| `swOpenDocOptions_e.swOpenDocOptions_Silent` | No UI during open |

---

## Drawing views

| API | Used in | Purpose |
| --- | --- | --- |
| `IDrawingDoc.CreateDrawViewFromModelView3` | `DrawingPipelineShared` | Front / top / right from model orientation |
| `IDrawingDoc.CreateDrawViewFromModelView3` (isometric) | Shared | Fourth view — isometric or palette-based |
| `IDrawingDoc.GenerateViewPaletteViews` | `BentSheetMetalDrawingPipeline` | Build palette incl. flat pattern |
| `IDrawingDoc.GetDrawingPaletteViewNames` | Bent pipeline | Find flat pattern palette entry |
| `IDrawingDoc.CreateDrawViewFromPaletteView` | Bent pipeline | Insert flat pattern |
| `IView.SetName2` | Shared, bent | Standardise `Drawing View1`…`4` |
| `IView.GetName2` | Pipelines, SmartDim | View iteration / skip isometric |
| `IView.ReferencedConfiguration` | Bent pipeline | Active part config for flat pattern |
| `IDrawingDoc.AutoArrangeDimensions` | Shared | SOLIDWORKS auto layout |
| `IDrawingDoc.GetFirstView` | Deduper, dim loops | Sheet → first model view chain |

### View naming convention

| Name | Role |
| --- | --- |
| `Drawing View1` | Front |
| `Drawing View2` | Top |
| `Drawing View3` | Right |
| `Drawing View4` | Isometric **or** flat pattern (bent pipeline) |

See [Glossary](../glossary.md).

---

## Selection & dimensions

| API | Used in | Purpose |
| --- | --- | --- |
| `IModelDocExtension.SelectByID2` | Shared, SmartDim | Select views, edges, faces for commands |
| `IModelDocExtension.AddDimension2` | SmartDim | Place linear/angular dims |
| `IModelDocExtension.AddRadialDimension2` | SmartDim, RoundFlatPlate | Diameter/radius |
| `IModelDoc2.ClearSelection2` | SmartDim | Reset selection between ops |
| `IModelDoc2.SetUserPreferenceToggle` | SmartDimHelper | Suppress dim input dialog |

Select type tokens (see `SmartDimConstants`):

- `DRAWINGVIEW`
- `COMPONENT`
- `FACE`

---

## Part geometry & features

| API | Used in | Purpose |
| --- | --- | --- |
| `IModelDoc2.FirstFeature` / `Feature.GetNextFeature` | `PartModelAnalyzer` | Feature tree walk |
| `Feature.GetTypeName2` | Analyzer | Bend, sheet metal, hole detection |
| `PartDoc.GetBodies2` | Analyzer | Solid body enumeration |
| `Body2.GetFaces` | Analyzer, SmartDim | Face type counting |
| `Face2.GetSurface` | Analyzer, geometry helpers | Plane / cylinder tests |
| `Surface.IsCylinder` / `IsPlane` | Analyzer | Topology classification |
| `Surface.CylinderParams` | Analyzer | Radius, hollow detection |
| `PartDoc.GetPartBox` | Analyzer, RoundFlatPlate | Bounding box (meters) |

**Note:** `GetPartBox` is on `PartDoc`, not `IModelDoc2` — cast required.

---

## Sheet metal

| API | Used in | Purpose |
| --- | --- | --- |
| Feature types: `BaseFlange`, `SheetMetal`, `FlatPattern` | Analyzer | Sheet metal detection |
| Bend feature types (see [Part classification](../drawing/part-classification.md)) | Analyzer | Bent vs flat plate |
| Sheet metal thickness read | `RoundFlatPlateThickness`, `SmartDimThickness` | Gauge dimension |

---

## Units

| API | Used in | Purpose |
| --- | --- | --- |
| `IModelDocExtension.SetUserPreferenceInteger` | `SheetMetalDrawingService` | Set drawing to MMGS |

Internal API lengths are **meters**. Display on drawing follows template/unit settings.

See [Units & coordinates](units-and-coordinates.md).

---

## Error handling patterns

```csharp
int errors = 0, warnings = 0;
var doc = swApp.OpenDoc6(path, type, options, "", ref errors, ref warnings);
if (doc == null)
    throw new InvalidOperationException($"Failed to open. Error code: {errors}.");
```

Most geometry helpers wrap COM calls in try/catch and log or skip on failure — batch processing continues with the next part where possible.

---

## See also

- [Shared drawing services](../drawing/shared-drawing-services.md)
- [SmartDim overview](../smartdim/overview.md)
- [Units & coordinates](units-and-coordinates.md)
