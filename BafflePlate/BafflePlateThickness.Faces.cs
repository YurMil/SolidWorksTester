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
        private static bool TryDimensionByCorrespondingFaces(
            SmartDimHelper h,
            IView view,
            double? expectedThicknessModel,
            Action<string> log)
        {
            if (!TryGetPartDoc(h, view, out IModelDoc2 partDoc, out PartDoc part))
                return false;

            if (!TryReadPartBox(part, out double[] box))
                return false;

            double[] extents = { box[3] - box[0], box[4] - box[1], box[5] - box[2] };
            int thinAxis = IndexOfMin(extents);
            double thickness = expectedThicknessModel ?? extents[thinAxis];
            if (thickness < MinThicknessModelMeters || thickness > MaxThicknessModelMeters)
                return false;

            if (!TryFindThicknessPlanarFaces(part, thinAxis, thickness, out Face2? faceA, out Face2? faceB, out double[] ptA, out double[] ptB))
            {
                log("    GetCorresponding faces: no opposite planar pair near SM thickness.");
                return false;
            }

            Face2? viewFaceA = GetCorrespondingFace(view, faceA!);
            Face2? viewFaceB = GetCorrespondingFace(view, faceB!);
            if (viewFaceA == null || viewFaceB == null)
            {
                log($"    GetCorresponding faces: null in view " +
                    $"(A={(viewFaceA != null)}, B={(viewFaceB != null)}) — not visible?");
                // Fall through: try selecting model faces via SelectEntity.
                viewFaceA ??= faceA;
                viewFaceB ??= faceB;
            }

            double[] sheetA = h.TransformToSheet(ptA, view);
            double[] sheetB = h.TransformToSheet(ptB, view);
            bool thicknessIsVertical =
                Math.Abs(sheetA[1] - sheetB[1]) >= Math.Abs(sheetA[0] - sheetB[0]);

            double textX = thicknessIsVertical
                ? Math.Max(sheetA[0], sheetB[0]) + DimOffset
                : (sheetA[0] + sheetB[0]) / 2.0;
            double textY = thicknessIsVertical
                ? (sheetA[1] + sheetB[1]) / 2.0
                : Math.Max(sheetA[1], sheetB[1]) + DimOffset;

            h.ClearSelection();
            if (!h.SelectFace(viewFaceA!, view, false) || !h.SelectFace(viewFaceB!, view, true))
            {
                log("    GetCorresponding faces: SelectFace failed.");
                h.ClearSelection();
                return false;
            }

            DisplayDimension? dim = h.CreateDimension(textX, textY);
            h.ClearSelection();
            if (dim == null)
            {
                log("    GetCorresponding faces: CreateDimension null.");
                return false;
            }

            if (expectedThicknessModel.HasValue &&
                !DimensionMatchesThickness(dim, expectedThicknessModel.Value))
            {
                log($"    GetCorresponding faces: reject value (not ≈ {expectedThicknessModel.Value * 1000:F2} mm).");
                TryDeleteDisplayDimension(h, dim);
                return false;
            }

            log($"    GetCorresponding faces: OK (thinAxis={AxisName(thinAxis)}).");
            return true;
        }

        private static bool TryFindThicknessPlanarFaces(
            PartDoc part,
            int thinAxis,
            double expectedThickness,
            out Face2? faceA,
            out Face2? faceB,
            out double[] ptA,
            out double[] ptB)
        {
            faceA = faceB = null;
            ptA = ptB = Array.Empty<double>();

            var planes = new List<PlanarFaceInfo>(16);
            object[]? bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null)
                return false;

            double[] thinDir = { 0, 0, 0 };
            thinDir[thinAxis] = 1.0;
            double tol = Math.Max(0.0002, expectedThickness * 0.15);

            foreach (object bodyObj in bodies)
            {
                if (bodyObj is not Body2 body)
                    continue;

                // Prefer GetFirstFace walk — planar count is small on perforated plates.
                Face2? face = body.GetFirstFace() as Face2;
                while (face != null)
                {
                    try
                    {
                        Surface? surf = face.GetSurface() as Surface;
                        if (surf != null && surf.IsPlane() &&
                            TryReadPlane(surf, face, out PlanarFaceInfo info) &&
                            info.Area >= MinThicknessFaceArea &&
                            Math.Abs(Dot(info.Normal, thinDir)) >= 0.95)
                        {
                            planes.Add(info);
                        }
                    }
                    catch
                    {
                        // next face
                    }

                    face = face.GetNextFace() as Face2;
                }
            }

            double bestAreaSum = 0;
            for (int i = 0; i < planes.Count; i++)
            {
                for (int j = i + 1; j < planes.Count; j++)
                {
                    // Outward normals on opposite sides → dot ≈ -1
                    if (Dot(planes[i].Normal, planes[j].Normal) > -0.90)
                        continue;

                    double dist = Math.Abs(Dot(
                        new[]
                        {
                            planes[j].Point[0] - planes[i].Point[0],
                            planes[j].Point[1] - planes[i].Point[1],
                            planes[j].Point[2] - planes[i].Point[2]
                        },
                        planes[i].Normal));

                    if (Math.Abs(dist - expectedThickness) > tol)
                        continue;

                    double areaSum = planes[i].Area + planes[j].Area;
                    if (areaSum > bestAreaSum)
                    {
                        bestAreaSum = areaSum;
                        faceA = planes[i].Face;
                        faceB = planes[j].Face;
                        ptA = planes[i].Point;
                        ptB = planes[j].Point;
                    }
                }
            }

            return faceA != null && faceB != null;
        }

        private static bool TryReadPlane(Surface surf, Face2 face, out PlanarFaceInfo info)
        {
            info = default;
            double[] param = (double[])surf.PlaneParams;
            if (param.Length < 6)
                return false;

            double[] normal = { param[3], param[4], param[5] };
            try
            {
                if (!face.FaceInSurfaceSense())
                {
                    normal[0] = -normal[0];
                    normal[1] = -normal[1];
                    normal[2] = -normal[2];
                }
            }
            catch
            {
                // keep surface sense
            }

            double area;
            try
            {
                area = face.GetArea();
            }
            catch
            {
                area = 0;
            }

            info = new PlanarFaceInfo(
                face,
                normal,
                new[] { param[0], param[1], param[2] },
                area);
            return true;
        }

        // ── Level 2: outer cylinder rim edges (fast path for disc baffles) ─
    }
}
