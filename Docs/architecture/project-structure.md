# Project structure

[← Documentation hub](../README.md) · [Architecture overview](overview.md)

## Source tree (logical)

```
SolidWorksTester/
├── Program.cs                 Entry point [STAThread]
├── MainForm.cs                Batch UI orchestration
├── publish.bat                Publish standalone EXE
├── SolidWorksTester.csproj    Build, packages, embedded docs
│
├── Services/
│   ├── SolidWorksConnection.cs
│   ├── SheetMetalDrawingService.cs
│   ├── Analysis/
│   │   ├── PartModelKind.cs
│   │   └── PartModelAnalyzer.cs
│   └── Drawing/
│       ├── FlatPlateDrawingPipeline.cs
│       ├── BentSheetMetalDrawingPipeline.cs
│       ├── CylindricalDrawingPipeline.cs
│       ├── DrawingPipelineShared.cs
│       ├── DrawingViewLayout.cs
│       └── DrawingDimensionDeduper.cs
│
├── SmartDim/                  SmartDimHelper partial class
├── SmartDimOverall.cs … SmartDimFlatBendLines.cs   Modules A–G
├── RoundFlatPlate/            Flat disc specialisation
├── Cylindrical/               Pipe / cylinder annotations
│
├── UI/
│   ├── AppMetadata.cs
│   ├── AppReleaseNotes.cs
│   ├── Theme/
│   ├── Controls/
│   ├── Layout/
│   ├── Views/
│   ├── Forms/
│   └── Documentation/
│
└── Docs/                      This documentation + ReleaseNotes/
```

---

## Namespaces

| Namespace | Contents |
| --- | --- |
| `SolidWorksTester` | `Program`, `MainForm`, SmartDim modules A–G |
| `SolidWorksTester.Services` | Connection, drawing service |
| `SolidWorksTester.Services.Analysis` | Classification |
| `SolidWorksTester.Services.Drawing` | Pipelines, shared drawing utilities |
| `SolidWorksTester.SmartDim` | `SmartDimConstants`, helper partials |
| `SolidWorksTester.Cylindrical` | Cylindrical annotation modules |
| `SolidWorksTester.RoundFlatPlate` | Round disc modules |
| `SolidWorksTester.UI` | Metadata, release banner |
| `SolidWorksTester.UI.*` | Theme, controls, layout, forms, docs viewer |

Root namespace in `.csproj`: `SolidWorksTester`.

---

## Core types reference

### Entry & orchestration

| Type | File | Responsibility |
| --- | --- | --- |
| `Program` | `Program.cs` | `Application.Run(new MainForm())` |
| `MainForm` | `MainForm.cs` | Batch loop, cancellation, UI callbacks |
| `MainFormView` | `UI/Views/MainFormView.cs` | Control references from layout builder |
| `SheetMetalDrawingService` | `Services/SheetMetalDrawingService.cs` | Template → drawing → pipeline → save |
| `SolidWorksConnection` | `Services/SolidWorksConnection.cs` | COM attach / launch |

### Analysis

| Type | File |
| --- | --- |
| `PartModelKind` | `Services/Analysis/PartModelKind.cs` |
| `PartAnalysisResult` | `Services/Analysis/PartModelAnalyzer.cs` |
| `PartModelAnalyzer` | `Services/Analysis/PartModelAnalyzer.cs` |

### Drawing pipelines

| Type | File | Pipeline ID |
| --- | --- | --- |
| `FlatPlateDrawingPipeline` | `Services/Drawing/FlatPlateDrawingPipeline.cs` | P-01 |
| `BentSheetMetalDrawingPipeline` | `Services/Drawing/BentSheetMetalDrawingPipeline.cs` | P-02 |
| `CylindricalDrawingPipeline` | `Services/Drawing/CylindricalDrawingPipeline.cs` | P-03 |
| `DrawingPipelineShared` | `Services/Drawing/DrawingPipelineShared.cs` | Shared |
| `DrawingDimensionDeduper` | `Services/Drawing/DrawingDimensionDeduper.cs` | Post-process |

### Dimensioning

| Type | Location |
| --- | --- |
| `SmartDimHelper` | `SmartDim/*.cs` (partial) |
| `SmartDimOverall` … `SmartDimFlatBendLines` | Project root `.cs` files |
| `RoundFlatPlate*` | `RoundFlatPlate/*.cs` |
| `CylindricalDim*` | `Cylindrical/*.cs` |

### UI metadata

| Symbol | File | Value |
| --- | --- | --- |
| `AppMetadata.ApplicationTitle` | `UI/AppMetadata.cs` | Window title |
| `AppMetadata.Version` | same | Semantic version string |
| `AppMetadata.DefaultTemplatePath` | same | Default `.DRWDOT` path |

---

## Build outputs

| Output | Location | Notes |
| --- | --- | --- |
| Debug build | `bin/Debug/net9.0-windows/` | Requires .NET 9 runtime |
| Release build | `bin/Release/net9.0-windows/` | Same |
| Published EXE | Project root `SolidWorksTester.exe` | Self-contained, single-file, win-x64 |

---

## See also

- [Data flow](data-flow.md)
- [Getting started](../development/getting-started.md)
- [Conventions](../development/conventions.md)
