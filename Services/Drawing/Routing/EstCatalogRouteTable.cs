using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Services.Drawing.Routing
{
    /// <summary>
    /// Optional catalog/profile overrides on top of <see cref="PartModelKind"/>.
    /// Most EST names only set kind via <see cref="EstNameRegistry"/>; this table handles exceptions.
    /// </summary>
    internal static class EstCatalogRouteTable
    {
        internal sealed record CatalogRoute(
            DrawingPipelineId? OverridePipelineId,
            FlatPlateSubKind? ForceFlatPlateSubKind,
            bool? HasDedicatedPipeline);

        private static readonly Dictionary<string, CatalogRoute> Routes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Structural — always imported pipeline even if geometry looks flat
                ["ipe"] = new(DrawingPipelineId.ImportedGeometry, null, false),
                ["hea"] = new(DrawingPipelineId.ImportedGeometry, null, false),
                ["heb"] = new(DrawingPipelineId.ImportedGeometry, null, false),
                ["inp"] = new(DrawingPipelineId.ImportedGeometry, null, false),
                ["unp"] = new(DrawingPipelineId.ImportedGeometry, null, false),
                ["upe"] = new(DrawingPipelineId.ImportedGeometry, null, false),
                ["shs"] = new(DrawingPipelineId.ImportedGeometry, null, false),
                ["rhs"] = new(DrawingPipelineId.ImportedGeometry, null, false),
                ["angle"] = new(DrawingPipelineId.ImportedGeometry, null, false),
                ["square_bar"] = new(DrawingPipelineId.ImportedGeometry, null, false),

                // Vessel lofted-bend shells — flat pattern + OD/height
                ["shell"] = new(DrawingPipelineId.LoftedBends, null, true),
                ["insulation_shell"] = new(DrawingPipelineId.LoftedBends, null, true),
                ["shell_with_cutting"] = new(DrawingPipelineId.LoftedBends, null, true),

                ["cone"] = new(DrawingPipelineId.Cylindrical, null, false),
                ["bellow"] = new(DrawingPipelineId.Cylindrical, null, false),
                ["dished_end"] = new(DrawingPipelineId.Cylindrical, null, false),
                ["dished_end_din28011"] = new(DrawingPipelineId.Cylindrical, null, false),
                ["dished_end_din28013"] = new(DrawingPipelineId.Cylindrical, null, false),
                ["dished_end_ss895"] = new(DrawingPipelineId.Cylindrical, null, false),

                // Baffle / tube-sheet — dedicated nested flat-plate pipeline
                ["baffle_plate"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.BafflePlate, true),
                ["baffle_plate_fuzzy"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.BafflePlate, true),

                // Flat families without dedicated dim modules yet
                ["lifting_lug"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.Generic, false),
                ["lifting_lug_vertical"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.Generic, false),
                ["lifting_lug_free"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.Generic, false),
                ["nozzle_support"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.Generic, false),
                ["nozzle_support_horizontal"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.Generic, false),
                ["nozzle_support_vertical"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.Generic, false),
                ["repad"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.Generic, false),
                ["grating"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.Generic, false),
                ["nameplate"] = new(DrawingPipelineId.FlatPlate, FlatPlateSubKind.Generic, false),
            };

        public static bool TryGet(string? catalogOrProfile, out CatalogRoute route)
        {
            route = default!;
            if (string.IsNullOrWhiteSpace(catalogOrProfile))
                return false;

            return Routes.TryGetValue(catalogOrProfile.Trim(), out route!);
        }
    }
}
