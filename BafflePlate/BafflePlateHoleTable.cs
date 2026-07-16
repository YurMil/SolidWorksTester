using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.BafflePlate
{
    /// <summary>
    /// Inserts a hole table on the primary face view (P3 alternative to ordinate chains).
    /// Requires marked preselection per API: datum=1, face=2, X-edge=4, Y-edge=8.
    /// </summary>
    internal static class BafflePlateHoleTable
    {
        private const int MarkDatum = 1;
        private const int MarkHoleFace = 2;
        private const int MarkAxisX = 4;
        private const int MarkAxisY = 8;

        private static readonly string[] PreferredTemplateNames =
        {
            "hole table--sizes combined--numbers.sldholtbt",
            "hole table--sizes combined--letters.sldholtbt",
            "standard hole table--numbers.sldholtbt",
            "standard hole table--letters.sldholtbt",
            "standard-hole.sldholtbt"
        };

        public static bool TryInsert(
            ISldWorks swApp,
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView? primaryFlatView,
            Action<string> log)
        {
            if (primaryFlatView == null)
            {
                log("  Hole table: no primary view.");
                return false;
            }

            string viewName = primaryFlatView.GetName2();
            if (!TryResolveTemplate(swApp, out string templatePath, out string templateLabel))
            {
                log("  Hole table: no .sldholtbt template found.");
                return false;
            }

            if (!TryGetPartDoc(primaryFlatView, out PartDoc part))
            {
                log("  Hole table: no referenced part.");
                return false;
            }

            if (!TryFindDatumEntities(part, log, out Face2 holeFace, out Vertex origin, out Edge edgeX, out Edge edgeY))
            {
                log("  Hole table: could not resolve origin / X / Y / hole face.");
                return false;
            }

            ResolveTablePlacement(drawing, out double x, out double y);
            log($"  Hole table: template «{templateLabel}» at ({x:F3}, {y:F3}) on {viewName}.");
            log("  Hole table: selecting datum(1) + face(2) + X(4) + Y(8)...");

            model.ClearSelection2(true);
            drawing.ActivateView(viewName);

            if (!TrySelectMarked(model, primaryFlatView, origin, MarkDatum, append: false, log, "datum vertex") ||
                !TrySelectMarked(model, primaryFlatView, edgeX, MarkAxisX, append: true, log, "X-axis edge") ||
                !TrySelectMarked(model, primaryFlatView, edgeY, MarkAxisY, append: true, log, "Y-axis edge") ||
                !TrySelectMarked(model, primaryFlatView, holeFace, MarkHoleFace, append: true, log, "hole face"))
            {
                model.ClearSelection2(true);
                return false;
            }

            HoleTableAnnotation? table = TryInsertWithTemplate(primaryFlatView, x, y, templatePath);
            if (table == null && !string.IsNullOrEmpty(templatePath))
            {
                log("  Hole table: retry InsertHoleTable2 with empty template path...");
                table = TryInsertWithTemplate(primaryFlatView, x, y, string.Empty);
            }

            model.ClearSelection2(true);

            if (table == null)
            {
                log("  Hole table: InsertHoleTable2 returned null after marked selection.");
                return false;
            }

            log("  Hole table: created.");
            return true;
        }

        private static HoleTableAnnotation? TryInsertWithTemplate(
            IView view,
            double x,
            double y,
            string templatePath)
        {
            try
            {
                return view.InsertHoleTable2(
                    false,
                    x,
                    y,
                    (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft,
                    "A",
                    templatePath);
            }
            catch
            {
                return null;
            }
        }

        private static bool TrySelectMarked(
            IModelDoc2 model,
            IView view,
            object modelEntity,
            int mark,
            bool append,
            Action<string> log,
            string label)
        {
            try
            {
                object? corresponding = null;
                try
                {
                    corresponding = view.GetCorresponding(modelEntity);
                }
                catch
                {
                    // fall through — select model entity in view context
                }

                Entity? entity = (corresponding as Entity) ?? (modelEntity as Entity);
                if (entity == null)
                {
                    log($"  Hole table: {label} is not an Entity.");
                    return false;
                }

                SelectionMgr selMgr = (SelectionMgr)model.SelectionManager;
                SelectData selData = (SelectData)selMgr.CreateSelectData();
                selData.View = (SolidWorks.Interop.sldworks.View)view;
                selData.Mark = mark;

                bool ok = entity.Select4(append, selData);
                if (!ok)
                {
                    // Fallback used by CodeStack samples.
                    ok = entity.SelectByMark(append, mark);
                }

                if (!ok)
                    log($"  Hole table: failed to select {label} (mark={mark}).");

                return ok;
            }
            catch (Exception ex)
            {
                log($"  Hole table: select {label} failed ({ex.Message}).");
                return false;
            }
        }

        /// <summary>
        /// Largest planar face + two nearly orthogonal linear edges + shared/corner vertex.
        /// </summary>
        private static bool TryFindDatumEntities(
            PartDoc part,
            Action<string> log,
            out Face2 holeFace,
            out Vertex origin,
            out Edge edgeX,
            out Edge edgeY)
        {
            holeFace = null!;
            origin = null!;
            edgeX = null!;
            edgeY = null!;

            Face2? bestFace = null;
            double bestArea = 0;

            object[]? bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null)
                return false;

            foreach (object bodyObj in bodies)
            {
                if (bodyObj is not Body2 body)
                    continue;

                Face2? face = body.GetFirstFace() as Face2;
                while (face != null)
                {
                    try
                    {
                        Surface? surf = face.GetSurface() as Surface;
                        if (surf != null && surf.IsPlane())
                        {
                            double area = face.GetArea();
                            if (area > bestArea)
                            {
                                bestArea = area;
                                bestFace = face;
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

            if (bestFace == null || bestArea < 1e-6)
            {
                log("  Hole table: no planar face found.");
                return false;
            }

            holeFace = bestFace;
            log($"  Hole table: hole face area={bestArea:F3} m².");

            object[]? edgesObj = holeFace.GetEdges() as object[];
            if (edgesObj == null || edgesObj.Length == 0)
                return false;

            var linear = new List<(Edge Edge, double Length, double[] Dir)>();
            foreach (object eo in edgesObj)
            {
                if (eo is not Edge edge)
                    continue;

                if (!TryGetLinearEdge(edge, out double len, out double[] dir) || len < 0.005)
                    continue;

                linear.Add((edge, len, dir));
            }

            if (linear.Count < 2)
            {
                log($"  Hole table: need ≥2 linear edges on face (found {linear.Count}).");
                return false;
            }

            linear.Sort((a, b) => b.Length.CompareTo(a.Length));
            edgeX = linear[0].Edge;
            double[] dirX = linear[0].Dir;

            Edge? bestY = null;
            double bestYLen = 0;
            foreach (var cand in linear.Skip(1))
            {
                double absDot = Math.Abs(Dot(dirX, cand.Dir));
                if (absDot > 0.25) // not ~perpendicular
                    continue;

                if (cand.Length > bestYLen)
                {
                    bestYLen = cand.Length;
                    bestY = cand.Edge;
                }
            }

            if (bestY == null)
            {
                // Fallback: second-longest edge even if not perfectly orthogonal.
                edgeY = linear[1].Edge;
                log("  Hole table: Y-edge not orthogonal — using 2nd longest linear edge.");
            }
            else
            {
                edgeY = bestY;
            }

            if (!TryFindSharedOrStartVertex(edgeX, edgeY, out origin))
            {
                log("  Hole table: no vertex for datum.");
                return false;
            }

            return true;
        }

        private static bool TryGetLinearEdge(Edge edge, out double length, out double[] direction)
        {
            length = 0;
            direction = new[] { 1.0, 0.0, 0.0 };
            try
            {
                Curve? curve = edge.GetCurve() as Curve;
                if (curve == null || !curve.IsLine())
                    return false;

                Vertex? sv = edge.GetStartVertex() as Vertex;
                Vertex? ev = edge.GetEndVertex() as Vertex;
                if (sv == null || ev == null)
                    return false;

                double[] a = (double[])sv.GetPoint();
                double[] b = (double[])ev.GetPoint();
                double dx = b[0] - a[0];
                double dy = b[1] - a[1];
                double dz = b[2] - a[2];
                length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (length < 1e-9)
                    return false;

                direction = new[] { dx / length, dy / length, dz / length };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFindSharedOrStartVertex(Edge edgeX, Edge edgeY, out Vertex origin)
        {
            origin = null!;
            var xVerts = GetEdgeVertices(edgeX);
            var yVerts = GetEdgeVertices(edgeY);

            foreach (Vertex xv in xVerts)
            {
                foreach (Vertex yv in yVerts)
                {
                    if (ReferenceEquals(xv, yv))
                    {
                        origin = xv;
                        return true;
                    }

                    try
                    {
                        double[] px = (double[])xv.GetPoint();
                        double[] py = (double[])yv.GetPoint();
                        double d = Math.Sqrt(
                            (px[0] - py[0]) * (px[0] - py[0]) +
                            (px[1] - py[1]) * (px[1] - py[1]) +
                            (px[2] - py[2]) * (px[2] - py[2]));
                        if (d < 1e-7)
                        {
                            origin = xv;
                            return true;
                        }
                    }
                    catch
                    {
                        // next
                    }
                }
            }

            if (xVerts.Count > 0)
            {
                origin = xVerts[0];
                return true;
            }

            return false;
        }

        private static List<Vertex> GetEdgeVertices(Edge edge)
        {
            var list = new List<Vertex>(2);
            if (edge.GetStartVertex() is Vertex sv)
                list.Add(sv);
            if (edge.GetEndVertex() is Vertex ev)
                list.Add(ev);
            return list;
        }

        private static double Dot(double[] a, double[] b) =>
            a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        private static bool TryGetPartDoc(IView view, out PartDoc part)
        {
            part = null!;
            try
            {
                object[]? comps = view.GetVisibleComponents() as object[];
                if (comps != null && comps.Length > 0 &&
                    comps[0] is Component2 comp &&
                    comp.GetModelDoc2() is PartDoc fromComp)
                {
                    part = fromComp;
                    return true;
                }

                if (view.ReferencedDocument is PartDoc fromRef)
                {
                    part = fromRef;
                    return true;
                }
            }
            catch
            {
                // fall through
            }

            return false;
        }

        private static void ResolveTablePlacement(IDrawingDoc drawing, out double x, out double y)
        {
            x = 0.30;
            y = 0.22;

            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            if (sheet?.GetProperties2() is not double[] props || props.Length < 7)
                return;

            x = props[5] * 0.62;
            y = props[6] * 0.55;
        }

        private static bool TryResolveTemplate(
            ISldWorks swApp,
            out string fullPath,
            out string label)
        {
            fullPath = string.Empty;
            label = string.Empty;

            foreach (string dir in EnumerateTemplateDirectories(swApp))
            {
                foreach (string name in PreferredTemplateNames)
                {
                    string path = Path.Combine(dir, name);
                    if (!File.Exists(path))
                        continue;

                    fullPath = path;
                    label = name;
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateTemplateDirectories(ISldWorks swApp)
        {
            string exe = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS";
            try
            {
                string? p = swApp.GetExecutablePath();
                if (!string.IsNullOrWhiteSpace(p))
                    exe = p;
            }
            catch
            {
                // keep default
            }

            string? lang = null;
            try
            {
                lang = swApp.GetCurrentLanguage();
            }
            catch
            {
                // ignore
            }

            var dirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(lang))
                dirs.Add(Path.Combine(exe, "lang", lang));

            dirs.Add(Path.Combine(exe, "lang", "english"));
            dirs.Add(Path.Combine(exe, "lang", "russian"));
            dirs.Add(@"C:\EST\91_SW Setup\Templates");

            return dirs
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
