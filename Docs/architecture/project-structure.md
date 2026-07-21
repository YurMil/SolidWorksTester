# Project structure

[← Documentation hub](../README.md) · [Architecture overview](overview.md) · [Flat-plate sub-kinds](../drawing/flat-plate-subkinds.md)

## Source tree (logical)

```
SolidWorksTester/
├── Program.cs / MainForm.cs
├── SolidWorksTester.csproj
├── SolidWorksTester.sln
├── SolidWorksTester.Tests/          Unit tests (no SOLIDWORKS)
├── publish.bat
│
├── Services/
│   ├── SolidWorksConnection.cs / SheetMetalDrawingService.cs / ComObjectRelease.cs
│   ├── Analysis/                    Kind, scanner, EST, FlatPlateClassifier, …
│   └── Drawing/
│       ├── *DrawingPipeline.cs
│       ├── FlatPlate/               SubKindResolver, DimRouter, DimContext
│       ├── SheetLayoutNormalizer.cs
│       └── Routing/                 Router, Executor, EstCatalogRouteTable
│
├── SmartDim/                        SmartDimHelper partials + constants
│   └── Modules/                     Overall (partials), Thickness, Holes, …
│                                    SymmetryCenterlines, Fillets, Chamfers
│
├── ArcSector/                       Annular-sector plate dims (P-01 sub-kind)
├── RoundFlatPlate/                  Disc + rounded-end
├── FlangeGasket/
├── BafflePlate/
├── Cylindrical/
├── LoftedBends/
├── Imported/
├── UI/
│   ├── Controls/                    Themed* + TaskManagerView
│   ├── Models/                      BatchTaskItem / BatchTaskStatus
│   ├── Layout/ / Views/ / Theme/
└── Docs/
```

---

## Namespaces

| Namespace | Contents |
| --- | --- |
| `SolidWorksTester` | `Program`, `MainForm`, SmartDim modules |
| `SolidWorksTester.Services` | Connection, drawing service |
| `SolidWorksTester.Services.Analysis` | Classification, EST registry |
| `SolidWorksTester.Services.Drawing` | Pipelines, layout, shared utilities |
| `SolidWorksTester.Services.Drawing.Routing` | Router / Executor / route table |
| `SolidWorksTester.Services.Drawing.FlatPlate` | Flat-plate sub-routing |
| `SolidWorksTester.SmartDim` | `SmartDimConstants` |
| `SolidWorksTester.ArcSector` | Arc-sector plate module |
| `SolidWorksTester.RoundFlatPlate` / `BafflePlate` / `FlangeGasket` / `LoftedBends` / … | Domain modules |
| `SolidWorksTester.UI.*` | Theme, controls, layout, forms |

---

## Core types

### Orchestration

| Type | Responsibility |
| --- | --- |
| `SheetMetalDrawingService` | Template → analyze → route → save (part stays open until drawing done) |
| `DrawingPipelineRouter` | `PartAnalysisResult` → `DrawingRouteDecision` |
| `DrawingPipelineExecutor` | Dispatch to `*DrawingPipeline.Process` |

### Pipelines (`DrawingPipelineId`)

| Id | Pipeline |
| --- | --- |
| P-01 | `FlatPlateDrawingPipeline` (+ nested `FlatPlateSubKind`) |
| P-02 | `BentSheetMetalDrawingPipeline` |
| P-03 | `CylindricalDrawingPipeline` |
| P-04 | `ImportedGeometryDrawingPipeline` |
| P-05 | `LoftedBendsDrawingPipeline` |
| P-00 | `GenericFallbackDrawingPipeline` |

### Dimensioning

| Type | Location |
| --- | --- |
| `SmartDimHelper` | `SmartDim/SmartDimHelper*.cs` |
| Shared modules | `SmartDim/Modules/*.cs` |
| Domain modules | `ArcSector/`, `RoundFlatPlate/`, `BafflePlate/`, `FlangeGasket/`, `Cylindrical/`, `LoftedBends/`, `Imported/` |

---

## Build outputs

| Output | Location |
| --- | --- |
| Debug / Release | `bin/.../net9.0-windows/` |
| Published EXE | Project root `SolidWorksTester.exe` (self-contained win-x64) |
| Tests | `dotnet test SolidWorksTester.sln` |

---

## See also

- [Data flow](data-flow.md)
- [Flat-plate sub-kinds](../drawing/flat-plate-subkinds.md)
- [Adding a pipeline](../development/adding-a-pipeline.md)
- [Conventions](../development/conventions.md)
