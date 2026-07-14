# Release notes (Markdown)

Version-specific **end-user** documentation for the **Sheet Metal Drawing Generator** UI.

> **Developers:** see the full engineering documentation hub at [../README.md](../README.md) (architecture, COM/API, pipelines, UI, packaging).

## File naming

| Application version | Markdown file |
| --- | --- |
| `0.0.01` in `AppMetadata.Version` | `v0.0.01.md` |
| `0.0.02` | `v0.0.02.md` |

**Rule:** file name is always `v` + exact value of `AppMetadata.Version` + `.md`.

## Adding a new release

1. Bump version in `SolidWorksTester.csproj` and `UI/AppMetadata.cs`.
2. Copy `_template.md` to `vX.Y.ZZ.md` and edit content.
3. Rebuild / run `publish.bat` — the viewer loads the matching file automatically.

No code changes are required in the release-notes dialog when only documentation changes.

## Load order at runtime

1. `Docs/ReleaseNotes/v{version}.md` next to the executable (editable deployment).
2. Embedded resource inside the published assembly (single-file EXE).
