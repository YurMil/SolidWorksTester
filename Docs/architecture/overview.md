# Architecture overview

[← Documentation hub](../README.md)

## Purpose

**Sheet Metal Drawing Generator** is a Windows desktop utility that:

1. Connects to a local SOLIDWORKS instance via COM.
2. Classifies each `.SLDPRT` into a drawing pipeline.
3. Creates or updates a `.SLDDRW` next to the part using a user-selected template.
4. Inserts standard views and automated dimensions/annotations.

The application is **batch-oriented**: the UI collects parts and a template, then processes them sequentially on a background thread.

---

## Layered architecture

```mermaid
flowchart TB
    subgraph UI["UI layer"]
        MF[MainForm]
        LAY[MainFormLayoutBuilder]
        RN[ReleaseNotesForm]
    end

    subgraph Services["Service layer"]
        SWC[SolidWorksConnection]
        SMS[SheetMetalDrawingService]
        ANA[PartModelAnalyzer]
    end

    subgraph Drawing["Drawing pipelines"]
        FP[FlatPlateDrawingPipeline]
        BS[BentSheetMetalDrawingPipeline]
        CY[CylindricalDrawingPipeline]
        SH[DrawingPipelineShared]
    end

    subgraph Annotations["Annotation modules"]
        SD[SmartDim modules A–G]
        RFP[RoundFlatPlate/*]
        CYL[Cylindrical/*]
    end

    MF --> SWC
    MF --> SMS
    SMS --> ANA
    SMS --> FP
    SMS --> BS
    SMS --> CY
    FP --> SH
    BS --> SH
    CY --> SH
    FP --> SD
    FP --> RFP
    BS --> SD
    CY --> CYL
```

---

## Primary extension seam

Almost all new drawing behaviour extends along this chain:

```
PartModelKind  →  *DrawingPipeline  →  SmartDim / specialised modules
```

| Layer | Stable contract | Typical change |
| --- | --- | --- |
| `PartModelKind` | Enum value | New part family (e.g. weldment) |
| `PartModelAnalyzer` | `PartAnalysisResult` | New detection heuristics |
| `*DrawingPipeline` | `Process(ISldWorks, IModelDoc2, …)` | View layout, module order |
| SmartDim / modules | `Add(SmartDimHelper, IView, …)` | New annotation type |

UI and COM connection code rarely need changes for new drawing logic.

---

## Key design decisions

| Decision | Rationale |
| --- | --- |
| **x64 process** | SOLIDWORKS is 64-bit; COM must match bitness |
| **Silent part open for analysis** | Faster batch; part closed immediately after classify |
| **Pipeline per part type** | Orthographic + flat pattern vs isometric layouts differ |
| **Drawing-only annotations** | Model is read-only; no feature edits on the part |
| **Session `DimensionedFeatures` set** | Prevents duplicate dims within one drawing pass |
| **Post-pass deduper** | Removes value/text duplicates across views |
| **MMGS unit system on drawing** | Consistent meter-based API values internally |

---

## Threading model

| Thread | Work |
| --- | --- |
| UI (STA) | WinForms message loop, file dialogs |
| `Task.Run` worker | SOLIDWORKS COM calls, batch loop |

All UI updates from the worker use `InvokeRequired` / `BeginInvoke` in `MainForm` (`AppendLog`, `UpdateStatus`, `UpdateProgress`).

**Important:** SOLIDWORKS COM calls must stay on the worker thread used for the batch; do not invoke COM from multiple threads without marshalling.

---

## External dependencies

| Package | Role |
| --- | --- |
| `SolidWorks.Interop.sldworks` | Primary COM API |
| `SolidWorks.Interop.swconst` | Enumerations and constants |
| `Markdig` | Release notes Markdown → HTML |
| `Microsoft.Web.WebView2` | In-app documentation viewer |

No WPF, no third-party UI framework — keeps the published EXE lean.

---

## See also

- [Project structure](project-structure.md)
- [Data flow](data-flow.md)
- [COM connection](../solidworks-api/com-connection.md)
- [Pipelines overview](../drawing/pipelines-overview.md)
