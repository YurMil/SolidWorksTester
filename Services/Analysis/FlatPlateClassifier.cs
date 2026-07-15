using SolidWorks.Interop.sldworks;
using SolidWorksTester.Services.Analysis.FlangeGasket;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>Resolves flat-plate sub-kind from part model analysis.</summary>
    internal static class FlatPlateClassifier
    {
        public static FlatPlateSubKind Classify(
            IModelDoc2 partDoc,
            bool isRoundFlatDisc,
            bool isRoundedEndFlatProfile,
            bool hasHoles)
        {
            if (FlangeGasketModelAnalyzer.IsFlangeOrGasket(partDoc, hasHoles))
                return FlatPlateSubKind.FlangeGasket;

            if (isRoundFlatDisc)
                return FlatPlateSubKind.RoundDisc;

            if (isRoundedEndFlatProfile)
                return FlatPlateSubKind.RoundedEnd;

            return FlatPlateSubKind.Generic;
        }
    }
}
