# Theming & controls

[← UI overview](overview.md)

Minimalist Teams/Material-inspired styling without external UI NuGet packages beyond WebView2 (viewer only).

---

## Theme system

| Type | File | Role |
| --- | --- | --- |
| `UiTheme` | `UI/Theme/UiTheme.cs` | Colors, fonts, spacing, corner radius |
| `UiControlHelper` | `UI/Theme/UiControlHelper.cs` | Shared paint helpers, gradients |

Palette: neutral greys, primary accent, subtle card shadows,Segoe UI typography.

---

## Custom controls

| Control | File | Base | Notes |
| --- | --- | --- | --- |
| `ThemedButton` | `UI/Controls/ThemedButton.cs` | `Button` | Gradient fill, shadow, 34px height, hover/press states |
| `ThemedCard` | `UI/Controls/ThemedCard.cs` | `Panel` | Rounded card container |
| `ThemedTextField` | `UI/Controls/ThemedTextField.cs` | `UserControl` | Bordered text input |
| `ThemedLogView` | `UI/Controls/ThemedLogView.cs` | `UserControl` | Read-only log surface |
| `ThemedProgressBar` | `UI/Controls/ThemedProgressBar.cs` | `Control` | Custom-painted progress |
| `TaskManagerView` | `UI/Controls/TaskManagerView.cs` | `UserControl` | Batch task grid (status / elapsed / stage) |
| `InfoBanner` | `UI/Controls/InfoBanner.cs` | `Control` | Inline status banner |

Custom properties that should not serialize in the designer use:

```csharp
[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
```

---

## Layout builder

`MainFormLayoutBuilder.Build(MainForm host)` returns `MainFormView` with all wired controls.

**Convention:** Layout code lives in the builder; `MainForm` only handles behaviour and data binding.

---

## Button elevation

Primary actions use:

- Vertical gradient (lighter top → darker bottom)
- 1px bottom shadow simulation
- Minimum height 34px for touch-friendly targets

---

## See also

- [UI overview](overview.md)
- [Conventions](../development/conventions.md)
