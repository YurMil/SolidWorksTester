using System.Collections.Generic;

namespace SolidWorksTester.Services.Analysis
{
    public sealed class EstDrawingQualityReport
    {
        public List<string> Flags { get; } = [];
        public List<string> Notes { get; } = [];
        public bool HasExpectedDimensions { get; init; }
        public bool IsPass => Flags.Count == 0;
    }
}
