using System;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Drawing;

namespace SolidWorksTester.Cylindrical
{
    /// <summary>
    /// Imports driving model dimensions once into the primary view only.
    /// </summary>
    public static class CylindricalDimModelImport
    {
        public static void ImportOnce(
            IModelDoc2 model,
            IDrawingDoc drawing,
            IView primaryView,
            Action<string> log) =>
            DrawingModelDimensionImport.ImportOnce(model, drawing, primaryView, log);
    }
}
