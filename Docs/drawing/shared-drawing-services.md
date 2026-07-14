# Shared drawing services

[← Documentation hub](../README.md) · [Pipelines overview](pipelines-overview.md)

**Class:** `DrawingPipelineShared`  
**File:** `Services/Drawing/DrawingPipelineShared.cs`  
**Layout constants:** `DrawingViewLayout.cs`

---

## Methods

### `DeleteExistingViews`

Removes all drawing views except the sheet format view before recreating layout. Ensures idempotent re-runs on existing `.SLDDRW` files.

### `CreateStandardThreeViews`

Creates front, top, and right views from the part file path:

1. `CreateDrawViewFromModelView3(partPath, "*Front", x, y, 0)`
2. Select View1 → create top from front
3. Select View1 → create right from front
4. Rename to `Drawing View1`, `Drawing View2`, `Drawing View3`

Positions from [DrawingViewLayout](pipelines-overview.md#shared-pipeline-steps).

### `CreateIsometricView`

Inserts isometric at `IsometricX/Y`, names **`Drawing View4`** via `SmartDimConstants.IsometricViewName`.

Used by P-01 and P-03.

### `AutoArrangeDimensions`

Calls `IDrawingDoc.AutoArrangeDimensions()` — SOLIDWORKS built-in dimension tidying.

### `AdjustSheetScaleIfNeeded`

Inspects view bounding boxes vs sheet size; may reduce drawing scale so views fit. Logs scale changes.

---

## View iteration pattern

Drawing views form a linked list:

```
Sheet (GetFirstView) → View1 → View2 → View3 → View4 → ...
```

Dimension loops typically start at `GetFirstView().GetNextView()` (skip sheet).

---

## Selection pattern for derived views

```csharp
modelDocExt.SelectByID2("Drawing View1", "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
drawing.CreateDrawViewFromModelView3(partPath, "*Top", topX, topY, 0);
topView.SetName2("Drawing View2");
```

---

## See also

- [API reference — drawing views](../solidworks-api/api-reference-by-area.md#drawing-views)
- [Units & coordinates](../solidworks-api/units-and-coordinates.md)
- [Dimension deduper](dimension-deduper.md)
