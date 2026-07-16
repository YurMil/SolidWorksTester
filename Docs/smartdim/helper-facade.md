# SmartDimHelper facade

[← SmartDim overview](overview.md)

**Class:** `SmartDimHelper` (partial)  
**Namespace:** `SolidWorksTester` (partial files under `SmartDim/`)

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
| `SmartDimHelper.Selection.cs` | Edge/face/sketch selection, `SelectEdgeAtPoint` |
| `SmartDimHelper.ViewEntities.cs` | Edges/vertices in a view, centerline count |
| `SmartDimHelper.ViewCache.cs` | Per-view edge cache |
| `SmartDimHelper.EdgeGeometry.cs` | Direction, length, circles, sheet transforms |
| `SmartDimHelper.FaceGeometry.cs` | Face normals, circular face detection |
| `SmartDimHelper.Features.cs` | Feature association from entities |
| `SmartDimHelper.Dimensions.cs` | **Create** linear/angular/diameter + center mark |
| `SmartDimHelper.Dimensions.Query.cs` | Value lookup, annotation walk, `DisplayDimensionEntry` |
| `SmartDimHelper.Dimensions.Text.cs` | Parentheses, `Nx` / linear prefixes |
| `SmartDimHelper.Dimensions.Delete.cs` | Delete by diameter / linear / angular filters |

---

## Key public surface (conceptual)

| Member | Purpose |
| --- | --- |
| `DimensionedFeatures` | Session dedupe set |
| `SuppressDimInput()` / `RestoreDimInput()` | Disable dim value dialog during batch |
| Create* / Has* / Delete* | Display-dimension lifecycle |
| View entity iterators | Edges, circular edges |
| Geometry helpers | Parallel edges, hole circles, arc Max conditions |

Modules call helpers — they do not duplicate common COM patterns.

---

## Dimension input suppression

Uses `IModelDoc2.SetUserPreferenceToggle` during automation. **Always** restored in `finally`.

---

## Constants

From `SmartDimConstants`: sheet orientation tol, dimension value tol, isometric view name, select-type tokens.

---

## See also

- [SmartDim modules](modules.md)
- [API reference — selection](../solidworks-api/api-reference-by-area.md#selection--dimensions)
