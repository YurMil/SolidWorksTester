# Baffle plate drawing pipeline

Dedicated nested flat-plate route for sheet-metal **baffle / tube-sheet** plates with dense hole arrays
(reference: EST-P91112, drawing `EST-P91112_r01_BAFFLE PLATE 3 - 6 x D2375`).

## Detection

| Signal | Rule |
|--------|------|
| Geometry | Thin flat plate + ≥40 similar small cylindrical holes, or linear/fill/sketch pattern |
| EST Name | `BAFFLE PLATE`, `BAFFLE`, fuzzy `BAFFLE` / `TUBE SHEET` |
| Description | Contains `BAFFLE` or `TUBE SHEET` (overrides generic `PLATE`) |
| Anti-flange | Flange detector skips when dense hole count ≥ 40; circular pattern = `CirPattern` only |

Classification uses a **single** COM face/feature scan (`PartGeometryScanner` → `PartGeometrySnapshot`); baffle/flange/rounded-end are pure functions over that snapshot.

Sub-kind: `FlatPlateSubKind.BafflePlate`  
Catalog id: `baffle_plate`

## Dimension strategy (v1)

Matches EST practice on the main sheet: **do not** place hundreds of hole Ø callouts.

1. Sheet format for large baffles: switch **EST_A1L / EST_A2L** `.slddrt` when the file exists under the active formats folder (or `C:\EST\91_SW Setup\Sheet Formats`); otherwise keep the template border. Views are created/relayout on the new sheet size.
2. Three orthographic views only — **no isometric** (HLV turns dense holes into a black blot)
3. Import Mark-for-Drawing model dimensions (radii, tab angles/widths, fillets)
4. Dedupe + noise filter (Move Face / sub-mm)
5. Side/profile: thickness via outer-rim edges + `SelectEntity` (value-validated)
6. Family notes: «No sharp edge», «Mark 0°/90°…», «Full hole pattern in drawing …»
7. Mass property units forced to **kg** (MMGS otherwise shows grams next to a kg stamp)
8. **Hole table** on primary view (`InsertHoleTable2`, sizes-combined template) — ordinate alternative

**Skip** `SmartDimHoles` / hole positions / flange BCD qty callouts.

Target format still outstanding for later iterations (P2–P4):
- Ordinate sets for hole columns/rows (optional upgrade over hole table)
- Detail A (pitch + 60° + representative Ø)
- Section B-B for chamfer / Ra / qty on holes
- Cosmetic reposition of imported dims outside the perforation field

## Code

- Analyzer: `Services/Analysis/BafflePlate/BafflePlateModelAnalyzer.cs`
- Pipeline: `BafflePlate/BafflePlateDimensionPipeline.cs`
- Thickness: `BafflePlate/BafflePlateThickness.cs` (outer-rim + SelectEntity)
- Hole table: `BafflePlate/BafflePlateHoleTable.cs` (`InsertHoleTable2`)
- Notes: `BafflePlate/BafflePlateNotes.cs`
- Sheet A1/scale: `Services/Drawing/DrawingSheetProfile.cs`
- Mass kg: `Services/Drawing/DrawingMassUnits.cs`
- Routing: `FlatPlateSubKindResolver`, `FlatPlateDimRouter`

## See also

- [Flat-plate sub-kinds](flat-plate-subkinds.md)
- [Arc-sector plate](../modules/arc-sector-plate.md) — different nested strategy
- [Pipeline router](pipeline-router.md)
