using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Services.Drawing.Routing
{
    /// <summary>Executes a resolved <see cref="DrawingRouteDecision"/> via existing pipelines.</summary>
    internal static class DrawingPipelineExecutor
    {
        public static void Execute(
            ISldWorks swApp,
            IModelDoc2 drawingModel,
            string partPath,
            PartAnalysisResult analysis,
            DrawingRouteDecision route,
            Action<string> log)
        {
            switch (route.PipelineId)
            {
                case DrawingPipelineId.FlatPlate:
                    FlatPlateDrawingPipeline.Process(swApp, drawingModel, partPath, analysis, route, log);
                    break;

                case DrawingPipelineId.BentSheetMetal:
                    BentSheetMetalDrawingPipeline.Process(swApp, drawingModel, partPath, analysis, route, log);
                    break;

                case DrawingPipelineId.Cylindrical:
                    CylindricalDrawingPipeline.Process(swApp, drawingModel, partPath, analysis, route, log);
                    break;

                case DrawingPipelineId.LoftedBends:
                    LoftedBends.LoftedBendsDrawingPipeline.Process(
                        swApp, drawingModel, partPath, analysis, route, log);
                    break;

                case DrawingPipelineId.ImportedGeometry:
                    ImportedGeometryDrawingPipeline.Process(swApp, drawingModel, partPath, analysis, route, log);
                    break;

                case DrawingPipelineId.GenericFallback:
                    GenericFallbackDrawingPipeline.Process(swApp, drawingModel, partPath, analysis, route, log);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported drawing pipeline: {route.PipelineId}");
            }
        }
    }
}
