using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Cylindrical;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Imported
{
    /// <summary>
    /// Selects dimension algorithms for imported geometry based on recognized shape and view role.
    /// </summary>
    internal static class ImportedDimStrategy
    {
        public static void ApplyForView(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view,
            PartAnalysisResult analysis,
            ImportedViewClassification classification,
            Action<string> log)
        {
            ImportedViewRole role = ImportedGeometryViewAnalyzer.GetRole(classification, view);
            string viewName = view.GetName2();

            switch (analysis.ImportedShape)
            {
                case ImportedGeometryShapeKind.ElongatedThinProfile:
                    ApplyElongatedProfile(h, model, drawing, view, role, viewName, log);
                    break;

                case ImportedGeometryShapeKind.CylindricalLike when analysis.IsTrueCylindricalTube:
                    ApplyTrueCylindricalTube(h, model, drawing, view, role, viewName, analysis, log);
                    break;

                case ImportedGeometryShapeKind.FlatPlateLike:
                    ApplyFlatPlateLike(h, drawing, view, role, classification, viewName, log);
                    break;

                case ImportedGeometryShapeKind.ComplexBracket:
                case ImportedGeometryShapeKind.BlockyPrismatic:
                case ImportedGeometryShapeKind.CylindricalLike:
                case ImportedGeometryShapeKind.Unknown:
                default:
                    ApplyGeneralImported(h, drawing, view, role, classification, viewName, log);
                    break;
            }
        }

        private static void ApplyElongatedProfile(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view,
            ImportedViewRole role,
            string viewName,
            Action<string> log)
        {
            bool isPrimary = role == ImportedViewRole.Profile ||
                role == ImportedViewRole.LengthPrimary;

            if (role == ImportedViewRole.Profile || role == ImportedViewRole.General)
            {
                log($"  [{viewName}] Profile — geometry dims (arcs, holes, thickness).");
                ImportedDimGeometry.AddForImportedView(h, drawing, view, isPrimaryView: true, log);
                return;
            }

            if (role == ImportedViewRole.LengthPrimary || role == ImportedViewRole.LengthSecondary)
            {
                log($"  [{viewName}] Length view — overall + centerline.");
                SmartDimOverall.Add(h, view);
                CylindricalDimCenterlines.Add(h, model, drawing, view, log);
                ImportedDimGeometry.AddForImportedView(h, drawing, view, isPrimaryView: false, log);
                return;
            }

            ImportedDimGeometry.AddForImportedView(h, drawing, view, isPrimaryView: false, log);
        }

        private static void ApplyTrueCylindricalTube(
            SmartDimHelper h,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView view,
            ImportedViewRole role,
            string viewName,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            Edge[] edges = h.GetViewEdgesCached(view);
            bool hasFullCircle = edges.Any(e =>
                h.IsCircular(e) && h.IsFullCircle(e) && h.GetCircleRadius(e) > 0.0001);

            if (hasFullCircle)
            {
                log($"  [{viewName}] Tube end view — OD / wall + center marks.");
                CylindricalDimCenterlines.Add(h, model, drawing, view, log);
                CylindricalDimSizes.Add(h, view, analysis.IsHollow, log);
                return;
            }

            if (role == ImportedViewRole.LengthPrimary || role == ImportedViewRole.Profile)
            {
                log($"  [{viewName}] Tube side view — length + centerline.");
                CylindricalDimCenterlines.Add(h, model, drawing, view, log);
                CylindricalDimSizes.Add(h, view, analysis.IsHollow, log);
            }
        }

        private static void ApplyFlatPlateLike(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            ImportedViewRole role,
            ImportedViewClassification classification,
            string viewName,
            Action<string> log)
        {
            bool isPrimary = role == ImportedViewRole.Profile ||
                (classification.OverallView != null && ReferenceEquals(view, classification.OverallView));

            log($"  [{viewName}] Flat-plate-like — geometry dims.");
            ImportedDimGeometry.AddForImportedView(h, drawing, view, isPrimary, log);
        }

        private static void ApplyGeneralImported(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            ImportedViewRole role,
            ImportedViewClassification classification,
            string viewName,
            Action<string> log)
        {
            bool isPrimary = role == ImportedViewRole.Profile ||
                (classification.OverallView != null && ReferenceEquals(view, classification.OverallView));

            log($"  [{viewName}] {(isPrimary ? "Primary/profile" : "Secondary")} — geometry dims.");
            ImportedDimGeometry.AddForImportedView(h, drawing, view, isPrimary, log);
        }
    }
}
