using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Services.Drawing.FlatPlate
{    /// <summary>Runtime context for flat-plate dimension routing.</summary>
    internal sealed class FlatPlateDimContext
    {
        public FlatPlateSubKind SubKind { get; init; }
        public IView? PrimaryFlatView { get; init; }
        public IView? DiscFaceView { get; init; }
        public bool ModelImportUsed { get; set; }

        /// <summary>When true, outer pipeline skips another full-drawing dedupe pass.</summary>
        public bool SkipPostPipelineDedupe { get; set; }

        /// <summary>When true, skip AlignDimensions (imported dims already placed).</summary>
        public bool SkipAutoArrange { get; set; }

        /// <summary>EST Dim1 / gauge hint in mm when available.</summary>
        public double? ExpectedThicknessMm { get; set; }

        /// <summary>
        /// Kept dims from an in-pipeline dedupe. When set, EST validate uses this
        /// instead of another annotation Collect pass.
        /// </summary>
        public IReadOnlyList<DrawingDimensionSample>? DimensionSamples { get; set; }

        public bool UsesDiscStyleThickness =>
            SubKind is FlatPlateSubKind.RoundDisc
                or FlatPlateSubKind.RoundedEnd
                or FlatPlateSubKind.FlangeGasket
                or FlatPlateSubKind.BafflePlate
                or FlatPlateSubKind.ArcSector;

        public bool SkipsModelImport =>
            SubKind is FlatPlateSubKind.RoundDisc
                or FlatPlateSubKind.ArcSector;
    }
}
