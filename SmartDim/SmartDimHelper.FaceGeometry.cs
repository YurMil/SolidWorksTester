using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SolidWorksTester
{
    public partial class SmartDimHelper
    {
        // ── Face geometry (centerlines) ──────────────────────────────────

        /// <summary>True when the face surface is a cylinder.</summary>
        public bool IsCylindricalFace(Face2 face)
        {
            try
            {
                Surface? surf = face.GetSurface() as Surface;
                return surf != null && surf.IsCylinder();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Cylinder radius from a cylindrical face (meters).</summary>
        public double GetCylinderFaceRadius(Face2 face)
        {
            Surface surf = (Surface)face.GetSurface();
            double[] param = (double[])surf.CylinderParams;
            return param[6];
        }

        /// <summary>Largest visible cylindrical face in the view (typically the outer wall).</summary>
        public Face2? FindBestCylindricalFaceInView(IView view) =>
            GetViewFaces(view)
                .Where(IsCylindricalFace)
                .OrderByDescending(GetCylinderFaceRadius)
                .FirstOrDefault();

        /// <summary>
        /// Sheet pick point for <c>SelectByID2("", "FACE", x, y, z)</c>
        /// (see SOLIDWORKS centerline drawing API example).
        /// </summary>
        public double[] GetCylindricalFacePickPointOnSheet(Face2 face, IView view)
        {
            Surface surf = (Surface)face.GetSurface();
            double[] param = (double[])surf.CylinderParams;
            double[] modelPt = { param[0], param[1], param[2] };
            return TransformToSheet(modelPt, view);
        }
    }
}
