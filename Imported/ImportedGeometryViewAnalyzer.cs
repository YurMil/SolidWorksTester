using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.Imported
{
    public enum ImportedViewRole
    {
        General,
        Profile,
        LengthPrimary,
        LengthSecondary
    }

    public sealed class ImportedViewClassification
    {
        public IView? ProfileView { get; init; }
        public IView? LengthPrimaryView { get; init; }
        public IView? LengthSecondaryView { get; init; }
        public IView? OverallView { get; init; }
        public Dictionary<string, ImportedViewRole> RolesByViewName { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Maps orthographic drawing views to profile / length roles for imported parts.</summary>
    internal static class ImportedGeometryViewAnalyzer
    {
        public static ImportedViewClassification Classify(
            SmartDimHelper h,
            IDrawingDoc drawing,
            PartAnalysisResult analysis)
        {
            var orthoViews = new List<(IView view, double width, double height, double area)>();

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                Edge[] edges = h.GetViewEdgesCached(view);
                if (edges.Length > 0)
                {
                    var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(edges, view);
                    double width = maxX - minX;
                    double height = maxY - minY;
                    orthoViews.Add((view, width, height, width * height));
                }

                view = view.GetNextView() as IView;
            }

            if (orthoViews.Count == 0)
                return new ImportedViewClassification();

            orthoViews.Sort((a, b) => a.area.CompareTo(b.area));

            IView? profileView = orthoViews[0].view;
            IView? overallView = orthoViews[^1].view;
            IView? lengthPrimary = null;
            IView? lengthSecondary = null;

            bool useLengthRoles = analysis.ImportedShape == ImportedGeometryShapeKind.ElongatedThinProfile;

            if (useLengthRoles)
            {
                double targetLong = analysis.BboxLongMeters;
                double bestLongScore = 0;
                double secondLongScore = 0;

                foreach (var entry in orthoViews)
                {
                    if (ReferenceEquals(entry.view, profileView))
                        continue;

                    double maxSpan = Math.Max(entry.width, entry.height);
                    double score = targetLong > 0
                        ? 1.0 / (1.0 + Math.Abs(maxSpan - targetLong))
                        : maxSpan;

                    if (score > bestLongScore)
                    {
                        secondLongScore = bestLongScore;
                        lengthSecondary = lengthPrimary;
                        bestLongScore = score;
                        lengthPrimary = entry.view;
                    }
                    else if (score > secondLongScore)
                    {
                        secondLongScore = score;
                        lengthSecondary = entry.view;
                    }
                }

                overallView = lengthPrimary ?? overallView;
            }

            var roles = new Dictionary<string, ImportedViewRole>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in orthoViews)
            {
                string name = entry.view.GetName2();
                if (ReferenceEquals(entry.view, profileView))
                    roles[name] = ImportedViewRole.Profile;
                else if (useLengthRoles && lengthPrimary != null && ReferenceEquals(entry.view, lengthPrimary))
                    roles[name] = ImportedViewRole.LengthPrimary;
                else if (useLengthRoles && lengthSecondary != null && ReferenceEquals(entry.view, lengthSecondary))
                    roles[name] = ImportedViewRole.LengthSecondary;
                else
                    roles[name] = ImportedViewRole.General;
            }

            return new ImportedViewClassification
            {
                ProfileView = profileView,
                LengthPrimaryView = lengthPrimary,
                LengthSecondaryView = lengthSecondary,
                OverallView = overallView,
                RolesByViewName = roles
            };
        }

        public static ImportedViewRole GetRole(ImportedViewClassification classification, IView view)
        {
            string name = view.GetName2();
            return classification.RolesByViewName.TryGetValue(name, out ImportedViewRole role)
                ? role
                : ImportedViewRole.General;
        }
    }
}
