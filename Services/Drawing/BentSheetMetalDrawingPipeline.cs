using System;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Drawing
{
    /// <summary>
    /// Drawing pipeline for bent sheet metal parts.
    /// Creates three standard views plus a flat pattern view.
    /// </summary>
    internal static class BentSheetMetalDrawingPipeline
    {
        private const int PaletteWaitAttempts = 20;
        private const int PaletteWaitMs = 150;

        public static void Process(
            ISldWorks swApp,
            IModelDoc2 drawingModel,
            string partPath,
            Action<string> log)
        {
            var drawing = (IDrawingDoc)drawingModel;

            log("Using bent sheet metal drawing pipeline (3 views + flat pattern).");

            DrawingPipelineShared.DeleteExistingViews(drawingModel, drawing, log);
            DrawingPipelineShared.CreateStandardThreeViews(drawingModel, drawing, partPath, log);
            CreateFlatPatternView(drawingModel, drawing, partPath, log);
            DrawingViewDisplayHelper.ApplyHiddenLinesVisibleToAllModelViews(drawingModel, drawing, log);
            ApplyDimensions(swApp, drawingModel, drawing, log);
            log("Checking for duplicate dimensions...");
            DrawingDimensionDeduper.RemoveDuplicateDimensions(drawingModel, drawing, log);
            DrawingPipelineShared.AutoArrangeDimensions(drawingModel, drawing);
            DrawingPipelineShared.AdjustSheetScaleIfNeeded(drawingModel, drawing, log);
            drawingModel.ForceRebuild3(true);
        }

        private static void CreateFlatPatternView(
            IModelDoc2 model,
            IDrawingDoc drawing,
            string partPath,
            Action<string> log)
        {
            log("Creating flat pattern view...");
            IModelDocExtension modelDocExt = model.Extension;

            IView? frontView = FindViewByName(drawing, "Drawing View1");
            if (frontView == null)
            {
                // Rename may have failed — fall back to the first model view.
                frontView = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
            }

            if (frontView == null)
            {
                log("  Warning: front view not found — skipping flat pattern.");
                return;
            }

            string activeConfigName = frontView.ReferencedConfiguration;
            log($"  Part configuration: {activeConfigName}");
            log("  Generating View Palette...");

            drawing.GenerateViewPaletteViews(partPath);

            string[]? paletteViewNames = WaitForPaletteViewNames(drawing, log);

            string? flatPatternPaletteName = null;
            if (paletteViewNames != null)
            {
                foreach (string name in paletteViewNames)
                {
                    string lowerName = name.ToLowerInvariant();
                    if (lowerName.Contains("flat pattern") || lowerName.Contains("развертка") || lowerName.Contains("развёртка"))
                    {
                        flatPatternPaletteName = name;
                        break;
                    }
                }
            }

            IView? flatPatternView = null;
            // SolidWorks names flat-pattern configs as {Parent}_SM-FLAT-PATTERN
            string configUsed = activeConfigName + "_SM-FLAT-PATTERN";

            if (!string.IsNullOrEmpty(flatPatternPaletteName))
            {
                log($"  Inserting flat pattern from palette: {flatPatternPaletteName}");
                try
                {
                    flatPatternView = drawing.DropDrawingViewFromPalette2(
                        flatPatternPaletteName,
                        DrawingViewLayout.FlatPatternX,
                        DrawingViewLayout.FlatPatternY,
                        0.0);
                }
                catch (Exception ex)
                {
                    log($"  Flat pattern insert error: {ex.Message}");
                }
            }

            if (flatPatternView == null)
            {
                log("  Fallback: CreateFlatPatternViewFromModelView3...");
                try
                {
                    flatPatternView = drawing.CreateFlatPatternViewFromModelView3(
                        partPath,
                        activeConfigName,
                        DrawingViewLayout.FlatPatternX,
                        DrawingViewLayout.FlatPatternY,
                        0.0,
                        false,
                        false);
                }
                catch (Exception ex)
                {
                    log($"  Flat pattern creation error: {ex.Message}");
                }
            }

            if (flatPatternView == null)
            {
                log("  Warning: flat pattern view was not created.");
                return;
            }

            flatPatternView.SetName2("Drawing View4");
            string flatName = flatPatternView.GetName2();
            flatPatternView.ReferencedConfiguration = configUsed;
            flatPatternView.UseSheetScale = 1;

            model.ClearSelection2(true);
            modelDocExt.SelectByID2(flatName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            bool configChanged = drawing.ChangeRefConfigurationOfFlatPatternView(partPath, configUsed);
            log($"  Flat pattern created. Configuration {configUsed}: {configChanged}");

            EnsureFlatPatternUnsuppressed(flatPatternView, configUsed, log);
            model.ForceRebuild3(true);
        }

        private static string[]? WaitForPaletteViewNames(IDrawingDoc drawing, Action<string> log)
        {
            for (int attempt = 1; attempt <= PaletteWaitAttempts; attempt++)
            {
                try
                {
                    string[]? names = drawing.GetDrawingPaletteViewNames() as string[];
                    if (names != null && names.Length > 0)
                        return names;
                }
                catch (Exception ex)
                {
                    log($"  View Palette wait ({attempt}/{PaletteWaitAttempts}): {ex.Message}");
                }

                Thread.Sleep(PaletteWaitMs);
            }

            log("  View Palette still empty after wait — using fallback.");
            return null;
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

        private static void EnsureFlatPatternUnsuppressed(IView flatPatternView, string configUsed, Action<string> log)
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
                log($"  Flat pattern warning: {ex.Message}");
            }
        }

        private static void ApplyDimensions(
            ISldWorks swApp,
            IModelDoc2 model,
            IDrawingDoc drawing,
            Action<string> log)
        {
            log("Adding dimensions (standard + flat pattern)...");
            IModelDocExtension modelDocExt = model.Extension;
            SmartDimHelper dimHelper = new SmartDimHelper(swApp, model, drawing, modelDocExt);
            dimHelper.SuppressDimInput();

            try
            {
                IView? dimView = (drawing.GetFirstView() as IView)?.GetNextView() as IView;

                while (dimView != null)
                {
                    string vName = dimView.GetName2();
                    bool isFlat = dimView.IsFlatPatternView();
                    log($"  Dimensions: {vName} (flat pattern={isFlat})");

                    SmartDimOverall.Add(dimHelper, dimView);

                    if (!isFlat)
                    {
                        SmartDimThickness.Add(dimHelper, dimView);
                        SmartDimHolePositions.Add(dimHelper, dimView);
                        SmartDimCutouts.Add(dimHelper, dimView);
                        SmartDimBends.Add(dimHelper, dimView);
                    }
                    else
                    {
                        SmartDimHoles.Add(dimHelper, dimView);
                        SmartDimFlatBendLines.Add(dimHelper, dimView);
                    }

                    dimView = dimView.GetNextView() as IView;
                }
            }
            finally
            {
                dimHelper.RestoreDimInput();
            }
        }
    }
}
