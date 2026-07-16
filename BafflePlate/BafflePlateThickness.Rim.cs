using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.BafflePlate
{
    /// <summary>
    /// Fast gabarit thickness for dense baffle plates.
    /// Prefers Level 2: model planar faces / outer-rim edges → IView.GetCorresponding → SelectFace/Edge.
    /// Pixel picks fail at small sheet separation (e.g. 6 mm @ 1:20 ≈ 0.3 mm).
    /// </summary>
    internal static partial class BafflePlateThickness
    {
        private static bool TryDimensionByOuterRimEdges(
            SmartDimHelper h,
            IView view,
            double? expectedThicknessModel,
            Action<string> log)
        {
            if (!TryGetPartDoc(h, view, out _, out PartDoc part))
                return false;

            if (!TryReadPartBox(part, out double[] box))
                return false;

            double[] extents = { box[3] - box[0], box[4] - box[1], box[5] - box[2] };
            int thinAxis = IndexOfMin(extents);
            double maxExtent = Math.Max(extents[0], Math.Max(extents[1], extents[2]));
            // Accept first cylinder near OD — do not scan thousands of hole walls for the absolute max.
            double targetOuterR = maxExtent * 0.5 * 0.85;

            if (!TryFindOuterRimEdges(part, targetOuterR, thinAxis, out Edge? edgeA, out Edge? edgeB, out double[] midA, out double[] midB))
            {
                log("    outer-rim: no cylinder rim pair near OD.");
                return false;
            }

            // GetCorresponding often returns null for these edges in HLV side views;
            // SelectEntity / Select4+View (via SelectEdge) maps model → view reliably.
            double[] sheetA = h.TransformToSheet(midA, view);
            double[] sheetB = h.TransformToSheet(midB, view);
            bool thicknessIsVertical =
                Math.Abs(sheetA[1] - sheetB[1]) >= Math.Abs(sheetA[0] - sheetB[0]);

            double textX = thicknessIsVertical
                ? Math.Max(sheetA[0], sheetB[0]) + DimOffset
                : (sheetA[0] + sheetB[0]) / 2.0;
            double textY = thicknessIsVertical
                ? (sheetA[1] + sheetB[1]) / 2.0
                : Math.Max(sheetA[1], sheetB[1]) + DimOffset;

            h.ClearSelection();
            if (!h.SelectEdge(edgeA!, view, false) || !h.SelectEdge(edgeB!, view, true))
            {
                log("    outer-rim: SelectEdge failed.");
                h.ClearSelection();
                return false;
            }

            DisplayDimension? dim = h.CreateDimension(textX, textY);
            h.ClearSelection();
            if (dim == null)
            {
                log("    outer-rim: CreateDimension null.");
                return false;
            }

            if (expectedThicknessModel.HasValue &&
                !DimensionMatchesThickness(dim, expectedThicknessModel.Value))
            {
                log($"    outer-rim: reject value (not ≈ {expectedThicknessModel.Value * 1000:F2} mm).");
                TryDeleteDisplayDimension(h, dim);
                return false;
            }

            log("    outer-rim: OK (early-exit OD search + SelectEntity).");
            return true;
        }

        /// <summary>
        /// Finds two circular edges on the first outer-ish cylindrical face (r ≥ targetOuterR).
        /// Early-exits — does not walk the rest of a dense hole array.
        /// </summary>
        private static bool TryFindOuterRimEdges(
            PartDoc part,
            double targetOuterR,
            int thinAxis,
            out Edge? edgeA,
            out Edge? edgeB,
            out double[] midA,
            out double[] midB)
        {
            edgeA = edgeB = null;
            midA = midB = Array.Empty<double>();

            Face2? outerFace = null;

            object[]? bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null)
                return false;

            foreach (object bodyObj in bodies)
            {
                if (outerFace != null)
                    break;

                if (bodyObj is not Body2 body)
                    continue;

                Face2? face = body.GetFirstFace() as Face2;
                while (face != null)
                {
                    try
                    {
                        Surface? surf = face.GetSurface() as Surface;
                        if (surf != null && surf.IsCylinder())
                        {
                            double r = ((double[])surf.CylinderParams)[6];
                            if (r >= targetOuterR)
                            {
                                outerFace = face;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // next
                    }

                    face = face.GetNextFace() as Face2;
                }
            }

            if (outerFace == null)
                return false;

            object[]? edges = outerFace.GetEdges() as object[];
            if (edges == null || edges.Length < 2)
                return false;

            var circles = new List<(Edge Edge, double[] Center)>(4);
            foreach (object edgeObj in edges)
            {
                if (edgeObj is not Edge edge)
                    continue;

                try
                {
                    Curve? curve = edge.GetCurve() as Curve;
                    if (curve == null || !curve.IsCircle())
                        continue;

                    double[] cp = (double[])curve.CircleParams;
                    circles.Add((edge, new[] { cp[0], cp[1], cp[2] }));
                }
                catch
                {
                    // next
                }
            }

            if (circles.Count < 2)
                return false;

            double bestDist = -1;
            for (int i = 0; i < circles.Count; i++)
            {
                for (int j = i + 1; j < circles.Count; j++)
                {
                    double d = Math.Abs(circles[i].Center[thinAxis] - circles[j].Center[thinAxis]);
                    if (d > bestDist)
                    {
                        bestDist = d;
                        edgeA = circles[i].Edge;
                        edgeB = circles[j].Edge;
                        midA = circles[i].Center;
                        midB = circles[j].Center;
                    }
                }
            }

            return edgeA != null && edgeB != null && bestDist >= MinThicknessModelMeters;
        }

        // ── Shared helpers ─────────────────────────────────────────────────
    }
}
