# UI overview

[← Documentation hub](../README.md)

WinForms desktop shell for batch drawing generation. No WPF or third-party UI frameworks.

---

## Main components

| Component | File | Role |
| --- | --- | --- |
| `Program` | `Program.cs` | `[STAThread]` entry |
| `MainForm` | `MainForm.cs` | Event handlers, batch worker, COM lifecycle |
| `MainFormView` | `UI/Views/MainFormView.cs` | Strongly typed control references |
| `MainFormLayoutBuilder` | `UI/Layout/MainFormLayoutBuilder.cs` | Builds layout tree |
| `ReleaseNotesForm` | `UI/Forms/ReleaseNotesForm.cs` | Version doc dialog |

---

## Layout structure

```
Form (MainForm)
└── TableLayoutPanel root
    ├── Header — title
    ├── InfoBanner — disclaimer
    ├── Card — template path (ThemedTextField)
    ├── Card — Tasks (TaskManagerView ListView) + side buttons
    │         Add files / folder / Remove / Clear / Skip / Retry
    ├── Action row — Generate / Cancel + progress
    ├── Card — Event log (selected task or session)
    └── Footer — author + version link
```

**Task manager:** one row per `.SLDPRT` with Status, Attempt, Start, Elapsed (+ progress bar), Stage.  
Selecting a row shows that file’s event log (including `[time]` stages). Skip marks Pending tasks; Retry re-runs Failed/Skipped/Cancelled.

Side and action buttons use **TableLayoutPanel** with percent column widths so controls **do not overlap** on resize.

Minimum window size is measured from the live layout (see `FormWindowConstraints`).

---

## Threading

| Action | Thread |
| --- | --- |
| User input, dialogs | UI (STA) |
| `RunBatchAsync` | `Task.Run` worker |
| Log/status/progress updates | `BeginInvoke` to UI |

Cancellation token stops the loop between parts.

---

## Template and sheet-format paths

Both fields start empty on a machine that has never saved them. The user types or browses the paths; each change is written to `%TEMP%\SolidWorksTester\paths.json` (the current user's temp folder). The next launch on the same Windows profile restores them.

The sheet format is applied with `SetupSheet6` before views are created. Leave that field empty to keep the border embedded in the drawing template. A baffle drawing does not auto-switch to another format while a sheet format is selected.

---

## See also

- [Theming & controls](theming-and-controls.md)
- [In-app documentation viewer](documentation-viewer.md)
- [Packaging & deployment](packaging-and-deployment.md)
