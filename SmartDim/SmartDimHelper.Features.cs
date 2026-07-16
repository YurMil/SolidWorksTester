using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    public partial class SmartDimHelper
    {
        // Prefer finishing features over the base flange/extrude when an edge
        // sits between a Fillet/Chamfer face and a Base-Flange face.
        private static readonly HashSet<string> PreferredEdgeOwnerTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Fillet", "VarFillet", "ConstCircFillet",
            "Chamfer",
            "HoleWzd", "HoleWizard", "SimpleHole", "AdvHole",
            "Cut", "CutExtrude", "ICE", "CutSweep", "CutLoft", "CutRevolve"
        };

        // ── Model feature traceability ───────────────────────────────────

        /// <summary>
        /// Returns the model feature that owns an edge (via adjacent faces).
        /// When faces disagree (e.g. Fillet arc between Fillet face and Base-Flange),
        /// prefers Fillet/Chamfer/Cut/Hole over the base body feature.
        /// </summary>
        public Feature? GetEdgeFeature(Edge edge)
        {
            try
            {
                object[]? faces = edge.GetTwoAdjacentFaces2() as object[];
                if (faces == null || faces.Length == 0)
                    return null;

                Feature? first = null;
                Feature? corner = null;
                Feature? preferred = null;
                foreach (object fo in faces)
                {
                    if (fo is not Face2 face)
                        continue;

                    Feature? feat = face.GetFeature() as Feature;
                    if (feat == null)
                        continue;

                    first ??= feat;
                    string type = feat.GetTypeName2() ?? string.Empty;
                    if (IsCornerFeatureType(type))
                    {
                        corner = feat;
                        break;
                    }

                    if (preferred == null && IsPreferredEdgeOwner(type))
                        preferred = feat;
                }

                return corner ?? preferred ?? first;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Returns the SOLIDWORKS feature type name for an edge owner.</summary>
        public string GetEdgeFeatureType(Edge edge) =>
            GetEdgeFeature(edge)?.GetTypeName2() ?? string.Empty;

        /// <summary>True when either adjacent face is owned by a Fillet-family feature.</summary>
        public bool EdgeTouchesFilletFeature(Edge edge)
        {
            try
            {
                object[]? faces = edge.GetTwoAdjacentFaces2() as object[];
                if (faces == null)
                    return false;

                foreach (object fo in faces)
                {
                    if (fo is not Face2 face)
                        continue;
                    string type = (face.GetFeature() as Feature)?.GetTypeName2() ?? string.Empty;
                    if (type.Contains("Fillet", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                // ignore COM failures
            }

            return false;
        }

        private static bool IsCornerFeatureType(string type) =>
            !string.IsNullOrEmpty(type) &&
            (type.Equals("Chamfer", StringComparison.OrdinalIgnoreCase) ||
             type.Equals("Fillet", StringComparison.OrdinalIgnoreCase) ||
             type.Equals("VarFillet", StringComparison.OrdinalIgnoreCase) ||
             type.Equals("ConstCircFillet", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("Fillet", StringComparison.OrdinalIgnoreCase));

        private static bool IsPreferredEdgeOwner(string type)
        {
            if (string.IsNullOrEmpty(type))
                return false;
            if (PreferredEdgeOwnerTypes.Contains(type))
                return true;
            return type.Contains("Fillet", StringComparison.OrdinalIgnoreCase) ||
                   type.Contains("Chamfer", StringComparison.OrdinalIgnoreCase) ||
                   type.Contains("Hole", StringComparison.OrdinalIgnoreCase);
        }
    }
}
