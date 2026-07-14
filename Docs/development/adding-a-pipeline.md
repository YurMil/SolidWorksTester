# Adding a pipeline

[← Documentation hub](../README.md) · [Pipelines overview](../drawing/pipelines-overview.md)

Step-by-step guide for supporting a new part family (e.g. weldments, assemblies).

---

## 1. Extend classification

**File:** `Services/Analysis/PartModelKind.cs`

```csharp
public enum PartModelKind
{
    BentSheetMetal,
    FlatPlate,
    Cylindrical,
    YourNewKind   // add
}
```

**File:** `Services/Analysis/PartModelAnalyzer.cs`

- Add feature/heuristic detection
- Set `Kind = PartModelKind.YourNewKind` in the decision tree
- Extend `PartAnalysisResult` if new flags needed
- Log human-readable classification line

Document rules in [Part classification](../drawing/part-classification.md).

---

## 2. Create pipeline class

**File:** `Services/Drawing/YourNewDrawingPipeline.cs`

```csharp
internal static class YourNewDrawingPipeline
{
    public static void Process(
        ISldWorks swApp,
        IModelDoc2 drawingModel,
        string partPath,
        PartAnalysisResult analysis,
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

---

## 3. Wire dispatch

**File:** `Services/SheetMetalDrawingService.cs`

```csharp
case PartModelKind.YourNewKind:
    YourNewDrawingPipeline.Process(swApp, model, partPath, analysis, log);
    break;
```

---

## 4. Add annotation modules

Choose one:

| Approach | When |
| --- | --- |
| New `SmartDim*.cs` module | Sheet-metal-like dims on orthographic views |
| New subfolder (like `Cylindrical/`) | Distinct geometry domain |
| Extend existing module | Small variant of current behaviour |

Use `SmartDimHelper` for selection and session dedupe.

---

## 5. Document

Create `Docs/drawing/pipeline-your-new-kind.md` and link from:

- [Pipelines overview](../drawing/pipelines-overview.md)
- [Documentation hub](../README.md)

---

## 6. Release notes

If users see new behaviour, add `Docs/ReleaseNotes/vX.Y.ZZ.md` section.

---

## 7. Verify

- [ ] Build Release x64
- [ ] Test with representative parts
- [ ] Re-run publish if embedding new release notes
- [ ] Confirm no UAC/COM regression

---

## See also

- [Architecture overview](../architecture/overview.md)
- [SmartDim modules](../smartdim/modules.md)
- [Conventions](conventions.md)
