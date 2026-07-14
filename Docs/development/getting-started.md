# Getting started

[← Documentation hub](../README.md)

---

## Prerequisites

| Requirement | Version / notes |
| --- | --- |
| Windows | x64 |
| .NET SDK | 9.0+ |
| SOLIDWORKS | 2024 recommended (interop 32.1.0) |
| Visual Studio / Cursor | Optional; CLI build supported |

---

## Clone & build

```powershell
cd "C:\cadas\SW API MD Help\Testig folder\SolidWorksTester"
dotnet build -c Release
```

Run from:

```
bin\Release\net9.0-windows\SolidWorksTester.exe
```

---

## First run checklist

1. Launch SOLIDWORKS **or** let the app start it (same UAC level as the app).
2. Confirm default template path exists or browse to your `.DRWDOT`:
   `C:\EST\91_SW Setup\Templates\DRAWING.DRWDOT`
3. Add one or more `.SLDPRT` files to the list.
4. Click **Generate** — watch the log panel.
5. Output: `{PartFolder}\{PartName}.SLDDRW`

---

## Publish standalone EXE

```powershell
.\publish.bat
# or
dotnet publish -p:PublishProfile=StandaloneWin64
```

Produces `SolidWorksTester.exe` in the project root (self-contained, single-file).

---

## Project entry points for developers

| Task | Start here |
| --- | --- |
| Change classification | `Services/Analysis/PartModelAnalyzer.cs` |
| Change view layout | `Services/Drawing/DrawingPipelineShared.cs` |
| Add dimensions | `SmartDim*.cs` or pipeline `ApplyDimensions` |
| Change UI | `UI/Layout/MainFormLayoutBuilder.cs` |
| COM connection issues | `Services/SolidWorksConnection.cs` |

---

## Documentation

Read [Documentation hub](../README.md) for the full linked index.

---

## See also

- [COM connection](../solidworks-api/com-connection.md)
- [Packaging & deployment](../ui/packaging-and-deployment.md)
- [Conventions](conventions.md)
