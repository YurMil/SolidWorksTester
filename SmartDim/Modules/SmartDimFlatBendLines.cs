using System;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    /// <summary>
    /// Module G: Flat pattern bend line dimensions — distance from outer edge to each bend line.
    /// Applied to flat pattern views ONLY.
    /// Uses IView.GetBendLines() to access bend line sketch segments.
    /// Creates 1 dimension per bend line (from nearest outer edge).
    /// </summary>
    public static class SmartDimFlatBendLines
    {
        public static void Add(SmartDimHelper h, IView view, Action<string>? log = null)
        {
            string viewName = view.GetName2();
            log?.Invoke($"  [FlatBend] Adding bend line dimensions to flat pattern: {viewName}");

            if (!view.IsFlatPatternView())
            {
                log?.Invoke($"  [FlatBend] View is not a flat pattern — skipping");
                return;
            }

            int bendLineCount = view.GetBendLineCount();
            log?.Invoke($"  [FlatBend] Found {bendLineCount} bend line(s)");

            if (bendLineCount == 0) return;

            object[]? bendLines = view.GetBendLines() as object[];
            if (bendLines == null || bendLines.Length == 0)
            {
                log?.Invoke($"  [FlatBend] GetBendLines() returned null or empty");
                return;
            }

            // Get all visible edges to find the outer boundary
            Edge[] allEdges = h.GetViewEdges(view);
            var linearEdges = allEdges.Where(e => h.IsLinear(e)).ToArray();
            var (minX, minY, maxX, maxY) = h.ComputeEdgesBoundingBox(allEdges, view);

            // Find the best left boundary edge (vertical, closest to minX)
            Edge leftBoundary = linearEdges
                .Where(e => h.IsVerticalInView(e, view))
                .OrderBy(e => {
                    var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                    return Math.Abs((s[0] + end[0]) / 2.0 - minX);
                })
                .FirstOrDefault();

            // Find the best bottom boundary edge (horizontal, closest to minY)
            Edge bottomBoundary = linearEdges
                .Where(e => h.IsHorizontalInView(e, view))
                .OrderBy(e => {
                    var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                    return Math.Abs((s[1] + end[1]) / 2.0 - minY);
                })
                .FirstOrDefault();

            // Find the best right boundary edge (vertical, closest to maxX)
            Edge rightBoundary = linearEdges
                .Where(e => h.IsVerticalInView(e, view))
                .OrderBy(e => {
                    var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                    return Math.Abs((s[0] + end[0]) / 2.0 - maxX);
                })
                .FirstOrDefault();

            // Find the best top boundary edge (horizontal, closest to maxY)
            Edge topBoundary = linearEdges
                .Where(e => h.IsHorizontalInView(e, view))
                .OrderBy(e => {
                    var (s, end) = h.GetEdgeEndpointsOnSheet(e, view);
                    return Math.Abs((s[1] + end[1]) / 2.0 - maxY);
                })
                .FirstOrDefault();

            if (leftBoundary == null || bottomBoundary == null || rightBoundary == null || topBoundary == null)
            {
                log?.Invoke($"  [FlatBend] WARNING: Missing one or more boundary edges");
                return;
            }

            HashSet<string> dimensionedPositions = new HashSet<string>();
            int dimCount = 0;
            foreach (object obj in bendLines)
            {
                SketchSegment bendSeg = (SketchSegment)obj;
                SketchLine bendLine = bendSeg as SketchLine;
                if (bendLine == null) continue;

                try
                {
                    SketchPoint pt1 = (SketchPoint)bendLine.GetStartPoint2();
                    SketchPoint pt2 = (SketchPoint)bendLine.GetEndPoint2();
                    double dx = Math.Abs(pt1.X - pt2.X);
                    double dy = Math.Abs(pt1.Y - pt2.Y);
                    bool isHoriz = dx > dy; // Orientation in sketch space

                    Edge refEdge;
                    double dimX, dimY;
                    double mappedX = 0, mappedY = 0;
                    
                    if (isHoriz)
                    {
                        // For horizontal bend lines, dimension from Top or Bottom (whichever is closer)
                        double bendY = (pt1.Y + pt2.Y) / 2.0;
                        // Map bendY to sheet coordinates (rough approximation: assume drawing center is 0,0 or compare relative to bounding box)
                        // Actually, bendLine coords are in sketch space (part space). 
                        // But wait! minX/minY are in sheet space. 
                        // We must compare in part space, or project the bend line midpoint to sheet space!
                        // Let's project the bend line start point to sheet space:
                        double[] ptOnSheet = h.TransformToSheet(new double[] { pt1.X, pt1.Y, pt1.Z }, view);
                        
                        mappedY = ptOnSheet[1];
                        mappedX = ptOnSheet[0];
                        double distToTop = Math.Abs(maxY - mappedY);
                        double distToBottom = Math.Abs(mappedY - minY);
                        
                        if (distToTop < distToBottom)
                        {
                            refEdge = topBoundary;
                            dimX = (minX + maxX) / 2.0;
                            dimY = maxY + 0.015 + dimCount * 0.005;
                        }
                        else
                        {
                            refEdge = bottomBoundary;
                            dimX = (minX + maxX) / 2.0;
                            dimY = minY - 0.015 - dimCount * 0.005;
                        }
                    }
                    else
                    {
                        // For vertical bend lines, dimension from Left or Right (whichever is closer)
                        double[] ptOnSheet = h.TransformToSheet(new double[] { pt1.X, pt1.Y, pt1.Z }, view);
                        
                        mappedX = ptOnSheet[0];
                        mappedY = ptOnSheet[1];
                        double distToRight = Math.Abs(maxX - mappedX);
                        double distToLeft = Math.Abs(mappedX - minX);
                        
                        if (distToRight < distToLeft)
                        {
                            refEdge = rightBoundary;
                            dimX = maxX + 0.015 + dimCount * 0.005;
                            dimY = (minY + maxY) / 2.0;
                        }
                        else
                        {
                            refEdge = leftBoundary;
                            dimX = minX - 0.015 - dimCount * 0.005;
                            dimY = (minY + maxY) / 2.0;
                        }
                    }

                    if (refEdge == null) continue;

                    string posKey = isHoriz ? $"H_{Math.Round(mappedY, 3)}" : $"V_{Math.Round(mappedX, 3)}";
                    if (dimensionedPositions.Contains(posKey)) continue;

                    h.ClearSelection();
                    h.SelectEdge(refEdge, view, false);
                    h.SelectSketchSegment(bendSeg, view, true);
                    
                    var dim = h.CreateDimension(dimX, dimY);
                    if (dim != null)
                    {
                        dimCount++;
                        dimensionedPositions.Add(posKey);
                        log?.Invoke($"  [FlatBend] Bend line {dimCount} dimension created (Horiz={isHoriz})");
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke($"  [FlatBend] WARNING: Failed to dimension bend line: {ex.Message}");
                }
            }

            log?.Invoke($"  [FlatBend] Total bend line dimensions created: {dimCount}");
            h.ClearSelection();
        }
    }
}
