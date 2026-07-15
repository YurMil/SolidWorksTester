using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Applies drawing view display settings (Hidden Lines Visible, high quality).
    /// </summary>
    internal static class DrawingViewDisplayHelper
    {
        public static void ApplyHiddenLinesVisibleToAllModelViews(
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log)
        {
            log("Setting display mode: Hidden Lines Visible (high quality)...");

            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            int applied = 0;

            while (view != null)
            {
                if (ApplyHiddenLinesVisible(view, log))
                    applied++;

                view = view.GetNextView() as IView;
            }

            if (applied > 0)
            {
                model.ForceRebuild3(true);
                log($"  Hidden Lines Visible applied to {applied} view(s).");
            }
            else
            {
                log("  Warning: could not set Hidden Lines Visible on any view.");
            }
        }

        public static bool ApplyHiddenLinesVisible(IView view, Action<string>? log = null)
        {
            string viewName = view.GetName2();

            try
            {
                int mode = (int)swDisplayMode_e.swHIDDEN_GREYED;
                bool ok = view.SetDisplayMode4(false, mode, false, false, true);

                if (!ok)
                    ok = view.SetDisplayMode3(false, mode, false, false);

                if (ok)
                    log?.Invoke($"  Display mode HLV: {viewName}");
                else
                    log?.Invoke($"  Warning: display mode not changed for {viewName}");

                return ok;
            }
            catch (Exception ex)
            {
                log?.Invoke($"  Warning: display mode failed for {viewName}: {ex.Message}");
                return false;
            }
        }
    }
}
