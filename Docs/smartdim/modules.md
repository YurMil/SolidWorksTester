# SmartDim modules

[← SmartDim overview](overview.md) · [SmartDimHelper](helper-facade.md)

Modules live under `SmartDim/Modules/`. Most expose `Add(...)` or view-specific entry points and take `SmartDimHelper` + `IView`.

---

## Module A — `SmartDimOverall` (partials)

Overall width/height (rectangles, trapezoids, outline fallback).

| File | Responsibility |
| --- | --- |
| `SmartDimOverall.cs` | Entry `Add`, edge collect, orchestration |
| `SmartDimOverall.Vertical.cs` | Height (`Overall_H`), trapezoid left vertical |
| `SmartDimOverall.Horizontal.cs` | Width (`Overall_W`) |
| `SmartDimOverall.Trapezoid.cs` | Top/bottom widths + taper angle |
| `SmartDimOverall.Helpers.cs` | Boundary/arc pick, outline pick, `ApplyArcEndMax` |

`SetArcEndCondition(Index, Condition)` uses entity index **1 or 2** and Max so arc spans reach the tangent.

---

## Module B — `SmartDimThickness`

**File:** `SmartDim/Modules/SmartDimThickness.cs`

Sheet gauge/thickness (parallel edges; skips circular face views). Round / disc / arc-sector / flange / baffle often use specialised thickness helpers instead.

---

## Module C — `SmartDimHoles`

**File:** `SmartDim/Modules/SmartDimHoles.cs`

Hole diameters with quantity prefix. `AddForStandardViews` for flat pipeline; supports `excludeDiameter` for outer profile circles.

---

## Module D — `SmartDimHolePositions`

**File:** `SmartDim/Modules/SmartDimHolePositions.cs`

Linear dims from boundary edges to hole centers (Generic path). ArcSector uses its own hole-position branch.

---

## Module E — `SmartDimCutouts`

**File:** `SmartDim/Modules/SmartDimCutouts.cs`

Cutouts, slots, non-circular openings.

---

## Module F — `SmartDimBends`

**File:** `SmartDim/Modules/SmartDimBends.cs`  
**Pipeline:** P-02 orthographic views.

---

## Module G — `SmartDimFlatBendLines`

**File:** `SmartDim/Modules/SmartDimFlatBendLines.cs`  
**Pipeline:** P-02 flat pattern only.

---

## Additional flat-plate modules

| Module | File | Role |
| --- | --- | --- |
| `SmartDimFillets` | `SmartDimFillets.cs` | Corner fillet R (small arcs) |
| `SmartDimChamfers` | `SmartDimChamfers.cs` | Chamfer legs |
| `SmartDimSymmetryCenterlines` | `SmartDimSymmetryCenterlines.cs` | Bilateral symmetry → H/V `InsertCenterLine2` |

Symmetry: hole-center (or outer-edge) mirror test about bbox midlines; wired from Generic primary view in `FlatPlateDimRouter`.

---

## Pipeline matrix

| Module | P-01 Generic | P-01 Round/Arc/… | P-02 Bent (1–3) | P-02 Flat | P-03 |
| --- | --- | --- | --- | --- | --- |
| A Overall | ✓ | Domain | ✓ | — | — |
| Symmetry CLs | ✓ primary | Domain | — | — | — |
| B Thickness | ✓ | Domain | ✓ | — | — |
| Fillets/Chamfers | ✓ | Domain | — | — | — |
| C Holes | ✓ | Domain | ✓ | — | — |
| D Hole pos. | ✓ | Domain | ✓ | — | — |
| E Cutouts | ✓ | — | ✓ | — | — |
| F Bends | — | — | ✓ | — | — |
| G Flat bends | — | — | — | ✓ | — |

\*RoundDisc / RoundedEnd / ArcSector / Flange / Baffle replace Generic modules with domain folders — see [flat-plate-subkinds.md](../drawing/flat-plate-subkinds.md).

---

## Adding a new SmartDim module

1. Create `SmartDim/Modules/SmartDimYourFeature.cs`.
2. Static `Add(SmartDimHelper helper, IView view, Action<string>? log = null)`.
3. Use `DimensionedFeatures` / value checks for session dedupe.
4. Wire into `FlatPlateDimRouter` or the target pipeline.
5. Document here + [flat-plate-subkinds.md](../drawing/flat-plate-subkinds.md) if P-01 nested.

---

## See also

- [Flat plate pipeline](../drawing/pipeline-flat-plate.md)
- [Arc-sector plate](../modules/arc-sector-plate.md)
- [Helper facade](helper-facade.md)
