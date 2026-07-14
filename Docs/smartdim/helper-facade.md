# SmartDimHelper facade

[← SmartDim overview](overview.md)

**Class:** `SmartDimHelper` (partial)  
**Namespace:** `SolidWorksTester.SmartDim`

---

## Constructor dependencies

```csharp
public SmartDimHelper(ISldWorks swApp, IModelDoc2 model, IDrawingDoc drawing, IModelDocExtension modelDocExt)
```

Held for the duration of one drawing annotation pass.

---

## Partial class files

| File | Responsibility |
| --- | --- |
| `SmartDimHelper.cs` | Core fields, `DimensionedFeatures`, dim input toggle |
| `SmartDimHelper.Selection.cs` | SelectByID2 wrappers, clear selection |
| `SmartDimHelper.ViewEntities.cs` | Edges/vertices visible in a view |
| `SmartDimHelper.EdgeGeometry.cs` | Edge direction, length, parallelism |
| `SmartDimHelper.FaceGeometry.cs` | Face normals, circular face detection |
| `SmartDimHelper.Features.cs` | Feature association from entities |
| `SmartDimHelper.Dimensions.cs` | AddDimension2, radial dims, placement helpers |

---

## Key public surface (conceptual)

| Member | Purpose |
| --- | --- |
| `DimensionedFeatures` | Session dedupe set |
| `SuppressDimInput()` / `RestoreDimInput()` | Disable dim value dialog during batch |
| `TryMarkDimensioned(key)` | Returns false if already dimensioned |
| View entity iterators | Edges, circular edges, face loops |
| Geometry helpers | Parallel edges, thickness candidates, hole circles |

Modules call helpers — they do not talk to COM directly for common patterns.

---

## Dimension input suppression

Uses `IModelDoc2.SetUserPreferenceToggle` to suppress interactive dimension input during automation. **Always** restored in `finally` to avoid leaving SOLIDWORKS in a bad UI state if an exception occurs.

---

## Constants used

From `SmartDimConstants`:

- `SheetOrientationToleranceMeters`
- `DimensionValueToleranceMeters`
- `IsometricViewName`
- Select type tokens (`DRAWINGVIEW`, `FACE`, `COMPONENT`)

---

## See also

- [SmartDim modules](modules.md)
- [API reference — selection](../solidworks-api/api-reference-by-area.md#selection--dimensions)
