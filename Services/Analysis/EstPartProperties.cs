using System.Globalization;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>
    /// Evaluated configuration properties used on EST parts (PDM template).
    /// </summary>
    public sealed class EstPartProperties
    {
        public string? Name { get; init; }
        public string? Description { get; init; }
        public double? Dim1Mm { get; init; }
        public double? Dim2Mm { get; init; }
        public double? Dim3Mm { get; init; }
        public double? LengthMm { get; init; }
        public string? IdNumber { get; init; }
        public string? Revision { get; init; }
        public string? Material { get; init; }

        public bool HasName => !string.IsNullOrWhiteSpace(Name);
        public bool HasAnyDimension =>
            Dim1Mm.HasValue || Dim2Mm.HasValue || Dim3Mm.HasValue || LengthMm.HasValue;
    }

    internal static class EstPartPropertiesParser
    {
        public static EstPartProperties Parse(CustomPropertySnapshot snapshot)
        {
            return new EstPartProperties
            {
                Name = ResolveText(snapshot, "Name"),
                Description = ResolveText(snapshot, "Description"),
                Dim1Mm = ResolveDoubleMm(snapshot, "DIM1"),
                Dim2Mm = ResolveDoubleMm(snapshot, "DIM2"),
                Dim3Mm = ResolveDoubleMm(snapshot, "DIM3"),
                LengthMm = ResolveDoubleMm(snapshot, "Length"),
                IdNumber = ResolveText(snapshot, "IDNumber"),
                Revision = ResolveText(snapshot, "Revision"),
                Material = ResolveText(snapshot, "Material")
            };
        }

        public static bool TryMapNameToPartKind(string? name, out PartModelKind kind)
        {
            kind = default;
            if (!EstNameRegistry.TryIdentify(name, out EstNameIdentification? id))
                return false;

            kind = id.PartKind;
            return true;
        }

        public static bool TryMapNameToFlatPlateSubKind(string? name, out FlatPlateSubKind subKind)
        {
            subKind = FlatPlateSubKind.Unknown;
            if (!EstNameRegistry.TryIdentify(name, out EstNameIdentification? id))
                return false;

            if (id.FlatPlateSubKind == FlatPlateSubKind.Unknown)
                return false;

            subKind = id.FlatPlateSubKind;
            return true;
        }

        /// <summary>EST Description often carries the real family when Name is generic (e.g. PLATE + "BLIND FLANGE").</summary>
        public static bool DescriptionIndicatesFlangeOrGasket(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return false;

            string upper = description.Trim().ToUpperInvariant();
            return upper.Contains("FLANGE") ||
                   upper.Contains("GASKET") ||
                   upper.Contains("BLIND FL") ||
                   upper.Contains("SPECTACLE");
        }

        private static string? ResolveText(CustomPropertySnapshot snapshot, string key)
        {
            if (snapshot.ConfigurationProperties.TryGetValue(key, out string? config) &&
                !string.IsNullOrWhiteSpace(config))
                return config.Trim();

            if (snapshot.FileProperties.TryGetValue(key, out string? file) &&
                !string.IsNullOrWhiteSpace(file))
                return file.Trim();

            return null;
        }

        private static double? ResolveDoubleMm(CustomPropertySnapshot snapshot, string key)
        {
            string? raw = ResolveText(snapshot, key);
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            raw = raw.Trim()
                .Replace(',', '.')
                .TrimEnd('D', 'd', 'L', 'l', 'T', 't', 'X', 'x', ' ');

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
                value > 0)
                return value;

            return null;
        }

        private static bool Assign<T>(T value, out T target)
        {
            target = value;
            return true;
        }
    }
}
