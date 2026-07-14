# Sheet Metal Drawing Generator

WinForms utility for batch-creating SOLIDWORKS sheet-metal drawings (`.SLDDRW`) from part files (`.SLDPRT`) using the SOLIDWORKS COM API.

**Version:** 0.0.01

## Features

- Automatic part classification: flat plate, bent sheet metal, cylindrical
- Standard orthographic views + isometric or flat pattern
- Smart dimensioning (overall, thickness, holes, bends, cutouts)
- Round flat plate and cylindrical annotation modules
- Modern minimalist UI with in-app Markdown release notes

## Requirements

| Component | Version |
| --- | --- |
| Windows | x64 |
| .NET SDK | 9.0+ (build) / bundled in published EXE |
| SOLIDWORKS | 2024 recommended (Interop 32.1.0) |
| WebView2 Runtime | For release-notes viewer (usually preinstalled on Windows 11) |

## Quick start

```powershell
git clone https://github.com/YurMil/SolidWorksTester.git
cd SolidWorksTester
dotnet build -c Release
dotnet run -c Release
```

Run SOLIDWORKS at the **same privilege level** as this app (both normal user or both admin).

## Publish standalone EXE

```powershell
.\publish.bat
```

Produces a self-contained single-file `SolidWorksTester.exe` in the project root (Release, win-x64).

Publish profile: `Properties/PublishProfiles/StandaloneWin64.pubxml`

## Documentation

Developer documentation: [`Docs/README.md`](Docs/README.md)

End-user release notes: [`Docs/ReleaseNotes/`](Docs/ReleaseNotes/)

## Repository layout

```
SolidWorksTester/
├── Services/          COM connection, analysis, drawing pipelines
├── SmartDim/          Dimensioning helper + modules A–G
├── RoundFlatPlate/    Round disc annotations
├── Cylindrical/       Pipe / cylinder annotations
├── UI/                WinForms shell, theme, release-notes viewer
├── Docs/              Developer + user documentation
└── publish.bat        Standalone EXE publish script
```

## License

Proprietary — contact the repository owner for usage terms.
