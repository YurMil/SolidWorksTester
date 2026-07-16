using SolidWorks.Interop.sldworks;
using SolidWorksTester.FlangeGasket;
using SolidWorksTester.RoundFlatPlate;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing.Routing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Services.Drawing.FlatPlate
{
    /// <summary>
    /// Resolves flat-plate sub-kind at drawing time (property/catalog route + view geometry fallback).
    /// </summary>
    internal static class FlatPlateSubKindResolver
    {
        public static FlatPlateDimContext Resolve(
            PartAnalysisResult analysis,
            DrawingRouteDecision route,
            SmartDimHelper dimHelper,
            IDrawingDoc drawing)
        {
            // Baffle before flange — dense arrays must not enter flange bolt-circle pipeline.
            if (TryResolveBaffleOverride(analysis, route, dimHelper, drawing, out FlatPlateDimContext? baffleContext))
                return baffleContext!;

            if (TryResolveFlangeOverride(analysis, route, dimHelper, drawing, out FlatPlateDimContext? flangeContext))
                return flangeContext!;

            if (route.ForcedFlatPlateSubKind is FlatPlateSubKind forced && forced != FlatPlateSubKind.Unknown)
                return BuildFromSubKind(forced, dimHelper, drawing);

            if (analysis.FlatPlateSubKind != FlatPlateSubKind.Unknown &&
                analysis.FlatPlateSubKindSource != ClassificationSource.Geometry)
            {
                return BuildFromSubKind(analysis.FlatPlateSubKind, dimHelper, drawing);
            }

            if (analysis.FlatPlateSubKind == FlatPlateSubKind.BafflePlate)
                return BuildFromSubKind(FlatPlateSubKind.BafflePlate, dimHelper, drawing);

            if (analysis.FlatPlateSubKind == FlatPlateSubKind.FlangeGasket ||
                FlangeGasketViewAnalyzer.DetectFromDrawing(dimHelper, drawing))
            {
                IView? discView = FlangeGasketViewAnalyzer.FindPrimaryDiscView(dimHelper, drawing);
                return new FlatPlateDimContext
                {
                    SubKind = FlatPlateSubKind.FlangeGasket,
                    PrimaryFlatView = discView,
                    DiscFaceView = discView
                };
            }

            bool isRoundDisc = analysis.IsRoundFlatProfile ||
                RoundFlatPlateViewAnalyzer.DetectFromDrawing(dimHelper, drawing);

            if (isRoundDisc)
            {
                return new FlatPlateDimContext
                {
                    SubKind = FlatPlateSubKind.RoundDisc,
                    PrimaryFlatView = null
                };
            }

            bool isRoundedEnd = analysis.IsRoundedEndFlatProfile ||
                RoundedFlatPlateViewAnalyzer.DetectFromDrawing(dimHelper, drawing);

            if (isRoundedEnd)
            {
                return new FlatPlateDimContext
                {
                    SubKind = FlatPlateSubKind.RoundedEnd,
                    PrimaryFlatView = FlatPlateViewAnalyzer.FindPrimaryFlatLyingView(dimHelper, drawing)
                };
            }

            return new FlatPlateDimContext
            {
                SubKind = FlatPlateSubKind.Generic,
                PrimaryFlatView = FlatPlateViewAnalyzer.FindPrimaryFlatLyingView(dimHelper, drawing)
            };
        }

        private static bool TryResolveBaffleOverride(
            PartAnalysisResult analysis,
            DrawingRouteDecision route,
            SmartDimHelper dimHelper,
            IDrawingDoc drawing,
            out FlatPlateDimContext? context)
        {
            context = null;
            bool geometryBaffle = analysis.GeometryFlatPlateSubKind == FlatPlateSubKind.BafflePlate ||
                                  analysis.FlatPlateSubKind == FlatPlateSubKind.BafflePlate;
            bool descriptionBaffle = EstPartPropertiesParser.DescriptionIndicatesBafflePlate(
                analysis.EstProperties.Description);
            bool forcedBaffle = route.ForcedFlatPlateSubKind == FlatPlateSubKind.BafflePlate;

            if (!geometryBaffle && !descriptionBaffle && !forcedBaffle)
                return false;

            // Do not override an explicit non-baffle dedicated sub-kind (except Generic/Unknown).
            FlatPlateSubKind forced = route.ForcedFlatPlateSubKind ?? analysis.FlatPlateSubKind;
            if (forced is FlatPlateSubKind.FlangeGasket or FlatPlateSubKind.RoundDisc or FlatPlateSubKind.RoundedEnd)
                return false;

            context = BuildFromSubKind(FlatPlateSubKind.BafflePlate, dimHelper, drawing);
            return true;
        }

        private static bool TryResolveFlangeOverride(
            PartAnalysisResult analysis,
            DrawingRouteDecision route,
            SmartDimHelper dimHelper,
            IDrawingDoc drawing,
            out FlatPlateDimContext? context)
        {
            context = null;

            if (analysis.GeometryFlatPlateSubKind == FlatPlateSubKind.BafflePlate ||
                analysis.FlatPlateSubKind == FlatPlateSubKind.BafflePlate ||
                EstPartPropertiesParser.DescriptionIndicatesBafflePlate(analysis.EstProperties.Description))
                return false;

            bool geometryFlange = analysis.GeometryFlatPlateSubKind == FlatPlateSubKind.FlangeGasket;
            bool drawingFlange = FlangeGasketViewAnalyzer.DetectFromDrawing(dimHelper, drawing);
            bool descriptionFlange = EstPartPropertiesParser.DescriptionIndicatesFlangeOrGasket(
                analysis.EstProperties.Description);

            if (!geometryFlange && !drawingFlange && !descriptionFlange)
                return false;

            FlatPlateSubKind forced = route.ForcedFlatPlateSubKind ?? analysis.FlatPlateSubKind;
            if (forced != FlatPlateSubKind.Generic && forced != FlatPlateSubKind.Unknown)
                return false;

            context = BuildFromSubKind(FlatPlateSubKind.FlangeGasket, dimHelper, drawing);
            return true;
        }

        private static FlatPlateDimContext BuildFromSubKind(
            FlatPlateSubKind subKind,
            SmartDimHelper dimHelper,
            IDrawingDoc drawing)
        {
            switch (subKind)
            {
                case FlatPlateSubKind.BafflePlate:
                {
                    // Outline-only: never walk dense hole edges / flange disc detection here.
                    IView? primary = FlatPlateViewAnalyzer.FindPrimaryFlatLyingViewByOutline(drawing);
                    return new FlatPlateDimContext
                    {
                        SubKind = FlatPlateSubKind.BafflePlate,
                        PrimaryFlatView = primary,
                        DiscFaceView = primary
                    };
                }

                case FlatPlateSubKind.FlangeGasket:
                {
                    IView? discView = FlangeGasketViewAnalyzer.FindPrimaryDiscView(dimHelper, drawing);
                    if (discView == null && FlangeGasketViewAnalyzer.DetectFromDrawing(dimHelper, drawing))
                        discView = FlangeGasketViewAnalyzer.FindPrimaryDiscView(dimHelper, drawing);

                    return new FlatPlateDimContext
                    {
                        SubKind = FlatPlateSubKind.FlangeGasket,
                        PrimaryFlatView = discView,
                        DiscFaceView = discView
                    };
                }

                case FlatPlateSubKind.RoundDisc:
                    return new FlatPlateDimContext
                    {
                        SubKind = FlatPlateSubKind.RoundDisc,
                        PrimaryFlatView = null
                    };

                case FlatPlateSubKind.RoundedEnd:
                    return new FlatPlateDimContext
                    {
                        SubKind = FlatPlateSubKind.RoundedEnd,
                        PrimaryFlatView = FlatPlateViewAnalyzer.FindPrimaryFlatLyingView(dimHelper, drawing)
                    };

                default:
                    return new FlatPlateDimContext
                    {
                        SubKind = FlatPlateSubKind.Generic,
                        PrimaryFlatView = FlatPlateViewAnalyzer.FindPrimaryFlatLyingView(dimHelper, drawing)
                    };
            }
        }

        public static string DescribeSubKind(FlatPlateSubKind subKind) => subKind switch
        {
            FlatPlateSubKind.BafflePlate =>
                "Baffle plate mode: import profile/tabs; thickness; skip dense hole flood (Detail/ordinate later).",
            FlatPlateSubKind.FlangeGasket => "Flange/gasket mode: OD, ID, BCD, pattern angle, hole qty, thickness.",
            FlatPlateSubKind.RoundDisc => "Round flat plate mode: OD, centerlines, side-view thickness.",
            FlatPlateSubKind.RoundedEnd => "Rounded-end flat plate mode: overall, outer arc, holes, thickness.",
            _ => "Generic flat plate mode: overall, thickness, holes."
        };
    }
}
