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

        public string Summary =>
            $"SOLIDWORKS {ProductYear} (API rev {RevisionNumber}, interop ref {InteropReferenceVersion}, " +
            $"installed interop {InstalledInteropFileVersion?.ToString() ?? "n/a"})";

        public static void InitializeFromBootstrap(SolidWorksInstallInfo? bestInstall)
        {
            Version interopRef = GetCompiledInteropVersion();

            if (bestInstall == null)
            {
                Current = new SolidWorksVersionContext
                {
                    ProductYear = 2022,
                    InteropReferenceVersion = interopRef,
                    SelectedInstall = null
                };
                return;
            }

            Current = new SolidWorksVersionContext
            {
                ProductYear = bestInstall.Year,
                InstallDirectory = bestInstall.InstallDirectory,
                InstalledInteropFileVersion = bestInstall.InteropAssemblyVersion,
                InteropReferenceVersion = interopRef,
                SelectedInstall = bestInstall
            };
        }

        public static void UpdateFromRunningInstance(string revisionNumber, SolidWorksInstallInfo? installHint = null)
        {
            int yearFromRevision = ParseYearFromRevision(revisionNumber);
            int yearFromInstall = installHint?.Year ?? Current.ProductYear;
            int productYear = Math.Max(yearFromRevision, yearFromInstall);

            Current = new SolidWorksVersionContext
            {
                ProductYear = productYear,
                RevisionNumber = revisionNumber,
                InstallDirectory = installHint?.InstallDirectory ?? Current.InstallDirectory,
                InstalledInteropFileVersion = installHint?.InteropAssemblyVersion ?? Current.InstalledInteropFileVersion,
                InteropReferenceVersion = Current.InteropReferenceVersion,
                SelectedInstall = installHint ?? Current.SelectedInstall
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
