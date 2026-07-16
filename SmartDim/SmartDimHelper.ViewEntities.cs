using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester
{
    public partial class SmartDimHelper
    {
        // ── View entities ────────────────────────────────────────────────

        /// <summary>Returns the first visible component in a drawing view (single-body parts).</summary>
        public Component2? GetViewComponent(IView view)
        {
            object[]? comps = view.GetVisibleComponents() as object[];
            if (comps == null || comps.Length == 0)
                return null;

            return comps[0] as Component2;
        }

        /// <summary>Returns visible model edges projected into the drawing view.</summary>
        public Edge[] GetViewEdges(IView view) =>
            GetViewEntities(view, swViewEntityType_e.swViewEntityType_Edge)
                .OfType<Edge>()
                .ToArray();

        /// <summary>Returns silhouette edges (used for drawing centerlines on side views).</summary>
        public Edge[] GetViewSilhouetteEdges(IView view) =>
            GetViewEntities(view, swViewEntityType_e.swViewEntityType_SilhouetteEdge)
                .OfType<Edge>()
                .ToArray();

        /// <summary>Returns visible faces projected into the drawing view.</summary>
        public Face2[] GetViewFaces(IView view) =>
            GetViewEntities(view, swViewEntityType_e.swViewEntityType_Face)
                .OfType<Face2>()
                .ToArray();

        /// <summary>True when the view already contains at least one centerline annotation.</summary>
        public bool HasCenterLineInView(IView view) => view.GetCenterLineCount() > 0;

        private object[] GetViewEntities(IView view, swViewEntityType_e entityType)
        {
            Component2? comp = GetViewComponent(view);
            if (comp == null)
                return Array.Empty<object>();

            object[]? raw = view.GetVisibleEntities2(comp, (int)entityType) as object[];
            return raw ?? Array.Empty<object>();
        }
    }
}
