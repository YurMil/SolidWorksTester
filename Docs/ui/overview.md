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
    ├── Header — title + subtitle
    ├── Card — template path (ThemedTextField)
    ├── Card — part list (ListBox) + side buttons (TableLayoutPanel)
    ├── Card — log (ThemedLogView) + progress
    ├── Action row — Generate / Cancel (TableLayoutPanel)
    └── Footer — author + version link (opens release notes)
```

Side and action buttons use **TableLayoutPanel** with percent column widths so controls **do not overlap** on resize.

Minimum window size: approximately **720 × 640**.

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
