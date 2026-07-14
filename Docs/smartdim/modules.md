# SmartDim modules A–G

[← SmartDim overview](overview.md) · [SmartDimHelper](helper-facade.md)

Each module is a static class with `Add(...)` or view-specific entry points. All take `SmartDimHelper` + `IView` unless noted.

---

## Module A — `SmartDimOverall`

**File:** `SmartDimOverall.cs`

Overall width and height of the part silhouette in each orthographic view. Uses extreme edges visible in the view.

---

## Module B — `SmartDimThickness`

**File:** `SmartDimThickness.cs`

Sheet gauge/thickness:

- Finds parallel edge pairs representing material thickness
- Skips **circular face views** (delegated to round plate module)
- Uses shortest-edge and vertex-extreme fallbacks

Round plates use `RoundFlatPlateThickness` instead when in round mode.

---

## Module C — `SmartDimHoles`

**File:** `SmartDimHoles.cs`

Radial dimensions on circular edges (holes). Supports `excludeDiameter` to skip the outer profile circle on round plates.

Entry: `AddForStandardViews(helper, view)` in flat pipeline.

---

## Module D — `SmartDimHolePositions`

**File:** `SmartDimHolePositions.cs`

Linear dimensions locating hole centers relative to edges or origin geometry.

---

## Module E — `SmartDimCutouts`

**File:** `SmartDimCutouts.cs`

Dimensions for cutouts, slots, and non-circular openings visible in the view.

---

## Module F — `SmartDimBends`

**File:** `SmartDimBends.cs`

**Pipeline:** P-02 orthographic views only.

Bend-related annotations where bend lines/edges are visible in standard views.

---

## Module G — `SmartDimFlatBendLines`

**File:** `SmartDimFlatBendLines.cs`

**Pipeline:** P-02 flat pattern view (`Drawing View4`) only.

Annotates bend lines on the flat pattern view from the View Palette.

---

## Pipeline matrix

| Module | P-01 Flat | P-02 Bent (1–3) | P-02 Flat pat. | P-03 Cyl. |
| --- | --- | --- | --- | --- |
| A Overall | ✓ | ✓ | — | — |
| B Thickness | ✓* | ✓ | — | — |
| C Holes | ✓ | ✓ | — | — |
| D Hole pos. | ✓ | ✓ | — | — |
| E Cutouts | ✓ | ✓ | — | — |
| F Bends | — | ✓ | — | — |
| G Flat bends | — | — | ✓ | — |

\*Round mode replaces A–E with `RoundFlatPlate*` modules.

---

## Adding a new module

1. Create `SmartDimYourFeature.cs` in project root (matches existing convention).
2. Static `Add(SmartDimHelper helper, IView view, Action<string>? log = null)`.
3. Use `TryMarkDimensioned` for session dedupe.
4. Wire into the appropriate pipeline's `ApplyDimensions` loop.
5. Document here and in [Adding a pipeline](../development/adding-a-pipeline.md).

---

## See also

- [Flat plate pipeline](../drawing/pipeline-flat-plate.md)
- [Bent sheet metal pipeline](../drawing/pipeline-bent-sheet-metal.md)
- [Round flat plate](../modules/round-flat-plate.md)
