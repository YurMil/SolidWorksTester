# In-app documentation viewer

[← UI overview](overview.md)

Renders **versioned release notes** inside the application using Markdown → HTML → WebView2.

---

## Components

| Class | File | Role |
| --- | --- | --- |
| `ReleaseNotesLoader` | `UI/Documentation/ReleaseNotesLoader.cs` | Resolve MD for current version |
| `MarkdownDocumentRenderer` | `UI/Documentation/MarkdownDocumentRenderer.cs` | Markdig pipeline → HTML document |
| `MarkdownViewerControl` | `UI/Documentation/MarkdownViewerControl.cs` | WebView2 host |
| `ReleaseNotesForm` | `UI/Forms/ReleaseNotesForm.cs` | Modal dialog |

---

## Load order

For version `AppMetadata.Version` (e.g. `0.0.01` → file `v0.0.01.md`):

1. **Disk:** `{ExeDirectory}/Docs/ReleaseNotes/v0.0.01.md`  
   Allows editing deployed docs without rebuild.
2. **Embedded resource:** `SolidWorksTester.Docs.ReleaseNotes.v0.0.01.md`  
   Required for single-file publish.
3. **Fallback:** Generated Markdown explaining missing file.

---

## Build embedding

From `SolidWorksTester.csproj`:

```xml
<EmbeddedResource Include="Docs\ReleaseNotes\v*.md" />
<None Include="Docs\ReleaseNotes\**\*" CopyToOutputDirectory="PreserveNewest" />
```

Only `ReleaseNotes/v*.md` are embedded — **developer docs** in `Docs/` (this tree) stay repository-only unless you extend the csproj.

---

## User entry point

Footer version link (`AppMetadata.VersionDisplay`) opens `ReleaseNotesForm`.

---

## Developer docs vs release notes

| Tree | Audience | Shown in app |
| --- | --- | --- |
| `Docs/` (architecture, API, etc.) | Developers | No — read in IDE / Git |
| `Docs/ReleaseNotes/` | End users | Yes — WebView2 viewer |

---

## See also

- [Release notes README](../ReleaseNotes/README.md)
- [Version & release](../development/version-and-release.md)
- [Packaging & deployment](packaging-and-deployment.md)
