# Part classification via Custom Properties

Optional override layer applied **after** geometry analysis in `PartModelAnalyzer`.

## Priority

1. **Explicit CADAS keys** — `CADAS_PartKind`, `CADAS_FlatPlateSubKind`
2. **EST configuration `Name`** — PIPE, PLATE, FLANGE, ELBOW, …
3. **Geometry fallback** — if properties missing or not trusted

Configuration properties override file-level properties for the same key.

## EST template properties (Configuration)

| Property | Role | Example (PIPE) | Example (PLATE) |
|----------|------|----------------|-----------------|
| `Name` | Part family → pipeline | `PIPE` | `PLATE` |
| `Description` | Evaluated title | `PIPE - D60.3 x 6.3 x L182` | `PLATE - 6 x 100 x 200` |
| `DIM1` | OD / thickness | `60.3` (D@Sketch1) | `6` (sheet thickness) |
| `DIM2` | — / width | — | `100` |
| `DIM3` | wall / length | `6.3` (T@Sketch1) | `200` |
| `Length` | pipe length | `182` (L@Sketch3) | — |

Evaluated values from SOLIDWORKS `CustomPropertyManager.Get4` (resolved text) are stored in  
`PartAnalysisResult.EstProperties` for benchmark comparison and future smart-dim validation.

### Name → pipeline mapping

See full catalog: [est-name-catalog.md](est-name-catalog.md)

Priority: `CADAS_PartKind` > **EST `Name` catalog** > geometry.

## Explicit CADAS keys (optional override)

| Key aliases | Values |
|-------------|--------|
| `CADAS_PartKind`, `DrawingPartType` | `FlatPlate`, `BentSheetMetal`, `Cylindrical`, … |
| `CADAS_FlatPlateSubKind` | `FlangeGasket`, `RoundDisc`, … |

## Fallback

If properties conflict with geometry → geometry wins.  
Log: `Property classification not trusted`.

## Benchmark

`DrawingTestBench audit` exports `Classification.Est` and `Quality.Flags` for each part/drawing pair.

### Quality flags (PIPE example)

| Flag | Meaning |
|------|---------|
| `missing_od` | No Ø/DIM1 on drawing (±0.5 mm or 1.5%) |
| `wrong_od` | Diameter on drawing, but not matching DIM1 |
| `missing_wall_thickness` | No linear dim matching DIM3 |
| `wrong_wall_thickness` | Closest wall dim ≠ DIM3 |
| `missing_length` / `wrong_length` | Length vs EST `Length` |

Plate: `missing_thickness`, `wrong_width`, `missing_plate_length`, …

After generation, cylindrical and flat-plate pipelines log the same check as  
`Checking EST dimension quality...`
