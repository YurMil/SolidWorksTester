# SOLIDWORKS API versions (2022–2026)

[← Version router](../version-router.md)

Reference for **product year**, **interop baseline**, and **automation nuances** used by this project.

---

## Quick matrix

| Year | API rev (typ.) | NuGet baseline | Interop file (typ.) | Router strategy |
| --- | --- | --- | --- | --- |
| **2026** | 34.x | 32.1.0 | 34.5.x | HLV display + outer-edge side centerlines |
| **2025** | 32.5–33.x | 32.1.0 | 33.5.x | HLV display + outer-edge side centerlines (no silhouette API) |
| **2024** | 32.x | 32.1.0 | 32.1.x | HLV display + outer-edge side centerlines |
| **2023** | 31.x | 32.1.0 | 31.x | Full legacy cylindrical centerlines |
| **2022** | 30.x | 32.1.0 | 30.x | Full legacy cylindrical centerlines |

Revision numbers from `RevisionNumber()` do not always match marketing year — router uses **max(registry year, revision-mapped year)**.

---

## 2022

| Topic | Guidance |
| --- | --- |
| Interop | NuGet 32.1 backward-compatible |
| Drawing views | `CreateDrawViewFromModelView3`, standard view names |
| Cylindrical | Side-view centerline via outer parallel edges + face pick |
| Sheet metal | View Palette flat pattern (EN/RU names) |
| COM | STA thread required |

---

## 2023

Same automation surface as 2022 for this codebase.  
Verify hole wizard feature names if custom templates differ.

---

## 2024

| Topic | Guidance |
| --- | --- |
| Interop | NuGet **32.1.0** matches shipping interop |
| Recommended | Default build/publish target for CI |
| Cylindrical | Outer-edge side centerlines + HLV display on all views |

---

## 2025

| Topic | Guidance |
| --- | --- |
| Interop file | Often **33.5.x** while `RevisionNumber` may show **32.5.0** |
| **Critical** | Silhouette-edge centerline API can crash SLDWORKS (RPC 0x800706BA) — not used |
| Router | Outer-edge + face-pick side centerlines on all years |
| Publish | Must bundle NuGet interop; do not rely on `Private=false` install reference |

Observed log pattern before fix:

```
Centerlines: Drawing View3
ERROR: The RPC server is unavailable. (0x800706BA)
```

---

## 2026

Treat as **2025 mode** until validated on site:

- HLV display on all model views
- Outer-edge side centerlines (no silhouette API)
- Re-test flat pattern palette after SW upgrades

---

## Published EXE checklist

| Check | Reason |
| --- | --- |
| NuGet interop in `.csproj` | Prevents `FileNotFoundException` for 33.5.x |
| `SolidWorksBootstrap.TryValidate` before batch | User sees clear message if SW not installed |
| STA worker (`StaTaskRunner`) | COM apartment correctness |
| `EnsureConnected` between parts | Recover from RPC loss |

---

## Registry / folder discovery

Scanned paths (in order of preference):

1. `HKLM\SOFTWARE\SolidWorks\SOLIDWORKS {year}\Setup` → `SolidWorks Folder`
2. `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS {year}\`
3. Legacy `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\` (mapped to latest default year)

Requires `SolidWorks.Interop.sldworks.dll` in install folder.

---

## See also

- [Version router](../version-router.md)
- [COM connection](../com-connection.md)
- [Cylindrical annotations](../../modules/cylindrical-annotations.md)
