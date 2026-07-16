# EST Name catalog (Configuration property `Name`)

Identification by EST PDM template **Name** → pipeline routing.

Priority: `CADAS_PartKind` > **EST Name catalog** > geometry.

## Catalog reference

| EST Name | Catalog ID | Pipeline | Sub-kind | Dedicated |
|----------|------------|----------|----------|-----------|
| PIPE | `pipe` | Cylindrical | — | yes |
| PLATE | `plate` | FlatPlate | Generic | yes |
| PLATE ROUND | `plate_round` | FlatPlate | RoundDisc | yes |
| BAFFLE PLATE / BAFFLE | `baffle_plate` | FlatPlate | BafflePlate | yes |
| GASKET | `gasket` | FlatPlate | FlangeGasket | yes |
| FLANGE (BLIND, PLATE) | `flange_blind_plate` | FlatPlate | FlangeGasket | yes |
| BENDED PLATE | `bended_plate` | BentSheetMetal | — | yes |
| SHELL / SHELL WITH CUTTING | `shell` / `shell_with_cutting` | Cylindrical | — | no |
| DISHED END DIN28011/13, SS895 | `dished_end_*` | Cylindrical | — | no |
| CONE, BELLOW | `cone`, `bellow` | Cylindrical | — | no |
| RE-PAD * | `repad_*` | FlatPlate | Generic/RoundDisc | no |
| INSULATION RING/SHELL | `insulation_*` | FlatPlate/Cylindrical | — | no |
| LIFTING LUG *, NOZZLE SUPPORT * | `lifting_lug_*`, `nozzle_support_*` | FlatPlate | Generic | no |
| IPE, HEA, HEB, UNP, UPE, SHS, RHS, ANGLE, SQUARE BAR | structural ids | ImportedGeometry | — | no |
| FLAT BAR | `flat_bar` | FlatPlate | Generic | no |
| ROUND BAR | `round_bar` | Cylindrical | — | no |

`DrawingProfile` is set to **catalog id** when not overridden by `CADAS_DrawingProfile`.

Description overrides when Name is generic `PLATE`:
- `BAFFLE…` / `TUBE SHEET` → `BafflePlate` (`baffle_plate`)
- `FLANGE` / `GASKET` / `BLIND FL…` → `FlangeGasket`

Geometry: ≥40 similar small holes (or linear/fill pattern) → `BafflePlate` before flange bolt-circle heuristics.

## Log example

```
EST Name: FLANGE (BLIND, PLATE) → flange_blind_plate (pipeline: FlatPlate, sub: FlangeGasket, dedicated: True)
EST Description: BAFFLE PLATE 3 - 6 x D2375 → BafflePlate (baffle_plate)
```

Source: `Services/Analysis/EstNameRegistry.cs`, `Services/Analysis/BafflePlate/`
