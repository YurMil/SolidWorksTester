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

## Default template path

Initial value: `AppMetadata.DefaultTemplatePath`  
(`C:\EST\91_SW Setup\Templates\DRAWING.DRWDOT`)

Shown in `ThemedTextField` with ToolTip explaining `.DRWDOT` / `.SLDDRW` templates.

---

## See also

- [Theming & controls](theming-and-controls.md)
- [In-app documentation viewer](documentation-viewer.md)
- [Packaging & deployment](packaging-and-deployment.md)
