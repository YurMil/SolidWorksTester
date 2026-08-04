using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>
    /// Single-pass COM geometry scan: one feature-tree walk, one GetPartBox, one face enumeration.
    /// Import detection and sketch-import disqualifiers are collected in the same feature walk.
    /// </summary>
    internal static class PartGeometryScanner
    {
        private static readonly HashSet<string> BendFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "EdgeFlange", "Hem", "Jog", "SketchBend", "SM3dBend",
            "SMMiteredFlange", "MiterFlange", "LoftedBend", "OneBend", "Fold"
        };

        private static readonly HashSet<string> SheetMetalRootTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "SheetMetal", "BaseFlange", "FlatPattern"
        };

        private static readonly HashSet<string> CylindricalFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Revolve", "RevolveBoss", "RevolveCut", "RevolveThin",
            "Sweep", "SweepBoss", "SweepCut", "SweepThin",
            "ThinRevolve", "ThinSweep", "Loft", "LoftBoss", "LoftCut"
        };

        private static readonly HashSet<string> HoleFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "HoleWiz", "AdvHoleWiz", "HoleSeries", "SimpleHole", "HoleSeriesWizard"
        };

        private static readonly HashSet<string> ChamferFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Chamfer", "Fillet"
        };

        private static readonly HashSet<string> LinearOrFillPatternTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "LPattern", "SketchPattern", "FillPattern", "TablePattern", "CurvePattern",
            "LocalLPattern", "DerivedPattern"
        };

        private static readonly HashSet<string> CircularPatternTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "CirPattern", "CircularPattern"
        };

        private static readonly HashSet<string> ImportFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "MBimport", "BodyFeature", "SolidBody", "ImportSolid", "ImpOrphan"
        };

        private static readonly string[] ImportFileExtensions =
        {
            ".STEP", ".STP", ".IGS", ".IGES", ".X_T", ".X_B", ".SAT", ".PARASOLID", ".VDA", ".JT"
        };

        private static readonly HashSet<string> NativeSolidFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Boss", "BossExtrude", "BossThin", "Cut", "CutExtrude", "CutThin",
            "Extrude", "Revolve", "RevolveBoss", "RevolveCut", "RevolveThin",
            "Sweep", "SweepBoss", "SweepCut", "SweepThin",
            "Loft", "LoftBoss", "LoftCut",
            "BaseBody", "BaseFlange", "EdgeFlange", "SheetMetal"
        };

        private static readonly HashSet<string> IgnoredImportFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "OriginProfileFeature", "Reference", "RefPlane", "RefAxis", "CoordSys",
            "CommentsFolder", "FavoriteFolder", "HistoryFolder", "SelectionSetFolder",
            "SensorFolder", "SolidBodyFolder", "SurfaceBodyFolder", "MateReference",
            "MaterialFolder", "DetailCabinet", "EnvFolder", "InkMarkupFolder",
            "NotesAreaFolder", "DocsFolder", "Attribute", "TitleBlock", "Frame",
            "FlatPatternFolder", "SubWeldFolder", "Weldment", "MeshBodyFeature",
            "MagneticGround", "AmbientLight", "DirectionLight", "SpotLight"
        };

        private static readonly HashSet<string> DisqualifyingSketchImportFeatureTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "MoveFace", "MoveFace2", "MoveBody", "Indent", "BossExtrude", "ExtrudeBoss",
            "Boss", "SweepBoss", "LoftBoss", "RevolveBoss", "BoundaryBoss",
            "Thicken", "CombineBodies", "SplitBody", "Split", "MirrorSolid",
            "ReplaceFace", "DeleteFace", "ScaleFeature", "Freeform", "Dome",
            "RuledSurfaceFromEdge", "LocalChainPattern", "MoveCopyBody", "Cavity",
            "DerivedPartSolid", "InsertPart", "VarFillet", "Wrap", "Flex"
        };

        public static PartGeometrySnapshot Scan(IModelDoc2 partDoc)
        {
            var bendNames = new List<string>();
            bool hasSheetMetal = false;
            int bendCount = 0;
            bool hasCylindricalFeature = false;
            bool hasHoleFeature = false;
            bool hasChamferFeature = false;
            bool hasLoftedBendFeature = false;
            bool hasLinearOrFillPattern = false;
            bool hasCircularPattern = false;
            bool hasDisqualifyingSketchImport = false;

            int importCount = 0;
            int nativeCount = 0;
            string primaryImportName = string.Empty;

            Feature? feat = partDoc.FirstFeature() as Feature;
            while (feat != null)
            {
                string typeName = feat.GetTypeName2() ?? string.Empty;
                string featName = feat.Name ?? string.Empty;

                if (SheetMetalRootTypes.Contains(typeName))
                    hasSheetMetal = true;

                if (BendFeatureTypes.Contains(typeName))
                {
                    bendCount++;
                    bendNames.Add(featName);
                }

                if (typeName.Equals("LoftedBend", StringComparison.OrdinalIgnoreCase) ||
                    featName.Contains("Lofted Bend", StringComparison.OrdinalIgnoreCase))
                {
                    hasLoftedBendFeature = true;
                }

                if (CylindricalFeatureTypes.Contains(typeName))
                    hasCylindricalFeature = true;

                if (HoleFeatureTypes.Contains(typeName))
                    hasHoleFeature = true;

                if (ChamferFeatureTypes.Contains(typeName))
                    hasChamferFeature = true;

                if (CircularPatternTypes.Contains(typeName))
                    hasCircularPattern = true;
                else if (LinearOrFillPatternTypes.Contains(typeName))
                    hasLinearOrFillPattern = true;

                if (!hasDisqualifyingSketchImport &&
                    DisqualifyingSketchImportFeatureTypes.Contains(typeName))
                    hasDisqualifyingSketchImport = true;

                if (!IgnoredImportFeatureTypes.Contains(typeName))
                {
                    if (IsImportFeature(feat, typeName, featName))
                    {
                        importCount++;
                        if (string.IsNullOrEmpty(primaryImportName))
                            primaryImportName = featName;
                    }
                    else if (NativeSolidFeatureTypes.Contains(typeName))
                    {
                        nativeCount++;
                    }
                }

                feat = feat.GetNextFeature() as Feature;
            }

            var importDetection = new ImportedGeometryDetector.ImportDetectionResult
            {
                IsImported = importCount > 0 && nativeCount == 0,
                ImportFeatureCount = importCount,
                NativeSolidFeatureCount = nativeCount,
                PrimaryImportFeatureName = primaryImportName
            };

            ReadBbox(partDoc, out double[] sortedDims, out double cx, out double cy, out double cz);

            // Sheet-metal + linear/fill pattern + thin flat bbox → baffle without enumerating
            // thousands of hole faces (classification only needs pattern + thin plate).
            bool thinFlat = sortedDims[0] >= 0.0004 && sortedDims[0] <= 0.080 &&
                            sortedDims[1] / Math.Max(sortedDims[0], 1e-12) >= 8.0;
            bool skipFaceScan = hasSheetMetal && hasLinearOrFillPattern && thinFlat && bendCount == 0;
            // Flat SM plate without bends: kind is FlatPlate; face scan only decides sub-kind.
            // Stop once baffle is proven (≥40 similar small holes) or enough cylinders to
            // rule out rounded-end (≥80) — sketch-cut hole arrays have no LPattern/FillPattern.
            bool earlyExitFaceScan = !skipFaceScan && hasSheetMetal && thinFlat && bendCount == 0;
            const int earlyExitCylinderCap = 80;

            var cylinders = new List<CylinderFaceSample>(skipFaceScan ? 0 : 256);
            int planarFaces = 0;
            int solidBodyCount = 0;
            var smallHoleCounts = earlyExitFaceScan
                ? new Dictionary<double, int>(64)
                : null;
            int dominantSmallLive = 0;
            bool faceScanStoppedEarly = false;

            if (partDoc is PartDoc part)
            {
                object[]? bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                if (bodies != null)
                {
                    solidBodyCount = bodies.Length;
                    if (!skipFaceScan)
                    {
                        foreach (object bodyObj in bodies)
                        {
                            if (faceScanStoppedEarly)
                                break;

                            if (bodyObj is not Body2 body)
                                continue;

                            object[]? faces = body.GetFaces() as object[];
                            if (faces == null)
                                continue;

                            foreach (object faceObj in faces)
                            {
                                if (faceObj is not Face2 face)
                                    continue;

                                Surface? surf = face.GetSurface() as Surface;
                                if (surf == null)
                                    continue;

                                if (surf.IsPlane())
                                {
                                    planarFaces++;
                                    continue;
                                }

                                if (!surf.IsCylinder())
                                    continue;

                                try
                                {
                                    double[] param = (double[])surf.CylinderParams;
                                    double radius = param[6];
                                    cylinders.Add(new CylinderFaceSample(
                                        radius, param[0], param[1], param[2]));

                                    if (smallHoleCounts != null &&
                                        radius >= 0.0015 &&
                                        radius <= PartGeometrySnapshot.MaxSmallHoleRadiusMeters)
                                    {
                                        double key = Math.Round(radius, 4);
                                        smallHoleCounts.TryGetValue(key, out int n);
                                        n++;
                                        smallHoleCounts[key] = n;
                                        if (n > dominantSmallLive)
                                            dominantSmallLive = n;
                                    }

                                    if (earlyExitFaceScan &&
                                        (dominantSmallLive >= PartGeometrySnapshot.MinDenseHoleCount ||
                                         cylinders.Count >= earlyExitCylinderCap))
                                    {
                                        faceScanStoppedEarly = true;
                                        break;
                                    }
                                }
                                catch
                                {
                                    // ignore invalid cylinder params
                                }
                            }
                        }
                    }
                }
            }

            int cylindricalFaces;
            bool hasHoles;
            bool isHollow;
            int smallCylinderFaces;
            int largeCylinderFaces;
            bool hasLargeOuter;
            int dominantSmallHoles;

            if (skipFaceScan)
            {
                cylindricalFaces = 0;
                planarFaces = 0;
                hasHoles = hasHoleFeature || hasLinearOrFillPattern;
                isHollow = false;
                smallCylinderFaces = 0;
                largeCylinderFaces = 0;
                hasLargeOuter = false;
                dominantSmallHoles = PartGeometrySnapshot.MinDenseHoleCount;
            }
            else
            {
                PartGeometrySnapshot.ComputeCylinderDerived(
                    cylinders,
                    hasHoleFeature,
                    out cylindricalFaces,
                    out hasHoles,
                    out isHollow,
                    out smallCylinderFaces,
                    out largeCylinderFaces,
                    out hasLargeOuter,
                    out dominantSmallHoles);
            }

            return new PartGeometrySnapshot
            {
                Cylinders = cylinders,
                PlanarFaces = planarFaces,
                SolidBodyCount = solidBodyCount,
                BboxSortedDims = sortedDims,
                BboxCenterX = cx,
                BboxCenterY = cy,
                BboxCenterZ = cz,
                HasSheetMetal = hasSheetMetal,
                BendCount = bendCount,
                BendNames = bendNames,
                HasCylindricalFeature = hasCylindricalFeature,
                HasHoleFeature = hasHoleFeature,
                HasChamferFeature = hasChamferFeature,
                HasLoftedBendFeature = hasLoftedBendFeature,
                HasLinearOrFillPattern = hasLinearOrFillPattern,
                HasCircularPattern = hasCircularPattern,
                HasDisqualifyingSketchImportFeatures = hasDisqualifyingSketchImport,
                ImportDetection = importDetection,
                CylindricalFaces = cylindricalFaces,
                HasHoles = hasHoles,
                IsHollow = isHollow,
                SmallCylinderFaces = smallCylinderFaces,
                LargeCylinderFaces = largeCylinderFaces,
                HasLargeOuterCylinderFace = hasLargeOuter,
                DominantSimilarSmallHoleCount = dominantSmallHoles
            };
        }

        private static bool IsImportFeature(Feature feat, string typeName, string featName)
        {
            if (ImportFeatureTypes.Contains(typeName))
                return true;

            if (typeName.StartsWith("Ice", StringComparison.OrdinalIgnoreCase))
                return true;

            if (featName.StartsWith("Imported", StringComparison.OrdinalIgnoreCase))
                return true;

            if (FeatureNameLooksLikeForeignCad(featName))
                return true;

            try
            {
                if (feat.Is3DInterconnectFeature)
                    return true;
            }
            catch
            {
                // ignore COM failures on older features
            }

            return false;
        }

        private static bool FeatureNameLooksLikeForeignCad(string featName)
        {
            if (string.IsNullOrWhiteSpace(featName))
                return false;

            string upper = featName.ToUpperInvariant();
            foreach (string ext in ImportFileExtensions)
            {
                if (upper.Contains(ext))
                    return true;
            }

            return false;
        }

        private static void ReadBbox(
            IModelDoc2 partDoc,
            out double[] sortedDims,
            out double cx,
            out double cy,
            out double cz)
        {
            sortedDims = new[] { 0.0, 0.0, 0.0 };
            cx = cy = cz = 0;

            if (partDoc is not PartDoc part)
                return;

            try
            {
                double[] box = (double[])part.GetPartBox(true);
                cx = (box[0] + box[3]) / 2.0;
                cy = (box[1] + box[4]) / 2.0;
                cz = (box[2] + box[5]) / 2.0;
                sortedDims =
                [
                    Math.Abs(box[3] - box[0]),
                    Math.Abs(box[4] - box[1]),
                    Math.Abs(box[5] - box[2])
                ];
                Array.Sort(sortedDims);
            }
            catch
            {
                // leave zeros
            }
        }
    }
}
