using System;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Drawing;

namespace SolidWorksTester.LoftedBends
{
    /// <summary>
    /// Flat-pattern view for lofted-bend shells: bend lines visible, inner face on top.
    /// </summary>
    internal static class LoftedBendsFlatPattern
    {
        public const string FlatPatternViewName = "Drawing View5";

        public static void Create(
            IModelDoc2 model,
            IDrawingDoc drawing,
            string partPath,
            Action<string> log)
        {
            log("Creating flat pattern view (inner side up, bend lines on)...");

            IView? frontView = FindViewByName(drawing, "Drawing View1");
            if (frontView == null)
            {
                log("  Warning: front view not found — skipping flat pattern.");
                return;
            }

            string activeConfigName = frontView.ReferencedConfiguration;
            log($"  Part configuration: {activeConfigName}");

            ISheet? sheet = drawing.GetCurrentSheet() as ISheet;
            double x = DrawingViewLayout.FlatPatternX;
            double y = DrawingViewLayout.FlatPatternY;
            if (sheet?.GetProperties2() is double[] props && props.Length >= 7)
            {
                // Lower-right content area (above stamp, right of ortho pack).
                x = props[5] * 0.62;
                y = props[6] * 0.28;
            }

            drawing.GenerateViewPaletteViews(partPath);

            string? paletteName = WaitForFlatPatternPaletteName(drawing, log);
            IView? flat = null;

            if (!string.IsNullOrEmpty(paletteName))
            {
                log($"  Inserting flat pattern from palette: {paletteName}");
                try
                {
                    flat = drawing.DropDrawingViewFromPalette2(paletteName, x, y, 0.0);
                }
                catch (Exception ex)
                {
                    log($"  Flat pattern palette error: {ex.Message}");
                }
            }

            if (flat == null)
            {
                log("  Fallback: CreateFlatPatternViewFromModelView3 (bend lines on, flip=inner)...");
                try
                {
                    // HideBendLines=false → show bend lines; FlipView=true → opposite/inner face up.
                    flat = drawing.CreateFlatPatternViewFromModelView3(
                        partPath,
                        activeConfigName,
                        x,
                        y,
                        0.0,
                        false,
                        true);
                }
                catch (Exception ex)
                {
                    log($"  Flat pattern creation error: {ex.Message}");
                }
            }

            if (flat == null)
            {
                log("  Warning: flat pattern view was not created.");
                return;
            }

            flat.SetName2(FlatPatternViewName);
            string flatName = flat.GetName2();
            // SolidWorks names flat-pattern configs as {Parent}_SM-FLAT-PATTERN
            string configUsed = activeConfigName + "_SM-FLAT-PATTERN";
            flat.ReferencedConfiguration = configUsed;
            flat.UseSheetScale = 1;

            model.ClearSelection2(true);
            model.Extension.SelectByID2(flatName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            bool configChanged = drawing.ChangeRefConfigurationOfFlatPatternView(partPath, configUsed);
            log($"  Flat pattern config {configUsed}: {configChanged}");

            EnsureInnerFaceUp(flat, log);
            EnsureFlatPatternUnsuppressed(flat, configUsed, log);
            model.ForceRebuild3(true);
            log($"  Flat pattern created as {flatName} (inner-up).");
        }

        private static void EnsureInnerFaceUp(IView flat, Action<string> log)
        {
            try
            {
                // Always force opposite-face orientation for lofted shells.
                if (!flat.FlipView)
                {
                    flat.FlipView = true;
                    log("  Flat pattern: FlipView=true (inner side up).");
                }
                else
                {
                    log("  Flat pattern: FlipView already true.");
                }
            }
            catch (Exception ex)
            {
                log($"  Flat pattern FlipView warning: {ex.Message}");
            }
        }

        private static string? WaitForFlatPatternPaletteName(IDrawingDoc drawing, Action<string> log)
        {
            const int attempts = 20;
            const int waitMs = 150;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    string[]? names = drawing.GetDrawingPaletteViewNames() as string[];
                    if (names != null && names.Length > 0)
                    {
                        foreach (string name in names)
                        {
                            string lower = name.ToLowerInvariant();
                            if (lower.Contains("flat pattern") ||
                                lower.Contains("развертка") ||
                                lower.Contains("развёртка"))
                                return name;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log($"  View Palette wait ({attempt}/{attempts}): {ex.Message}");
                }

                Thread.Sleep(waitMs);
            }

            log("  View Palette still empty after wait — using fallback.");
            return null;
        }

        private static void EnsureFlatPatternUnsuppressed(
            IView flatPatternView,
            string configUsed,
            Action<string> log)
        {
            try
            {
                ModelDoc2? partDoc = flatPatternView.ReferencedDocument as ModelDoc2;
                if (partDoc == null)
                    return;

                string savedConfig = partDoc.ConfigurationManager.ActiveConfiguration.Name;
                if (!partDoc.ShowConfiguration2(configUsed))
                {
                    log($"  Warning: could not activate flat-pattern config '{configUsed}'.");
                    return;
                }

                FeatureManager featMgr = partDoc.FeatureManager;
                FlatPatternFolder? flatFolder = featMgr.GetFlatPatternFolder() as FlatPatternFolder;
                if (flatFolder != null)
                {
                    object[]? flatPatterns = flatFolder.GetFlatPatterns() as object[];
                    if (flatPatterns != null)
                    {
                        foreach (object obj in flatPatterns)
                        {
                            Feature feat = (Feature)obj;
                            feat.Select2(false, 0);
                            partDoc.EditUnsuppress2();
                        }
                    }
                }
                else
                {
                    Feature? feat = partDoc.FirstFeature() as Feature;
                    while (feat != null)
                    {
                        if (feat.GetTypeName2() == "FlatPattern")
                        {
                            feat.Select2(false, 0);
                            partDoc.EditUnsuppress2();
                        }

                        feat = feat.GetNextFeature() as Feature;
                    }
                }

                partDoc.ForceRebuild3(true);
                partDoc.ShowConfiguration2(savedConfig);
                int saveErrors = 0;
                int saveWarnings = 0;
                partDoc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);
            }
            catch (Exception ex)
            {
                log($"  Flat pattern unsuppress warning: {ex.Message}");
            }
        }

        private static IView? FindViewByName(IDrawingDoc drawing, string viewName)
        {
            IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            while (view != null)
            {
                if (view.GetName2().Equals(viewName, StringComparison.OrdinalIgnoreCase))
                    return view;
                view = view.GetNextView() as IView;
            }

            return null;
        }
    }
}
