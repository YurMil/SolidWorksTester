# Packaging & deployment

[← UI overview](overview.md)

---

## Build targets

| Command | Output |
| --- | --- |
| `dotnet build -c Release` | `bin/Release/net9.0-windows/SolidWorksTester.exe` + DLLs |
| `dotnet publish -p:PublishProfile=StandaloneWin64` | Self-contained single EXE in **project root** |
| `publish.bat` | Wrapper for publish profile |

---

## Publish profile

**File:** `Properties/PublishProfiles/StandaloneWin64.pubxml`

| Setting | Value |
| --- | --- |
| `Configuration` | Release |
| `Platform` | x64 |
| `RuntimeIdentifier` | win-x64 |
| `SelfContained` | true |
| `PublishSingleFile` | true |
| `IncludeNativeLibrariesForSelfExtract` | true |
| `EnableCompressionInSingleFile` | true |
| `PublishTrimmed` | false (required for COM interop stability) |
| `DebugType` | none |
| `PublishDir` | Project root |

**Output:** `SolidWorksTester.exe` (~standalone, includes .NET runtime)

---

## Runtime dependencies

| Dependency | Notes |
| --- | --- |
| Windows x64 | Required |
| SOLIDWORKS | Installed locally; COM registered |
| WebView2 Runtime | Usually present on Windows 11; may need [Evergreen bootstrapper](https://developer.microsoft.com/microsoft-edge/webview2/) on clean machines |

---

## Deployed folder layout (optional)

For editable release notes beside the EXE:

```
SolidWorksTester.exe
Docs/
  ReleaseNotes/
    v0.0.01.md
```

Publish copies `Docs/ReleaseNotes/**` to output directory on build. Single-file EXE still serves embedded copy if disk file missing.

---

## Developer documentation

The full `Docs/` tree (this documentation) is **not** embedded in the EXE by default. Ship separately via Git, wiki, or optional copy step if field engineers need offline dev docs.

---

## Version alignment

Ensure these match before publish:

- `SolidWorksTester.csproj` — `<Version>`, `<AssemblyVersion>`, `<FileVersion>`
- `UI/AppMetadata.cs` — `Version`
- `Docs/ReleaseNotes/v{version}.md` exists

See [Version & release](../development/version-and-release.md).

---

## See also

- [Getting started](../development/getting-started.md)
- [In-app documentation viewer](documentation-viewer.md)
- [COM connection](../solidworks-api/com-connection.md)
