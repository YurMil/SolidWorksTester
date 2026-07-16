# Drawing pipeline router

Central routing between **classification** (`PartAnalysisResult`) and **execution** (existing P-01…P-04 pipelines).

## Flow

```
PartModelAnalyzer.Analyze()
  → geometry + CustomPropertyReader + EstNameRegistry + PartClassificationRouter
DrawingPipelineRouter.Resolve(analysis)
  → DrawingRouteDecision
DrawingPipelineExecutor.Execute(swApp, drawing, partPath, analysis, route)
  → FlatPlate | BentSheetMetal | Cylindrical | ImportedGeometry | GenericFallback
```

Flat-plate sub-routing remains in `FlatPlateSubKindResolver` + `FlatPlateDimRouter`, but respects `DrawingRouteDecision.ForcedFlatPlateSubKind` when classification came from EST/custom properties.

## Key types

| Type | Role |
|------|------|
| `DrawingPipelineId` | Stable pipeline id (P-01…P-04, GenericFallback) |
| `DrawingRouteDecision` | Resolved route: pipeline, catalog id, forced sub-kind, dedicated flag |
| `DrawingRouteSource` | Geometry / EstNameCatalog / ExplicitProfile / Hybrid |
| `EstCatalogRouteTable` | Optional catalog/profile overrides (structural → imported, shell → cylindrical, etc.) |
| `DrawingPipelineRouter` | `Resolve(PartAnalysisResult)` |
| `DrawingPipelineExecutor` | Dispatches to `*DrawingPipeline.Process` |

## Priority

1. Merged `PartModelKind` from `PartClassificationRouter` (CADAS props > EST Name > geometry)
2. `EstCatalogRouteTable` overrides by `EstNameCatalogId` or `CADAS_DrawingProfile`
3. For flat plate: forced sub-kind when `FlatPlateSubKindSource != Geometry`
4. `EstNameHasDedicatedPipeline == false` → same family pipeline, log + audit note (`generic-dims`)

## Integration points

- **`SheetMetalDrawingService.ProcessDrawingModel`** — calls router + executor instead of `switch (analysis.Kind)`
- **`DrawingTestBench` audit** — `Classification.Route` in JSON
- **Future cadas-sw port** — mirror `DrawingRouteDecision` in `DrawingGenRequest` / `DrawingGenerationService`

## Adding a catalog family

1. Add rule to `EstNameRegistry` (classification)
2. If pipeline differs from default kind mapping, add row to `EstCatalogRouteTable`
3. When dedicated dims are ready, set `DedicatedPipeline: true` in registry and implement modules

Flat-plate nested sub-kinds: `Generic`, `RoundDisc`, `RoundedEnd`, `FlangeGasket`, `BafflePlate`, **`ArcSector`**
— full matrix: [flat-plate-subkinds.md](flat-plate-subkinds.md), ArcSector details: [arc-sector-plate.md](../modules/arc-sector-plate.md), baffle: [baffle-plate-pipeline.md](baffle-plate-pipeline.md).

See also: [pipelines-overview.md](pipelines-overview.md), [est-name-catalog.md](../analysis/est-name-catalog.md).
