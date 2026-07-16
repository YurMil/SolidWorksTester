# Sheet Metal Drawing Generator — Developer Documentation

| Field | Value |
| --- | --- |
| Product | Sheet Metal Drawing Generator |
| Assembly | `SolidWorksTester` |
| Target | .NET 9 WinForms (`net9.0-windows`, x64) |
| SOLIDWORKS API | Interop 32.1.0 (SOLIDWORKS 2024) |
| Current version | 0.0.01 |

This folder is the **engineering documentation hub** for the codebase. It is separate from end-user [release notes](ReleaseNotes/README.md) shown inside the application.

---

## Quick start

| I want to… | Read |
| --- | --- |
| Understand the big picture | [Architecture overview](architecture/overview.md) |
| Connect to SOLIDWORKS safely | [COM connection](solidworks-api/com-connection.md) |
| See how parts are classified | [Part classification](drawing/part-classification.md) |
| Trace a drawing from part to `.SLDDRW` | [Data flow](architecture/data-flow.md) |
| Add a new part type / pipeline | [Adding a pipeline](development/adding-a-pipeline.md) |
| Build and ship the EXE | [Packaging & deployment](ui/packaging-and-deployment.md) |
| Bump version and release notes | [Version & release](development/version-and-release.md) |

---

## Documentation map

### Architecture

- [Overview](architecture/overview.md) — layers, responsibilities, extension seams
- [Project structure](architecture/project-structure.md) — folders, namespaces, key types
- [Data flow](architecture/data-flow.md) — batch run, analysis, pipeline dispatch, save

### SOLIDWORKS COM / API

- [COM connection](solidworks-api/com-connection.md) — attach, launch, UAC, ghost processes
- [Version router (2022–2026)](solidworks-api/version-router.md) — discovery, interop baseline, capability flags
- [API version matrix](solidworks-api/versions/version-matrix.md) — per-year API nuances
- [API reference by area](solidworks-api/api-reference-by-area.md) — methods grouped by workflow
- [Units & coordinates](solidworks-api/units-and-coordinates.md) — meters, sheet space, tolerances

### Drawing automation

- [Pipelines overview](drawing/pipelines-overview.md) — P-01…P-05 comparison
- [Flat plate pipeline (P-01)](drawing/pipeline-flat-plate.md)
- [Flat-plate sub-kinds (traceability)](drawing/flat-plate-subkinds.md) — Generic → ArcSector matrix
- [Baffle plate pipeline](drawing/baffle-plate-pipeline.md)
- [Bent sheet metal pipeline (P-02)](drawing/pipeline-bent-sheet-metal.md)
- [Cylindrical pipeline (P-03)](drawing/pipeline-cylindrical.md)
- [Part classification](drawing/part-classification.md) — `PartModelAnalyzer`, feature sets, thresholds
- [Pipeline router](drawing/pipeline-router.md)
- [Shared drawing services](drawing/shared-drawing-services.md) — views, scale, arrange
- [Dimension deduper](drawing/dimension-deduper.md) — cross-view duplicate removal

### Smart dimensioning

- [SmartDim overview](smartdim/overview.md) — modules, session dedupe
- [SmartDimHelper facade](smartdim/helper-facade.md) — partial classes (incl. Dimensions.*)
- [SmartDim modules](smartdim/modules.md) — Overall partials, symmetry CLs, A–G

### Specialised modules

- [Round flat plate](modules/round-flat-plate.md) — disc / rounded-end
- [Arc-sector plate](modules/arc-sector-plate.md) — concentric arcs, R_in/R_out, strip, holes
- [Cylindrical annotations](modules/cylindrical-annotations.md) — centerlines, OD/ID, model import

### UI & packaging

- [UI overview](ui/overview.md) — MainForm, layout builder, threading
- [Theming & controls](ui/theming-and-controls.md) — `UiTheme`, `Themed*` controls
- [In-app documentation viewer](ui/documentation-viewer.md) — Markdig + WebView2
- [Packaging & deployment](ui/packaging-and-deployment.md) — single-file EXE, embedded resources

### Development

- [Getting started](development/getting-started.md) — build, run, prerequisites
- [Conventions](development/conventions.md) — naming, views, logging, units
- [Adding a pipeline](development/adding-a-pipeline.md) — step-by-step extension guide
- [Version & release](development/version-and-release.md) — sync csproj, metadata, release notes

### Reference

- [Glossary](glossary.md) — terms, view names, configuration suffixes
- [Release notes (end-user)](ReleaseNotes/README.md) — versioned product documentation

---

## Related paths in the repository

| Path | Purpose |
| --- | --- |
| `Services/` | COM orchestration, analysis, drawing pipelines, routing |
| `SmartDim/` | Shared dimensioning facade; modules in `SmartDim/Modules/` |
| `ArcSector/` / `RoundFlatPlate/` / `BafflePlate/` / `Cylindrical/` / `LoftedBends/` / … | Domain annotation modules |
| `UI/` | WinForms shell, theme, release-notes viewer |
| `Docs/ReleaseNotes/` | User-facing Markdown per version |
| `SolidWorksTester.Tests/` | Pure unit tests (registry, router, clone, symmetry mirror) |
| `publish.bat` | Standalone EXE publish script |

---

## Document maintenance

When you change behaviour, update **both**:

1. The relevant developer doc in this tree (architecture, pipeline, API).
2. The end-user release note `ReleaseNotes/vX.Y.ZZ.md` if user-visible capability changed.

See [Version & release](development/version-and-release.md) for the full checklist.
