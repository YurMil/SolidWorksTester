using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.BafflePlate;
using SolidWorksTester.Cylindrical;
using SolidWorksTester.Services.Analysis;
using SolidWorksTester.Services.Drawing;
using SolidWorksTester.SmartDim;

namespace SolidWorksTester.LoftedBends
{
    /// <summary>
    /// Lofted-bend shell dimensions matched to EST reference drawings:
    /// side (View1) — height, OD (+ prefix), id (+ prefix), axis centerline;
    /// end (View2) — wall thickness, weld-gap centerline + linear gap at outer tips;
    /// flat pattern — length + width + bend notes.
    /// </summary>
    internal static partial class LoftedBendsDimensions
    {
        private const double DimOffset = 0.014;
        private const double MinSize = 0.0004;

        private static readonly Regex DescShellRegex = new(
            @"(\d+(?:[.,]\d+)?)\s*[x×]\s*(\d+(?:[.,]\d+)?)\s*[x×]\s*(\d+(?:[.,]\d+)?).*?(?:oD|OD|Ø)\s*(\d+(?:[.,]\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static void Apply(
            ISldWorks swApp,
            IModelDoc2 model,
            IDrawingDoc drawing,
            PartAnalysisResult analysis,
            Action<string> log)
        {
            log("Adding Lofted Bends dimensions...");
            SmartDimHelper h = new SmartDimHelper(swApp, model, drawing, model.Extension);
            h.SuppressDimInput();

            try
            {
                ShellSizeHints hints = ResolveHints(h, analysis, log);
                string? endViewName = PickPrimaryEndViewName(h, drawing, log);

                IView? view = (drawing.GetFirstView() as IView)?.GetNextView() as IView;
                while (view != null)
                {
                    string name = view.GetName2();
                    bool isFlat = view.IsFlatPatternView();
                    bool isIso = name.Equals(
                        SmartDimConstants.IsometricViewName, StringComparison.OrdinalIgnoreCase);
                    bool isEnd = !isFlat && !isIso &&
                        endViewName != null &&
                        name.Equals(endViewName, StringComparison.OrdinalIgnoreCase);

                    log($"  Dimensions: {name} (flat={isFlat}, iso={isIso}, end={isEnd})");

                    if (isIso)
                    {
                        // Iso: no dimensions / no degree marks.
                    }
                    else if (isFlat)
                    {
                        EnsureFlatPatternLengthAndWidth(h, view, hints, log);
                        TryEnableBendNotes(view, log);
                        SmartDimHoles.Add(h, view, log);
                        SmartDimCutouts.Add(h, view, log);
                        SmartDimFlatBendLines.Add(h, view, log);
                    }
                    else if (isEnd)
                    {
                        // End face (typically Drawing View2): thickness + gap centerline/dim.
                        CylindricalDimCenterlines.Add(h, model, drawing, view, log);
                        EnsureWallThicknessOnEndView(h, view, hints.ThicknessMeters, log);
                        TryWeldGapOnEndView(h, drawing, view, log);
                    }
                    else
                    {
                        // Main side view only (Drawing View1): height + OD + ID + axis.
                        if (name.Equals("Drawing View1", StringComparison.OrdinalIgnoreCase))
                        {
                            TrySideViewAxisCenterline(h, model, drawing, view, log);
                            EnsureHeightOnSideView(h, view, hints.HeightMeters, log);
                            EnsureOuterDiameterOnSideView(h, view, hints, log);
                            EnsureInnerDiameterOnSideView(h, view, hints, log);
                        }
                    }

                    view = view.GetNextView() as IView;
                }
            }
            finally
            {
                h.ClearViewCaches();
                h.RestoreDimInput();
            }
        }
    }
}
