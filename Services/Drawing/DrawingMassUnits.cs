using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Title-block mass: MMGS reports SW-Mass in grams while EST stamps say kg.
    /// Force mass-property display units to kilograms on the drawing and referenced part.
    /// </summary>
    internal static class DrawingMassUnits
    {
        public static void EnsureKilograms(IModelDoc2 drawingModel, IDrawingDoc drawing, Action<string> log)
        {
            try
            {
                SetMassPropKilograms(drawingModel);
            }
            catch (Exception ex)
            {
                log($"  Warning: could not set drawing mass units to kg ({ex.Message}).");
            }

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                try
                {
                    if (view.ReferencedDocument is IModelDoc2 partDoc)
                        SetMassPropKilograms(partDoc);
                }
                catch
                {
                    // next view
                }

                view = view.GetNextView() as IView;
            }

            log("  Mass property units: kilograms (title-block SW-Mass).");
        }

        private static void SetMassPropKilograms(IModelDoc2 model)
        {
            model.Extension.SetUserPreferenceInteger(
                (int)swUserPreferenceIntegerValue_e.swUnitsMassPropMass,
                0,
                (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Kilograms);
        }
    }
}
