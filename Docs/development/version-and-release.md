# Version & release

[← Documentation hub](../README.md)

---

## Version sources (must stay in sync)

| Location | Fields |
| --- | --- |
| `SolidWorksTester.csproj` | `<Version>`, `<AssemblyVersion>`, `<FileVersion>`, `<InformationalVersion>` |
| `UI/AppMetadata.cs` | `Version`, `VersionDisplay` |

Current example: **`0.0.01`**

---

## Release checklist

1. **Implement** feature/fix on a branch.
2. **Update developer docs** in `Docs/` (relevant architecture/pipeline/API pages).
3. **Bump version** in `.csproj` and `AppMetadata.cs` (same string).
4. **Create release note:**
   - Copy `Docs/ReleaseNotes/_template.md` → `Docs/ReleaseNotes/vX.Y.ZZ.md`
   - Fill user-facing sections (features, fixes, known issues).
5. **Build & test:**
   ```powershell
   dotnet build -c Release
   ```
6. **Publish** (optional):
   ```powershell
   .\publish.bat
   ```
7. **Verify** footer version link opens the new MD in WebView2.

No C# changes needed in `ReleaseNotesForm` when only documentation changes — loader matches filename to `AppMetadata.Version`.

---

## Release note file naming

```
v{AppMetadata.Version}.md
```

Examples:

| Version | File |
| --- | --- |
| `0.0.01` | `v0.0.01.md` |
| `0.0.02` | `v0.0.02.md` |

---

## Embedded resources

Release note Markdown is embedded at build:

```xml
<EmbeddedResource Include="Docs\ReleaseNotes\v*.md" />
```

After adding a new `v*.md`, rebuild so single-file EXE includes it.

---

## Semantic versioning guidance

| Segment | Use in this project |
| --- | --- |
| Major | Breaking workflow or template contract |
| Minor | New pipeline, major UI change |
| Patch | Bug fixes, dimension tweaks |

Project currently uses `0.0.XX` pre-1.0 scheme.

---

## See also

- [Release notes README](../ReleaseNotes/README.md)
- [In-app documentation viewer](../ui/documentation-viewer.md)
- [Packaging & deployment](../ui/packaging-and-deployment.md)
