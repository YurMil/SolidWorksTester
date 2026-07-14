# Units & coordinates

[← Documentation hub](../README.md) · [API reference](api-reference-by-area.md)

## Internal unit system

SOLIDWORKS COM API returns **lengths in meters** regardless of the on-screen unit display.

| Quantity | API unit | Example |
| --- | --- | --- |
| Length | meters (m) | 0.005 = 5 mm |
| Angle | radians | Use `Math` conversions when comparing |

Drawing documents are set to **MMGS** (millimetre–gram–second) at the start of `ProcessDrawingModel` so dimensions display in mm on the sheet.

---

## Tolerances in code

Defined in `SmartDim/SmartDimConstants.cs`:

| Constant | Value (m) | Use |
| --- | --- | --- |
| `SheetOrientationToleranceMeters` | 0.0005 | Edge alignment, view orientation checks (~0.5 mm) |
| `DimensionValueToleranceMeters` | 0.00005 | Duplicate value comparison (~0.05 mm) |

### Part analysis thresholds (`PartModelAnalyzer`)

| Constant | Value | Meaning |
| --- | --- | --- |
| `minThickness` | 0.0005 m | Minimum disc thickness (0.5 mm) |
| `minFlatRatio` | 5.0 | Large dim / thickness for flat disc |
| `roundTolerance` | 0.06 | 6% max difference between two large bbox dims |
| Hollow detection | 0.0005 m | Min radius delta for tube/pipe |
| Small hole cylinder | 0.05 m radius | Face-based hole heuristic |

---

## Sheet layout coordinates

View insertion positions are **fractions of sheet size** (0.0–1.0), defined in `DrawingViewLayout`:

| Constant | X | Y | View |
| --- | --- | --- | --- |
| `FrontX/Y` | 0.10 | 0.18 | View1 |
| `TopX/Y` | 0.10 | 0.08 | View2 |
| `RightX/Y` | 0.20 | 0.18 | View3 |
| `FlatPatternX/Y` | 0.30 | 0.13 | View4 (bent) |
| `IsometricX/Y` | 0.30 | 0.13 | View4 (flat/cylindrical) |

After insertion, `AdjustSheetScaleIfNeeded` may reduce scale if views overflow the sheet.

---

## Bounding box (`GetPartBox`)

Returns `[xMin, yMin, zMin, xMax, yMax, zMax]` in **model space, meters**.

Round flat disc detection sorts the three edge lengths ascending:

- `dims[0]` — thickness (smallest)
- `dims[1]`, `dims[2]` — in-plane extents (should be similar for a disc)

---

## Drawing vs model space

| Space | When used |
| --- | --- |
| Model | Part analysis, feature geometry |
| Sheet | View positions (`CreateDrawViewFromModelView3` x,y) |
| View | Entity picking for dimensions (`SelectByID2` with view context) |

SmartDim helpers transform edge endpoints and face normals into view coordinates before comparing orientations.

---

## See also

- [Shared drawing services](../drawing/shared-drawing-services.md)
- [Part classification](../drawing/part-classification.md)
- [Round flat plate](../modules/round-flat-plate.md)
