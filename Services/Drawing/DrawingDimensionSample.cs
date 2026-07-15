namespace SolidWorksTester.Services.Drawing
{
    public sealed class DrawingDimensionSample
    {
        public int Type { get; init; }
        public double ValueMm { get; init; }
        public string Prefix { get; init; } = string.Empty;
        public string ViewName { get; init; } = string.Empty;
    }
}
