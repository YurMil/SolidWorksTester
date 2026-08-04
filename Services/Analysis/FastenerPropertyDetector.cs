using System;

namespace SolidWorksTester.Services.Analysis
{
    /// <summary>
    /// Detects purchased fasteners that must not get auto-drawings
    /// (<c>DocumentType=Fastener</c> or <c>IsFastener=1</c>).
    /// </summary>
    internal static class FastenerPropertyDetector
    {
        public static readonly string[] DocumentTypeKeys =
        {
            "DocumentType",
            "CADAS_DocumentType"
        };

        public static readonly string[] IsFastenerKeys =
        {
            "IsFastener",
            "CADAS_IsFastener"
        };

        public static bool TryDetect(CustomPropertySnapshot snapshot, out string reason)
        {
            reason = string.Empty;

            string? documentType = CustomPropertyReader.ResolveFirst(snapshot, DocumentTypeKeys);
            if (!string.IsNullOrWhiteSpace(documentType) &&
                documentType.Equals("Fastener", StringComparison.OrdinalIgnoreCase))
            {
                reason = "DocumentType=Fastener";
                return true;
            }

            string? isFastener = CustomPropertyReader.ResolveFirst(snapshot, IsFastenerKeys);
            if (IsTruthyFlag(isFastener))
            {
                reason = $"IsFastener={isFastener}";
                return true;
            }

            return false;
        }

        private static bool IsTruthyFlag(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string v = raw.Trim();
            return v is "1" or "true" or "True" or "TRUE" or "yes" or "Yes" or "YES";
        }
    }
}
