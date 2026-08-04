using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.Services.Drawing.Routing;

namespace SolidWorksTester.Services
{
    public enum ProcessPartOutcome
    {
        Completed = 0,
        SkippedFastener = 1
    }

    public sealed class SheetMetalDrawingService
    {
        public ProcessPartOutcome ProcessPart(ISldWorks swApp, string partPath, string templatePath, Action<string> log)
        {
            if (!File.Exists(partPath))
                throw new FileNotFoundException("Part file not found.", partPath);

            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Template file not found.", templatePath);

            string extension = Path.GetExtension(partPath);
            if (!extension.Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Expected a part file (.SLDPRT).", nameof(partPath));

            string drawingPath = Path.ChangeExtension(partPath, ".SLDDRW");
            string partDirectory = Path.GetDirectoryName(partPath)
                ?? throw new InvalidOperationException("Could not determine the part folder.");

            log($"Part: {partPath}");
            log($"Drawing: {drawingPath}");

            using var partTimer = new PipelineStopwatch(log, "ProcessPart");

            PartAnalysisResult analysis = partTimer.Measure("part analysis", () =>
                PartModelAnalyzer.Analyze(swApp, partPath, log));

            if (analysis.IsFastener)
            {
                string reason = analysis.FastenerSkipReason ?? "DocumentType=Fastener";
                log($"SKIP: fastener part ({reason}) — drawing not created.");
                partTimer.Measure("close part (fastener skip)", () =>
                    SolidWorksConnection.SafeCloseDocumentByPath(swApp, partPath, log));
                return ProcessPartOutcome.SkippedFastener;
            }

            string templateExt = Path.GetExtension(templatePath);
            if (templateExt.Equals(".DRWDOT", StringComparison.OrdinalIgnoreCase))
            {
                log("Creating drawing from .DRWDOT template...");
                IModelDoc2? model = partTimer.Measure("new drawing from DRWDOT", () =>
                    swApp.NewDocument(templatePath, 0, 0, 0) as IModelDoc2);
                if (model == null)
                    throw new InvalidOperationException("Failed to create drawing from .DRWDOT template.");

                try
                {
                    partTimer.Measure("process drawing model", () =>
                        ProcessDrawingModel(swApp, model, partPath, analysis, log));
                    partTimer.Measure("save drawing", () =>
                        SaveDrawingToPath(model, drawingPath, log));
                }
                finally
                {
                    partTimer.Measure("close drawing", () =>
                        SolidWorksConnection.SafeCloseDocument(swApp, model, log));
                    // Part stays open through view creation; close after the drawing is finished.
                    SolidWorksConnection.SafeCloseDocumentByPath(swApp, partPath, log);
                }

                return ProcessPartOutcome.Completed;
            }

            if (templateExt.Equals(".SLDDRW", StringComparison.OrdinalIgnoreCase))
            {
                log("Copying .SLDDRW template...");
                Directory.CreateDirectory(partDirectory);
                File.Copy(templatePath, drawingPath, overwrite: true);
                log("Template copied.");
            }
            else
            {
                throw new NotSupportedException("Supported templates: .DRWDOT and .SLDDRW.");
            }

            partTimer.Measure("open and process drawing", () =>
                OpenAndProcessDrawing(swApp, partPath, drawingPath, analysis, log));
            return ProcessPartOutcome.Completed;
        }

        private static void OpenAndProcessDrawing(
            ISldWorks swApp,
            string partPath,
            string drawingPath,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            int errors = 0;
            int warnings = 0;

            IModelDoc2? model = swApp.OpenDoc6(
                drawingPath,
                (int)swDocumentTypes_e.swDocDRAWING,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings) as IModelDoc2;

            if (model == null)
                throw new InvalidOperationException($"Failed to open drawing. Error code: {errors}.");

            try
            {
                log("Drawing opened.");
                ProcessDrawingModel(swApp, model, partPath, analysis, log);
                SaveDrawingToPath(model, drawingPath, log);
            }
            finally
            {
                SolidWorksConnection.SafeCloseDocumentByPath(swApp, drawingPath, log);
                // Part stays open through view creation; close after the drawing is finished.
                SolidWorksConnection.SafeCloseDocumentByPath(swApp, partPath, log);
            }
        }

        private static void ProcessDrawingModel(
            ISldWorks swApp,
            IModelDoc2 model,
            string partPath,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            model.Extension.SetUserPreferenceInteger(
                (int)swUserPreferenceIntegerValue_e.swUnitSystem,
                0,
                (int)swUnitSystem_e.swUnitSystem_MMGS);

            DrawingRouteDecision route = DrawingPipelineRouter.Resolve(analysis, log);
            DrawingPipelineExecutor.Execute(swApp, model, partPath, analysis, route, log);
        }

        private static void SaveDrawingToPath(IModelDoc2 model, string drawingPath, Action<string> log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(drawingPath)!);

            int saveErrors = 0;
            int saveWarnings = 0;
            string currentPath = model.GetPathName();

            bool saved;
            if (string.IsNullOrEmpty(currentPath) ||
                !currentPath.Equals(drawingPath, StringComparison.OrdinalIgnoreCase))
            {
                saved = model.Extension.SaveAs(
                    drawingPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null,
                    ref saveErrors,
                    ref saveWarnings);
            }
            else
            {
                saved = model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref saveErrors, ref saveWarnings);
            }

            if (!saved)
                throw new InvalidOperationException($"Failed to save drawing. Errors: {saveErrors}, warnings: {saveWarnings}.");

            log("Drawing saved.");
        }
    }
}
