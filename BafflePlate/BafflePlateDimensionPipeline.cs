using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.BafflePlate
{
    /// <summary>
    /// Dimension pipeline for sheet-metal baffle / tube-sheet plates with dense hole arrays.
    /// Import marked dims to the primary face view only (avoids duplicates on side views).
    /// </summary>
    internal static class BafflePlateDimensionPipeline
    {
        /// <returns>
        /// Kept-dimension snapshot from the last dedupe (for EST validate without Collect).
        /// Empty when nothing was imported / deduped.
        /// </returns>
        public static IReadOnlyList<DrawingDimensionSample> Apply(
            IModelDoc2 model,
            IDrawingDoc drawing,
            SmartDimHelper dimHelper,
            IView? primaryFlatView,
            PartAnalysisResult? analysis,
            Action<string> log)
        {
            using var timer = new PipelineStopwatch(log, "baffle dimensions");

            log("  Baffle plate mode: import outer/tab dims; skip mass hole callouts.");

            double? smThickness = BafflePlateThickness.TryReadSheetMetalThickness(dimHelper);

            IReadOnlyList<DrawingDimensionSample> samples = Array.Empty<DrawingDimensionSample>();
            int imported = 0;
            timer.Measure("Step 1: import marked dims (primary view)", () =>
            {
                if (primaryFlatView != null)
                {
                    log($"  Step 1: import model dimensions into {primaryFlatView.GetName2()} only...");
                    imported = DrawingModelDimensionImport.ImportOnce(
                        model, drawing, primaryFlatView, log);
                }
                else
                {
                    log("  Step 1: no primary view — import marked dims to all views...");
                    imported = DrawingModelDimensionImport.ImportMarkedDimensionsToAllViews(
                        model, drawing, log);
                }
            });

            // Dedupe only when import likely created cross-view / duplicate noise.
            if (imported > 0)
            {
                timer.Measure("Step 2: dedupe after import", () =>
                {
                    log("  Step 2: dedupe after model import...");
                    if (primaryFlatView != null)
                    {
                        // Side / iso views are empty after primary-only import — skip their COM walks.
                        samples = DrawingDimensionDeduper.RemoveDuplicateDimensionsOnlyIn(
                            model, drawing, log, smThickness, primaryFlatView.GetName2());
                    }
                    else
                    {
                        samples = DrawingDimensionDeduper.RemoveDuplicateDimensions(
                            model, drawing, log, smThickness, SmartDimConstants.IsometricViewName);
                    }
                });
            }
            else
            {
                log("  Step 2: skip dedupe (nothing imported).");
            }

            log("  Step 3: skip per-view SmartDimThickness (use sheet-metal gabarit only).");

            bool thicknessPlaced = false;
            IView? thicknessView = null;
            timer.Measure("Step 4: gabarit thickness (fast outline picks)", () =>
            {
                log("  Step 4: ensure gabarit thickness on side/profile view...");
                thicknessPlaced = BafflePlateThickness.TryAddOnce(
                    dimHelper, drawing, primaryFlatView, analysis, log, out thicknessView);
            });

            if (thicknessPlaced && thicknessView != null)
            {
                timer.Measure("Step 5: append side thickness sample", () =>
                {
                    log("  Step 5: sample side view only (skip re-walk of primary)...");
                    IReadOnlyList<DrawingDimensionSample> sideSamples =
                        DrawingDimensionDeduper.RemoveDuplicateDimensionsOnlyIn(
                            model, drawing, log, smThickness, thicknessView.GetName2());

                    if (samples.Count == 0)
                    {
                        samples = sideSamples;
                    }
                    else if (sideSamples.Count > 0)
                    {
                        var merged = new List<DrawingDimensionSample>(samples.Count + sideSamples.Count);
                        merged.AddRange(samples);
                        merged.AddRange(sideSamples);
                        samples = merged;
                    }
                });
            }
            else
            {
                log("  Step 5: skip final dedupe (no dims added after last dedupe).");
            }

            // Hole table deferred: dense arrays flood tags and do not fit any sheet size.
            // Keep BafflePlateHoleTable as a dormant alternative — do not call it here.
            timer.Measure("Step 6: Detail A + Section B-B", () =>
            {
                log("  Step 6: Detail A (1:1) + Section B-B (2:1) — hole table skipped...");
                BafflePlateDetailSection.TryCreate(
                    dimHelper, model, drawing, primaryFlatView, analysis, log);
            });

            return samples;
        }
    }
}
