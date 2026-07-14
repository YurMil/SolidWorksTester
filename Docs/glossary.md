# Glossary

[← Documentation hub](../README.md)

---

## Product terms

| Term | Definition |
| --- | --- |
| **Sheet Metal Drawing Generator** | Official application title (`AppMetadata.ApplicationTitle`) |
| **Pipeline** | End-to-end drawing creation strategy for a `PartModelKind` (P-01, P-02, P-03) |
| **SmartDim** | Automated dimensioning subsystem (modules A–G) |
| **Batch run** | Single user action processing all listed `.SLDPRT` files |
| **Template** | `.DRWDOT` (new drawing) or `.SLDDRW` (copied layout) |

---

## View names

| Name | Typical content |
| --- | --- |
| `Drawing View1` | Front orthographic |
| `Drawing View2` | Top orthographic |
| `Drawing View3` | Right orthographic |
| `Drawing View4` | Isometric (P-01, P-03) **or** flat pattern (P-02) |

**Warning:** View4 name is overloaded — always consider pipeline context.

---

## Part kinds

| `PartModelKind` | Description |
| --- | --- |
| `FlatPlate` | Sheet metal without bends, or non-sheet flat stock |
| `BentSheetMetal` | Sheet metal with ≥ 1 bend feature |
| `Cylindrical` | Dominant cylindrical / revolved geometry |

---

## Analysis flags

| Flag | Meaning |
| --- | --- |
| `IsRoundFlatProfile` | Disc-like flat plate (round OD) |
| `IsHollow` | Tube/pipe (inner + outer radius) |
| `HasSheetMetalFeature` | Sheet metal feature in tree |
| `BendFeatureCount` | Count of bend-type features |

---

## COM / API terms

| Term | Definition |
| --- | --- |
| **ROT** | Running Object Table — registry for active COM objects |
| **ProgID** | `SldWorks.Application` programmatic identifier |
| **RCW** | Runtime Callable Wrapper — .NET proxy for COM |
| **MMGS** | mm–g–second unit system on drawing |
| **Silent open** | `swOpenDocOptions_Silent` — no file open dialogs |

---

## File extensions

| Extension | Role |
| --- | --- |
| `.SLDPRT` | SOLIDWORKS part (input) |
| `.SLDDRW` | SOLIDWORKS drawing (output) |
| `.DRWDOT` | Drawing template (input) |

---

## UI controls

| Control | Purpose |
| --- | --- |
| `ThemedTextField` | Template path input |
| `ThemedLogView` | Batch log output |
| `ThemedButton` | Primary/secondary actions |

---

## Abbreviations

| Abbr. | Meaning |
| --- | --- |
| OD | Outer diameter |
| ID | Inner diameter |
| bbox | Bounding box |
| COM | Component Object Model |
| STA | Single-threaded apartment (WinForms) |

---

## See also

- [Pipelines overview](drawing/pipelines-overview.md)
- [Part classification](drawing/part-classification.md)
- [SmartDim modules](smartdim/modules.md)
