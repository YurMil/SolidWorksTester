using SolidWorks.Interop.sldworks;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester
{
    public partial class SmartDimHelper
    {
        // ── Drawing selection ────────────────────────────────────────────

        /// <summary>Selects an edge in the context of a drawing view.</summary>
        public bool SelectEdge(Edge edge, IView view, bool append) =>
            TrySelectEntity((Entity)edge, view, append);

        /// <summary>Selects a vertex in the context of a drawing view.</summary>
        public bool SelectVertex(Vertex vertex, IView view, bool append) =>
            TrySelectEntity((Entity)vertex, view, append);

        /// <summary>Selects a face in the context of a drawing view.</summary>
        public bool SelectFace(Face2 face, IView view, bool append) =>
            TrySelectEntity((Entity)face, view, append);

        /// <summary>Selects a sketch segment (e.g. bend line) in a drawing view.</summary>
        public bool SelectSketchSegment(SketchSegment seg, IView view, bool append)
        {
            ActivateView(view);
            return seg.Select4(append, CreateViewSelectData(view));
        }

        /// <summary>
        /// Selects the drawing component using <see cref="Component2.GetSelectByIDString"/>.
        /// </summary>
        public bool SelectDrawingComponent(IView view)
        {
            Component2? comp = GetViewComponent(view);
            if (comp == null)
                return false;

            ActivateView(view);
            string selectName = comp.GetSelectByIDString();
            if (string.IsNullOrWhiteSpace(selectName))
                return false;

            return Ext.SelectByID2(
                selectName,
                SmartDimConstants.ComponentSelectType,
                0.0,
                0.0,
                0.0,
                false,
                0,
                null,
                0);
        }

        /// <summary>Selects a face at a sheet-space pick point.</summary>
        public bool SelectFaceBySheetPoint(IView view, double[] sheetPoint)
        {
            ActivateView(view);
            return Ext.SelectByID2(
                string.Empty,
                SmartDimConstants.FaceSelectType,
                sheetPoint[0],
                sheetPoint[1],
                sheetPoint.Length > 2 ? sheetPoint[2] : 0.0,
                false,
                0,
                null,
                0);
        }
    }
}
