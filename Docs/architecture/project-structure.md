# Project structure

[← Documentation hub](../README.md) · [Architecture overview](overview.md)

## Source tree (logical)

```
SolidWorksTester/
├── Program.cs / MainForm.cs
├── SolidWorksTester.csproj
├── SolidWorksTester.sln
├── SolidWorksTester.Tests/     Unit tests (no SOLIDWORKS)
├── publish.bat
│
├── Services/
│   ├── SolidWorksConnection.cs / SheetMetalDrawingService.cs
│   ├── Analysis/               Kind, scanner, EST registry, classification
│   └── Drawing/
│       ├── *DrawingPipeline.cs
│       ├── FlatPlate/          Sub-kind dim router
│       └── Routing/            Router, Executor, EstCatalogRouteTable
│
├── SmartDim/                   SmartDimHelper partials + constants
│   └── Modules/                SmartDimOverall … FlatBendLines (A–G)
├── RoundFlatPlate/
├── FlangeGasket/
├── BafflePlate/
├── Cylindrical/
├── LoftedBends/
├── Imported/
├── UI/
└── Docs/
```

---

## Namespaces

| Namespace | Contents |
| --- | --- |
| `SolidWorksTester` | `Program`, `MainForm`, SmartDim modules A–G |
| `SolidWorksTester.Services` | Connection, drawing service |
| `SolidWorksTester.Services.Analysis` | Classification, EST registry |
| `SolidWorksTester.Services.Drawing` | Pipelines, shared utilities |
| `SolidWorksTester.Services.Drawing.Routing` | Router / Executor / route table |
| `SolidWorksTester.Services.Drawing.FlatPlate` | Flat-plate sub-routing |
| `SolidWorksTester.SmartDim` | `SmartDimConstants`, helper partials |
| `SolidWorksTester.BafflePlate` / `FlangeGasket` / `LoftedBends` / … | Domain modules |
| `SolidWorksTester.UI.*` | Theme, controls, layout, forms |

---

## Core types

### Orchestration

| Type | Responsibility |
| --- | --- |
| `SheetMetalDrawingService` | Template → analyze → route → save |
| `DrawingPipelineRouter` | `PartAnalysisResult` → `DrawingRouteDecision` |
| `DrawingPipelineExecutor` | Dispatch to `*DrawingPipeline.Process` |

### Pipelines (`DrawingPipelineId`)

| Id | Pipeline |
| --- | --- |
| P-01 | `FlatPlateDrawingPipeline` |
| P-02 | `BentSheetMetalDrawingPipeline` |
| P-03 | `CylindricalDrawingPipeline` |
| P-04 | `ImportedGeometryDrawingPipeline` |
| P-05 | `LoftedBendsDrawingPipeline` |
| P-00 | `GenericFallbackDrawingPipeline` |

### Dimensioning

| Type | Location |
| --- | --- |
| `SmartDimHelper` | `SmartDim/*.cs` |
| Modules A–G | `SmartDim/Modules/*.cs` |
| Domain modules | `RoundFlatPlate/`, `BafflePlate/`, `FlangeGasket/`, `Cylindrical/`, `LoftedBends/`, `Imported/` |

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
- [Adding a pipeline](../development/adding-a-pipeline.md)
- [Conventions](../development/conventions.md)
