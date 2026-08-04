using System;
using System.Reflection;

namespace SolidWorksTester.Services.SolidWorks
{
    /// <summary>Runtime SOLIDWORKS version context used by the capability router.</summary>
    public sealed class SolidWorksVersionContext
    {
        public static SolidWorksVersionContext Current { get; private set; } = CreateFallback();

        public int ProductYear { get; private init; }
        public string RevisionNumber { get; private init; } = "unknown";
        public string InstallDirectory { get; private init; } = string.Empty;
        public Version InteropReferenceVersion { get; private init; } = new(32, 1, 0, 0);
        public Version? InstalledInteropFileVersion { get; private init; }
        public SolidWorksInstallInfo? SelectedInstall { get; private init; }
        public SolidWorksInstallSelectionReason SelectionReason { get; private init; }

        public string Summary =>
            $"SOLIDWORKS {ProductYear} (API rev {RevisionNumber}, interop ref {InteropReferenceVersion}, " +
            $"installed interop {InstalledInteropFileVersion?.ToString() ?? "n/a"})";

        public static void InitializeFromBootstrap(
            SolidWorksInstallInfo? bestInstall,
            SolidWorksInstallSelectionReason reason = SolidWorksInstallSelectionReason.None)
        {
            Version interopRef = GetCompiledInteropVersion();

            if (bestInstall == null)
            {
                Current = new SolidWorksVersionContext
                {
                    ProductYear = 2022,
                    InteropReferenceVersion = interopRef,
                    SelectedInstall = null,
                    SelectionReason = SolidWorksInstallSelectionReason.None
                };
                return;
            }

            Current = new SolidWorksVersionContext
            {
                ProductYear = bestInstall.Year,
                InstallDirectory = bestInstall.InstallDirectory,
                InstalledInteropFileVersion = bestInstall.InteropAssemblyVersion,
                InteropReferenceVersion = interopRef,
                SelectedInstall = bestInstall,
                SelectionReason = reason
            };
        }

        /// <summary>
        /// Refines context from the live <c>RevisionNumber</c>. Product year always follows the running API;
        /// install metadata is matched to that year (not the newest discovered install).
        /// </summary>
        public static void UpdateFromRunningInstance(string revisionNumber, SolidWorksInstallInfo? installHint = null)
        {
            int productYear = ParseYearFromRevision(revisionNumber);

            SolidWorksInstallInfo? matched = SolidWorksInstallDiscovery.FindByYear(productYear);
            if (matched == null && installHint?.Year == productYear)
                matched = installHint;
            if (matched == null && Current.SelectedInstall?.Year == productYear)
                matched = Current.SelectedInstall;

            Current = new SolidWorksVersionContext
            {
                ProductYear = productYear,
                RevisionNumber = revisionNumber,
                InstallDirectory = matched?.InstallDirectory ?? string.Empty,
                InstalledInteropFileVersion = matched?.InteropAssemblyVersion,
                InteropReferenceVersion = Current.InteropReferenceVersion,
                SelectedInstall = matched,
                SelectionReason = Current.SelectionReason
            };
        }

        private static int ParseYearFromRevision(string revisionNumber)
        {
            if (string.IsNullOrWhiteSpace(revisionNumber))
                return Current.ProductYear;

            string[] parts = revisionNumber.Split('.');
            if (parts.Length > 0 && int.TryParse(parts[0], out int major))
                return SolidWorksInstallDiscovery.MapRevisionMajorToYear(major);

            return Current.ProductYear;
        }

        private static Version GetCompiledInteropVersion()
        {
            try
            {
                Assembly interop = typeof(global::SolidWorks.Interop.sldworks.ISldWorks).Assembly;
                return interop.GetName().Version ?? new Version(32, 1, 0, 0);
            }
            catch
            {
                return new Version(32, 1, 0, 0);
            }
        }

        private static SolidWorksVersionContext CreateFallback() => new()
        {
            ProductYear = 2022,
            InteropReferenceVersion = new Version(32, 1, 0, 0)
        };
    }
}
