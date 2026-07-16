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
        private static Face2? GetCorrespondingFace(IView view, Face2 modelFace)
        {
            try
            {
                return view.GetCorresponding(modelFace) as Face2;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetPartDoc(
            SmartDimHelper h,
            IView view,
            out IModelDoc2 partDoc,
            out PartDoc part)
        {
            partDoc = null!;
            part = null!;
            try
            {
                Component2? comp = h.GetViewComponent(view);
                if (comp?.GetModelDoc2() is not IModelDoc2 doc || doc is not PartDoc p)
                    return false;

                partDoc = doc;
                part = p;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadPartBox(PartDoc part, out double[] box)
        {
            box = Array.Empty<double>();
            try
            {
                if (part.GetPartBox(true) is not double[] b || b.Length < 6)
                    return false;
                box = b;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double Dot(double[] a, double[] b) =>
            a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        private static int IndexOfMin(double[] extents)
        {
            int i = 0;
            if (extents[1] < extents[i]) i = 1;
            if (extents[2] < extents[i]) i = 2;
            return i;
        }

        private static string AxisName(int axis) => axis switch
        {
            0 => "X",
            1 => "Y",
            2 => "Z",
            _ => "?"
        };

        private static bool DimensionMatchesThickness(DisplayDimension dim, double expectedMeters)
        {
            try
            {
                Dimension? modelDim = dim.GetDimension2(0) as Dimension;
                if (modelDim == null)
                    return false;

                double value = Math.Abs(modelDim.SystemValue);
                double tol = Math.Max(0.00005, expectedMeters * ValueMatchRelativeTol);
                return Math.Abs(value - expectedMeters) <= tol;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteDisplayDimension(SmartDimHelper h, DisplayDimension dim)
        {
            try
            {
                Annotation? ann = dim.GetAnnotation() as Annotation;
                if (ann == null)
                    return;

                h.ClearSelection();
                ann.Select3(true, null);
                h.Model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                h.ClearSelection();
            }
            catch
            {
                h.ClearSelection();
            }
        }

        private static double? TryReadFromPartDoc(IModelDoc2? partDoc)
        {
            if (partDoc == null)
                return null;

            Feature? feat = partDoc.FirstFeature() as Feature;
            while (feat != null)
            {
                string typeName = feat.GetTypeName2();
                try
                {
                    if (typeName.Equals("SheetMetal", StringComparison.OrdinalIgnoreCase))
                    {
                        if (feat.GetDefinition() is ISheetMetalFeatureData sm)
                        {
                            sm.AccessSelections(partDoc, null);
                            try
                            {
                                double t = sm.Thickness;
                                if (t > MinThicknessModelMeters && t <= MaxThicknessModelMeters)
                                    return t;
                            }
                            finally
                            {
                                sm.ReleaseSelectionAccess();
                            }
                        }
                    }
                    else if (typeName.Equals("BaseFlange", StringComparison.OrdinalIgnoreCase))
                    {
                        if (feat.GetDefinition() is IBaseFlangeFeatureData bf)
                        {
                            bf.AccessSelections(partDoc, null);
                            try
                            {
                                double t = bf.Thickness;
                                if (t > MinThicknessModelMeters && t <= MaxThicknessModelMeters)
                                    return t;
                            }
                            finally
                            {
                                bf.ReleaseSelectionAccess();
                            }
                        }
                    }
                }
                catch
                {
                    // next
                }

                feat = feat.GetNextFeature() as Feature;
            }

            return null;
        }

        private readonly struct PlanarFaceInfo
        {
            public PlanarFaceInfo(Face2 face, double[] normal, double[] point, double area)
            {
                Face = face;
                Normal = normal;
                Point = point;
                Area = area;
            }

            public Face2 Face { get; }
            public double[] Normal { get; }
            public double[] Point { get; }
            public double Area { get; }
        }
    }
}
