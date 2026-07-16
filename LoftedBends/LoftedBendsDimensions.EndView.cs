using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.BafflePlate;
using SolidWorksTester.Cylindrical;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.LoftedBends
{
    /// <summary>
    /// Lofted-bend shell dimensions matched to EST reference drawings:
    /// side (View1) — height, OD (+ prefix), id (+ prefix), axis centerline;
    /// end (View2) — wall thickness, weld-gap centerline + linear gap at outer tips;
    /// flat pattern — length + width + bend notes.
    /// </summary>
    internal static partial class LoftedBendsDimensions
    {
        private static string? PickPrimaryEndViewName(
            SmartDimHelper h,
            IDrawingDoc drawing,
            Action<string> log)
        {
            string? bestName = null;
            double bestScore = double.MinValue;
            string? view2Name = null;

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                string name = view.GetName2();
                if (name.Equals(SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase) ||
                    view.IsFlatPatternView())
                {
                    view = view.GetNextView() as IView;
                    continue;
                }

                if (name.Equals("Drawing View2", StringComparison.OrdinalIgnoreCase))
                    view2Name = name;

                Edge[] edges = h.GetViewEdges(view);
                double score = ScoreEndView(h, view, edges);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestName = name;
                }

                view = view.GetNextView() as IView;
            }

            // Require positive circular evidence; otherwise prefer View2 (projected top → end often).
            if (bestScore < 1.0)
            {
                bestName = view2Name ?? bestName;
                log($"  End view pick: weak circular score — using {bestName ?? "none"}.");
            }
            else
            {
                log($"  End view pick: {bestName} (score={bestScore:F1}).");
            }

            return bestName;
        }

        private static double ScoreEndView(SmartDimHelper h, IView view, Edge[] edges)
        {
            if (edges.Length == 0)
                return -1;

            var circular = edges.Where(h.IsCircular).ToArray();
            if (circular.Length == 0)
                return -1;

            double maxR = circular.Max(h.GetCircleRadius);
            if (maxR < 0.020)
                return -1;

            if (!FlatPlateViewAnalyzer.TryGetOutlineSize(view, out double w, out double ht))
                return -1;

            double aspect = Math.Max(w, ht) / Math.Max(Math.Min(w, ht), 1e-9);
            if (aspect > 1.55)
                return -1; // elongated → side silhouette

            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            double odSheet = maxR * 2.0 * scale;
            double outline = Math.Max(w, ht);
            if (outline < odSheet * 0.40)
                return -1;

            // Side views often show left/right end-cap arcs with centers far apart.
            // True end face: arc centers clustered near one ring center.
            double[][] centers = circular
                .Select(c => h.GetCircleCenterOnSheet(c, view))
                .ToArray();
            double cx = centers.Average(p => p[0]);
            double cy = centers.Average(p => p[1]);
            double maxSpread = centers.Max(p =>
            {
                double dx = p[0] - cx;
                double dy = p[1] - cy;
                return Math.Sqrt(dx * dx + dy * dy);
            });
            if (maxSpread > odSheet * 0.28)
                return -1;

            return circular.Length * 10.0 + maxR * 1000.0 + (2.0 - Math.Min(aspect, 2.0)) * 5.0;
        }

        /// <summary>
        /// Open-ring end view: circular arcs present, outline not elongated like a side silhouette.
        /// </summary>
        internal static bool IsShellEndView(SmartDimHelper h, IView view, Edge[] edges) =>
            ScoreEndView(h, view, edges) >= 1.0;

        private sealed class ShellSizeHints
        {
            public double? ThicknessMeters;
            public double? HeightMeters;
            public double? FlatLengthMeters;
            public double? OuterDiameterMeters;
        }

        private static ShellSizeHints ResolveHints(
            SmartDimHelper h,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            var hints = new ShellSizeHints
            {
                // EST SHELL: DIM1=t, DIM2=h, DIM3=developed length
                ThicknessMeters = analysis.EstProperties.Dim1Mm is double t && t > 0.4 && t <= 40
                    ? t / 1000.0
                    : BafflePlateThickness.TryReadSheetMetalThickness(h),
                HeightMeters = analysis.EstProperties.Dim2Mm is double hgt && hgt > 1
                    ? hgt / 1000.0
                    : null,
                FlatLengthMeters = analysis.EstProperties.Dim3Mm is double len && len > 10
                    ? len / 1000.0
                    : null
            };

            if (!string.IsNullOrWhiteSpace(analysis.EstProperties.Description))
            {
                Match m = DescShellRegex.Match(analysis.EstProperties.Description);
                if (m.Success)
                {
                    hints.ThicknessMeters ??= ParseMm(m.Groups[1].Value) / 1000.0;
                    hints.HeightMeters ??= ParseMm(m.Groups[2].Value) / 1000.0;
                    hints.FlatLengthMeters ??= ParseMm(m.Groups[3].Value) / 1000.0;
                    hints.OuterDiameterMeters ??= ParseMm(m.Groups[4].Value) / 1000.0;
                    log($"  EST Description parse: t={hints.ThicknessMeters * 1000:F1}, " +
                        $"h={hints.HeightMeters * 1000:F0}, L={hints.FlatLengthMeters * 1000:F0}, " +
                        $"OD={hints.OuterDiameterMeters * 1000:F0}.");
                }
            }

            // Prefer SM feature thickness when EST DIM1 missing.
            hints.ThicknessMeters ??= BafflePlateThickness.TryReadSheetMetalThickness(h);

            log($"  Hints: t={Fmt(hints.ThicknessMeters)}, h={Fmt(hints.HeightMeters)}, " +
                $"L={Fmt(hints.FlatLengthMeters)}, OD={Fmt(hints.OuterDiameterMeters)}.");
            return hints;
        }

        private static string Fmt(double? m) =>
            m.HasValue ? $"{m.Value * 1000:F1} mm" : "?";

        private static double ParseMm(string s) =>
            double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

        // ── End view: wall thickness between OD/ID arcs ───────────────────

        private static void EnsureWallThicknessOnEndView(
            SmartDimHelper h,
            IView view,
            double? expectedT,
            Action<string> log)
        {
            if (h.DimensionedFeatures.Contains("Thickness"))
                return;
            if (expectedT.HasValue && h.HasDimensionWithValueInDrawing(expectedT.Value))
            {
                h.DimensionedFeatures.Add("Thickness");
                return;
            }

            var circles = h.GetViewEdges(view)
                .Where(h.IsCircular)
                .OrderByDescending(h.GetCircleRadius)
                .ToArray();
            if (circles.Length < 2)
            {
                log($"  Wall thickness: need OD+ID arcs on {view.GetName2()} (got {circles.Length}).");
                return;
            }

            Edge outer = circles[0];
            Edge? inner = null;
            double bestErr = double.MaxValue;
            double outerR = h.GetCircleRadius(outer);

            foreach (Edge c in circles.Skip(1))
            {
                double wall = outerR - h.GetCircleRadius(c);
                if (wall < 0.0004 || wall > 0.040)
                    continue;
                double err = expectedT.HasValue ? Math.Abs(wall - expectedT.Value) : wall;
                if (err < bestErr)
                {
                    bestErr = err;
                    inner = c;
                }
            }

            if (inner == null)
            {
                log($"  Wall thickness: no ID arc matching t on {view.GetName2()}.");
                return;
            }

            if (expectedT.HasValue && bestErr > expectedT.Value * 0.4 + 0.0005)
            {
                log($"  Wall thickness: best wall err too large on {view.GetName2()}.");
                return;
            }

            h.ClearSelection();
            if (!h.SelectEdge(outer, view, false) || !h.SelectEdge(inner, view, true))
            {
                log("  Wall thickness: SelectEdge failed.");
                return;
            }

            double[] cOuter = h.GetCircleCenterOnSheet(outer, view);
            double scale = Math.Max(view.ScaleDecimal, 1e-9);
            // Place dim to the right of the wall (3 o'clock-ish).
            double textX = cOuter[0] + outerR * scale + DimOffset;
            double textY = cOuter[1];
            DisplayDimension? dim = h.CreateDimension(textX, textY);
            if (dim != null)
            {
                h.DimensionedFeatures.Add("Thickness");
                log($"  Wall thickness on {view.GetName2()} (OD−ID).");
            }
            else
            {
                log($"  Wall thickness: CreateDimension null on {view.GetName2()}.");
            }
        }

        // ── End view: weld gap centerline + linear dim below ───────────────

        private static void TryWeldGapOnEndView(
            SmartDimHelper h,
            IDrawingDoc drawing,
            IView view,
            Action<string> log)
        {
            if (h.DimensionedFeatures.Contains("LoftWeldGap"))
                return;

            Edge[] edges = h.GetViewEdges(view);
            var circles = edges.Where(h.IsCircular).OrderByDescending(h.GetCircleRadius).ToArray();
            var linear = edges.Where(h.IsLinear).ToArray();
            if (circles.Length == 0 || linear.Length < 2)
            {
                log($"  Weld gap: need circle + 2 lines on {view.GetName2()}.");
                return;
            }

            double[] center = h.GetCircleCenterOnSheet(circles[0], view);
            double outerRSheet = h.GetCircleRadius(circles[0]) * Math.Max(view.ScaleDecimal, 1e-9);
            double scale = Math.Max(view.ScaleDecimal, 1e-9);

            var radial = new System.Collections.Generic.List<(Edge E, double Ang, double[] Mid)>();
            foreach (Edge e in linear)
            {
                double[] mid = h.GetEdgeMidpointOnSheet(e, view);
                double dx = mid[0] - center[0];
                double dy = mid[1] - center[1];
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < outerRSheet * 0.50 || dist > outerRSheet * 1.20)
                    continue;
                radial.Add((e, Math.Atan2(dy, dx), mid));
            }

            if (radial.Count < 2)
            {
                log($"  Weld gap: <2 radial edges on {view.GetName2()} (found {radial.Count}).");
                return;
            }

            Edge? a = null, b = null;
            double bestDist = double.MaxValue;
            for (int i = 0; i < radial.Count; i++)
            {
                for (int j = i + 1; j < radial.Count; j++)
                {
                    double dx = radial[i].Mid[0] - radial[j].Mid[0];
                    double dy = radial[i].Mid[1] - radial[j].Mid[1];
                    double dModel = Math.Sqrt(dx * dx + dy * dy) / scale;
                    if (dModel < 0.0003 || dModel > 0.080)
                        continue;
                    if (dModel < bestDist)
                    {
                        bestDist = dModel;
                        a = radial[i].E;
                        b = radial[j].E;
                    }
                }
            }

            if (a == null || b == null)
            {
                double bestAng = double.MaxValue;
                for (int i = 0; i < radial.Count; i++)
                {
                    for (int j = i + 1; j < radial.Count; j++)
                    {
                        double d = Math.Abs(NormalizeAngle(radial[i].Ang - radial[j].Ang));
                        if (d < 0.002 || d > 0.5)
                            continue;
                        if (d < bestAng)
                        {
                            bestAng = d;
                            a = radial[i].E;
                            b = radial[j].E;
                            bestDist = Math.Abs(radial[i].Mid[0] - radial[j].Mid[0]) / scale;
                        }
                    }
                }
            }

            if (a == null || b == null)
            {
                log($"  Weld gap: could not pair seam edges on {view.GetName2()}.");
                return;
            }

            // Midline through the weld gap (between the two cut faces).
            bool clOk = CylindricalDimCenterlinesLegacy.TryInsertCenterlineBetweenEdges(
                h, drawing, view, a, b);
            log(clOk
                ? $"  Weld gap centerline on {view.GetName2()}."
                : $"  Weld gap centerline failed on {view.GetName2()}.");

            // Linear gap between OUTERMOST tips of the cut (not angular chord of radial edges).
            var (_, minY, _, _) = h.ComputeEdgesBoundingBox(edges, view);
            Vertex? vA = FartherVertexFromCenter(h, a, view, center);
            Vertex? vB = FartherVertexFromCenter(h, b, view, center);
            double[] mA = h.GetEdgeMidpointOnSheet(a, view);
            double[] mB = h.GetEdgeMidpointOnSheet(b, view);
            double tx = (mA[0] + mB[0]) / 2.0;
            double ty = minY - DimOffset;

            h.ClearSelection();
            bool linearOk = false;
            bool selected =
                vA != null && vB != null &&
                h.SelectVertex(vA, view, false) && h.SelectVertex(vB, view, true);

            if (!selected)
            {
                h.ClearSelection();
                selected = h.SelectEdge(a, view, false) && h.SelectEdge(b, view, true);
            }

            if (selected)
            {
                // Force linear — AddDimension2 on two radial edges becomes angular (~0.38°).
                DisplayDimension? dim = h.CreateLinearDimension(tx, ty);
                if (dim != null && dim.Type2 == (int)SolidWorks.Interop.swconst.swDimensionType_e.swLinearDimension)
                {
                    linearOk = true;
                    log($"  Weld gap linear ≈{bestDist * 1000:F2} mm (outer tips) on {view.GetName2()}.");
                }
                else if (dim != null)
                {
                    try
                    {
                        if (dim.GetAnnotation() is IAnnotation ann)
                        {
                            h.ClearSelection();
                            ann.Select3(false, null);
                            h.Model.Extension.DeleteSelection2(0);
                        }
                    }
                    catch { /* ignore */ }
                    log($"  Weld gap: got non-linear dim — deleted on {view.GetName2()}.");
                }
            }

            if (linearOk || clOk)
                h.DimensionedFeatures.Add("LoftWeldGap");
            else
                log($"  Weld gap: linear Create failed on {view.GetName2()}.");
        }

        private static Vertex? FartherVertexFromCenter(
            SmartDimHelper h,
            Edge edge,
            IView view,
            double[] centerSheet)
        {
            Vertex? sv = edge.GetStartVertex() as Vertex;
            Vertex? ev = edge.GetEndVertex() as Vertex;
            if (sv == null && ev == null)
                return null;
            if (sv == null) return ev;
            if (ev == null) return sv;

            double[] ps = h.TransformToSheet((double[])sv.GetPoint(), view);
            double[] pe = h.TransformToSheet((double[])ev.GetPoint(), view);
            double ds = Dist2(ps, centerSheet);
            double de = Dist2(pe, centerSheet);
            return ds >= de ? sv : ev;
        }

        private static double Dist2(double[] a, double[] b)
        {
            double dx = a[0] - b[0];
            double dy = a[1] - b[1];
            return dx * dx + dy * dy;
        }

        // ── Side view: height + OD + ID ───────────────────────────────────
    }
}
