namespace SolidWorksTester.Services.Analysis
{
    /// <summary>Sub-classification for flat-plate pipeline (P-01 nested router).</summary>
    public enum FlatPlateSubKind
    {
        Unknown = 0,
        Generic,
        RoundDisc,
        RoundedEnd,
        FlangeGasket,
        /// <summary>Sheet-metal baffle / tube-sheet style plate with a dense hole array.</summary>
        BafflePlate
    }
}
