# Round flat plate module

[← Documentation hub](../README.md) · [Flat plate pipeline (P-01)](../drawing/pipeline-flat-plate.md)

Specialised annotation for **circular flat sheet** parts (discs, round plates) and **rounded-end / segment** plates without bends.

**Folder:** `RoundFlatPlate/`

Related: annular **sector** plates (two concentric arcs) live in **`ArcSector/`** — see [arc-sector-plate.md](arc-sector-plate.md). Do not conflate with `RoundDisc` or `RoundedEnd`.

---

## Activation

Round mode is enabled when **either**:

1. **Analysis flag:** `PartAnalysisResult.IsRoundFlatProfile` (bbox heuristic in `PartModelAnalyzer.IsRoundFlatDisc`)
2. **Drawing detection:** `RoundFlatPlateViewAnalyzer.DetectFromDrawing(dimHelper, drawing)` finds a primary circular face view

---

## Components

| Class | File | Role |
| --- | --- | --- |
| `RoundFlatPlateViewAnalyzer` | `RoundFlatPlateViewAnalyzer.cs` | Detect circular profile from drawing views |
| `RoundFlatPlateDimensions` | `RoundFlatPlateDimensions.cs` | OD, centerlines, holes per view; orchestrates thickness |
| `RoundFlatPlateThickness` | `RoundFlatPlateThickness.cs` | Side-view thickness dimension |

---

## Per-view dimensions (`AddForView`)

On each orthographic view (not isometric):

- **Outer diameter (OD)** — radial dim on outer circular edge
- **Centerlines** — mark disc center / axis
- **Holes** — via `SmartDimHoles` with `excludeDiameter` = outer OD so the profile circle is not double-dimensioned

---

## Thickness (`AddThicknessOnce`)

Called **once** after all views — not per view.

Strategies (in order of attempt):

1. Parallel edge pair on side/edge view (same as `SmartDimThickness`)
2. Shortest visible edge heuristic
3. Extreme vertex pair across thickness direction
4. Sheet metal API read from `BaseFlange` / sheet metal feature

Skips views named `Drawing View4` (isometric).

**Known test cases:** EST-P61261 (PLATE 5×D84), EST-P61339.

---

## Interaction with standard SmartDim

When round mode is active, modules A–E are **not** called per view. Thickness module B is replaced entirely by `RoundFlatPlateThickness`.

---

## See also

- [Flat-plate sub-kinds](../drawing/flat-plate-subkinds.md)
- [Arc-sector plate](arc-sector-plate.md) — concentric sector (different sub-kind)
- [Part classification](../drawing/part-classification.md)
- [SmartDim modules](../smartdim/modules.md)
- [Units & coordinates](../solidworks-api/units-and-coordinates.md)
