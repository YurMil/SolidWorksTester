using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    public partial class SmartDimHelper
    {
        // ── Model feature traceability ───────────────────────────────────

        /// <summary>Returns the model feature that owns an edge (via an adjacent face).</summary>
        public Feature? GetEdgeFeature(Edge edge)
        {
            try
            {
                object[]? faces = edge.GetTwoAdjacentFaces2() as object[];
                if (faces == null || faces.Length == 0)
                    return null;

                Face2 face = (Face2)faces[0];
                return face.GetFeature() as Feature;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Returns the SOLIDWORKS feature type name for an edge owner.</summary>
        public string GetEdgeFeatureType(Edge edge) =>
            GetEdgeFeature(edge)?.GetTypeName2() ?? string.Empty;
    }
}
