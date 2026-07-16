# Development conventions

[← Documentation hub](../README.md)

---

## Naming

| Item | Convention |
| --- | --- |
| Drawing views | `Drawing View1` … `Drawing View4` (SOLIDWORKS default pattern) |
| Pipelines | `{Kind}DrawingPipeline` static class |
| SmartDim modules | `SmartDim/Modules/SmartDim{Feature}.cs` |
| Special modules | Subfolder per domain (`RoundFlatPlate/`, `Cylindrical/`, `BafflePlate/`, …) |
| UI controls | `Themed{ControlName}` |

---

## Code organisation

- **Services** — no UI references
- **SmartDim** — no WinForms references
- **UI** — orchestrates services; passes `Action<string>` log callback
- **Internal** — pipelines and helpers are `internal` unless test exposure needed

---

## Logging

```csharp
void Process(..., Action<string> log)
{
    log("Human-readable progress line.");
}
```

UI marshals to `ThemedLogView`. Prefer `log` over `Console.WriteLine` for user-visible output.

---

## Units

- Always assume API **meters** for lengths
- Compare with constants from `SmartDimConstants` or documented thresholds
- Display on drawings follows MMGS after service sets unit system

---

## COM safety

- One worker thread per batch for COM
- Close documents when done
- Cast to specific types (`PartDoc`, `IDrawingDoc`) before type-specific API
- Wrap optional geometry probes in try/catch — skip entity on failure

---

## View4 ambiguity

`Drawing View4` means **isometric** in P-01/P-03 and **flat pattern** in P-02. Never assume View4 content without pipeline context.

---

## Documentation updates

When changing behaviour:

1. Update the relevant `Docs/**/*.md` page
2. If user-visible, add/update `Docs/ReleaseNotes/vX.Y.ZZ.md`
3. Bump version per [Version & release](version-and-release.md)

---

## See also

- [Project structure](../architecture/project-structure.md)
- [Adding a pipeline](adding-a-pipeline.md)
