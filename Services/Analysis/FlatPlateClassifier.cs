using SolidWorksTester.Services.Analysis.BafflePlate;
using SolidWorksTester.Services.Analysis.FlangeGasket;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>Resolves flat-plate sub-kind from a geometry snapshot (no extra COM).</summary>
    internal static class FlatPlateClassifier
    {
        public static FlatPlateSubKind Classify(
            PartGeometrySnapshot snap,
            bool isRoundFlatDisc,
            bool isRoundedEndFlatProfile)
        {
            // Dense hole arrays first — otherwise flange bolt-circle heuristics misfire.
            if (BafflePlateModelAnalyzer.IsBafflePlate(snap))
                return FlatPlateSubKind.BafflePlate;

            if (FlangeGasketModelAnalyzer.IsFlangeOrGasket(snap))
                return FlatPlateSubKind.FlangeGasket;

            if (isRoundFlatDisc)
                return FlatPlateSubKind.RoundDisc;

            if (isRoundedEndFlatProfile)
                return FlatPlateSubKind.RoundedEnd;

            return FlatPlateSubKind.Generic;
        }
    }
}
