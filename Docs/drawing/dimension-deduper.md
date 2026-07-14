# Dimension deduper

[← Documentation hub](../README.md) · [Pipelines overview](pipelines-overview.md)

**Class:** `DrawingDimensionDeduper`  
**File:** `Services/Drawing/DrawingDimensionDeduper.cs`

---

## Purpose

After all SmartDim modules run, the same physical size may appear as duplicate dimensions in multiple views (e.g. thickness in front and right). The deduper removes **value/text duplicates** while keeping the first occurrence.

---

## Algorithm (summary)

1. Walk views in order: View1 → View2 → View3 → (View4 unless excluded).
2. For each view, enumerate display dimensions.
3. Build a key from normalised dimension text / value (tolerance from `SmartDimConstants.DimensionValueToleranceMeters`).
4. If key already seen → delete duplicate dimension.
5. Log count of removed duplicates.

**Order rule:** First occurrence wins — typically View1 retains the dimension.

---

## Pipeline-specific exclusions

| Pipeline | `excludeViewName` parameter |
| --- | --- |
| P-01 Flat plate | `"Drawing View4"` (isometric) |
| P-02 Bent sheet metal | none (flat pattern is dimensioned) |
| P-03 Cylindrical | `"Drawing View4"` (isometric) |

Flat plate call:

```csharp
DrawingDimensionDeduper.RemoveDuplicateDimensions(
    drawingModel, drawing, log, SmartDimConstants.IsometricViewName);
```

---

## Relationship to session dedupe

| Mechanism | Scope | Prevents |
| --- | --- | --- |
| `SmartDimHelper.DimensionedFeatures` | Single drawing pass | Re-dimensioning same model edge/face |
| `DrawingDimensionDeduper` | Post-pass across views | Identical values shown twice |

Both are needed — session set does not catch cross-view duplicates with different selections.

---

## See also

- [SmartDim overview](../smartdim/overview.md)
- [Units & coordinates](../solidworks-api/units-and-coordinates.md)
