using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis;

namespace SolidWorksTester.Services.Drawing.FlatPlate
{
    /// <summary>Runtime context for flat-plate dimension routing.</summary>
    internal sealed class FlatPlateDimContext
    {
        public FlatPlateSubKind SubKind { get; init; }
        public IView? PrimaryFlatView { get; init; }
        public IView? DiscFaceView { get; init; }
        public bool ModelImportUsed { get; set; }

        public bool UsesDiscStyleThickness =>
            SubKind is FlatPlateSubKind.RoundDisc
                or FlatPlateSubKind.RoundedEnd
                or FlatPlateSubKind.FlangeGasket;

        public bool SkipsModelImport =>
            SubKind is FlatPlateSubKind.RoundDisc;
    }
}
