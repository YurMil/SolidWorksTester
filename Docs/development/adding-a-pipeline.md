# Adding a pipeline

[← Documentation hub](../README.md) · [Pipelines overview](../drawing/pipelines-overview.md) · [Pipeline router](../drawing/pipeline-router.md)

Step-by-step guide for supporting a new part family (e.g. weldments, assemblies).

Routing is **not** a `switch` inside `SheetMetalDrawingService`. Classification produces a `PartAnalysisResult`; `DrawingPipelineRouter` resolves a `DrawingRouteDecision`; `DrawingPipelineExecutor` dispatches to the pipeline.

---

## 1. Extend classification

**File:** `Services/Analysis/PartModelKind.cs`

```csharp
public enum PartModelKind
{
    BentSheetMetal,
    FlatPlate,
    Cylindrical,
    ImportedGeometry,
    LoftedBends,
    YourNewKind   // add
}
```

**File:** `Services/Analysis/PartModelAnalyzer.cs` (+ scanner / EST as needed)

- Add feature/heuristic detection in `PartGeometryScanner` / analyzer decision tree
- Set `Kind = PartModelKind.YourNewKind`
- Extend `PartAnalysisResult` if new flags are required (also update `Clone()`)
- Optionally register EST Name rules in `EstNameRegistry`
- Optionally add catalog overrides in `EstCatalogRouteTable`

Document rules in [Part classification](../drawing/part-classification.md) and [EST name catalog](../analysis/est-name-catalog.md).

---

## 2. Add pipeline id

**File:** `Services/Drawing/Routing/DrawingPipelineId.cs`

```csharp
YourNewKind = 7
```

Update `DrawingRouteDecision.PipelineLabel` and `DrawingPipelineRouter.MapKindToPipeline`.

---

## 3. Create pipeline class

**File:** `Services/Drawing/YourNewDrawingPipeline.cs` (or domain folder like `LoftedBends/`)

```csharp
internal static class YourNewDrawingPipeline
{
    public static void Process(
        ISldWorks swApp,
        IModelDoc2 drawingModel,
        string partPath,
        PartAnalysisResult analysis,
        DrawingRouteDecision route,
        Action<string> log)
    {
        var drawing = (IDrawingDoc)drawingModel;
        DrawingPipelineShared.DeleteExistingViews(...);
        DrawingPipelineShared.CreateStandardThreeViews(...);
        // Fourth view if needed
        ApplyDimensions(...);
        DrawingDimensionDeduper.RemoveDuplicateDimensions(...);
        DrawingPipelineShared.AutoArrangeDimensions(...);
        DrawingPipelineShared.AdjustSheetScaleIfNeeded(...);
        drawingModel.ForceRebuild3(true);
    }
}
```

Reuse [shared drawing services](../drawing/shared-drawing-services.md) wherever possible.

**Contract:** every pipeline `Process` takes `(swApp, drawingModel, partPath, analysis, route, log)`.

---

## 4. Wire executor

**File:** `Services/Drawing/Routing/DrawingPipelineExecutor.cs`

```csharp
case DrawingPipelineId.YourNewKind:
    YourNewDrawingPipeline.Process(swApp, drawingModel, partPath, analysis, route, log);
    break;
```

Do **not** add a kind `switch` in `SheetMetalDrawingService` — it already calls router + executor.

---

## 5. Add annotation modules

| Approach | When |
| --- | --- |
| New `SmartDim/Modules/SmartDim*.cs` | Shared orthographic dims |
| New domain folder (`ArcSector/`, `BafflePlate/`, …) | Distinct geometry family |
| Flat-plate sub-kind | Nested under P-01: enum + classifier + `FlatPlateSubKindResolver` + `FlatPlateDimRouter` |

### Adding a P-01 sub-kind (checklist)

1. Add value to `FlatPlateSubKind`.
2. Detect in `FlatPlateClassifier` and/or `*ViewAnalyzer` (+ property tokens if needed).
3. Wire resolve override in `FlatPlateSubKindResolver` (order vs Generic/Baffle/Flange).
4. Implement dim pipeline (prefer one orchestrator + branch files, like `ArcSector/`).
5. Call from `FlatPlateDimRouter.ApplyForView`.
6. Document in [flat-plate-subkinds.md](../drawing/flat-plate-subkinds.md) and a module page under `Docs/modules/`.

Use `SmartDimHelper` for selection and session dedupe. Pass `Action<string> log` into modules (no `Console.WriteLine`).

---

## 6. Tests (no SOLIDWORKS required)

Add cases under `SolidWorksTester.Tests`:

- `EstNameRegistry` / `EstCatalogRouteTable` for new catalog ids
- `DrawingPipelineRouter.Resolve` for the new kind / override

---

## 7. Document

Create `Docs/drawing/pipeline-your-new-kind.md` (or `Docs/modules/…` for a P-01 sub-kind) and link from:

- [Pipelines overview](../drawing/pipelines-overview.md)
- [Flat-plate sub-kinds](../drawing/flat-plate-subkinds.md) when nested under P-01
- [Pipeline router](../drawing/pipeline-router.md)
- [Documentation hub](../README.md)

---

## 8. Release notes

If users see new behaviour, add `Docs/ReleaseNotes/vX.Y.ZZ.md` section.

---

## 9. Verify

- [ ] `dotnet build SolidWorksTester.sln -c Release`
- [ ] `dotnet test SolidWorksTester.sln -c Release`
- [ ] Test with representative parts in SOLIDWORKS
- [ ] Re-run publish if embedding new release notes
- [ ] Confirm no UAC/COM regression

---

## See also

- [Architecture overview](../architecture/overview.md)
- [Pipeline router](../drawing/pipeline-router.md)
- [SmartDim modules](../smartdim/modules.md)
- [Conventions](conventions.md)
