using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Drawing;

namespace SolidWorksTester
{
    /// <summary>
    /// Display-dimension creation and center marks.
    /// Related partials:
    /// <list type="bullet">
    /// <item><c>Dimensions.Query</c> — value / type lookup, annotation walk</item>
    /// <item><c>Dimensions.Text</c> — parentheses, <c>Nx</c> / linear prefixes</item>
    /// <item><c>Dimensions.Delete</c> — remove matching dims</item>
    /// </list>
    /// </summary>
    public partial class SmartDimHelper
    {
        /// <summary>
        /// Creates a display dimension at sheet coordinates; entities must be pre-selected.
        /// </summary>
        public DisplayDimension? CreateDimension(double textX, double textY)
        {
            var dim = Model.AddDimension2(textX, textY, 0.0) as DisplayDimension;
            if (dim != null)
                InvalidateDimensionValueCache();
            return dim;
        }

        /// <summary>
        /// Forces a linear dimension (avoids angular when two non-parallel edges are selected).
        /// </summary>
        public DisplayDimension? CreateLinearDimension(double textX, double textY)
        {
            int err = 0;
            DisplayDimension? dim = Ext.AddSpecificDimension(
                textX, textY, 0.0,
                (int)swDimensionType_e.swLinearDimension,
                ref err) as DisplayDimension;
            dim ??= CreateDimension(textX, textY);
            if (dim != null)
                InvalidateDimensionValueCache();
            return dim;
        }

        /// <summary>
        /// Angular dimension; two lines/edges must be pre-selected.
        /// Does not fall back to <see cref="IModelDoc2.AddDimension2"/> (that creates a linear chord).
        /// </summary>
        public DisplayDimension? CreateAngularDimension(double textX, double textY)
        {
            int err = 0;
            return Ext.AddSpecificDimension(
                textX, textY, 0.0,
                (int)swDimensionType_e.swAngularDimension,
                ref err) as DisplayDimension;
        }

        /// <summary>
        /// Diameter dimension on a circular edge (full circle or arc). Prefers diameter display over radius.
        /// </summary>
        public DisplayDimension? CreateDiameterDimension(
            Edge circularEdge,
            IView view,
            double textX,
            double textY)
        {
            ClearSelection();
            if (!SelectEdge(circularEdge, view, false))
                return null;

            DisplayDimension? dim = null;

            if (IsFullCircle(circularEdge))
            {
                dim = Model.AddDimension2(textX, textY, 0.0) as DisplayDimension;
                if (dim != null)
                {
                    InvalidateDimensionValueCache();
                    return dim;
                }
            }

            int err = 0;
            dim = Ext.AddSpecificDimension(
                textX, textY, 0.0,
                (int)swDimensionType_e.swDiameterDimension,
                ref err) as DisplayDimension;

            if (dim != null)
            {
                InvalidateDimensionValueCache();
                return dim;
            }

            dim = Model.AddRadialDimension2(textX, textY, 0.0) as DisplayDimension;
            if (dim != null)
                InvalidateDimensionValueCache();
            return dim;
        }

        /// <summary>Inserts a center mark on a circular edge (hole, arc, or full circle).</summary>
        public bool TryInsertCenterMark(IDrawingDoc drawing, IView view, Edge circularEdge)
        {
            try
            {
                ClearSelection();
                if (!SelectEdge(circularEdge, view, false))
                    return false;

                return DrawingAnnotationWalk.TryInsertCenterMark2(drawing, 1, true);
            }
            catch
            {
                return false;
            }
            finally
            {
                ClearSelection();
            }
        }
    }
}
