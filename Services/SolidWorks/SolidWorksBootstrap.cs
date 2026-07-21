using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SolidWorksTester.Services.SolidWorks
{
    /// <summary>
    /// Startup bootstrap: discovers installed SOLIDWORKS (2022–2026), validates interop availability,
    /// and initializes <see cref="SolidWorksVersionContext"/>.
    /// </summary>
    public static class SolidWorksBootstrap
    {
        private static bool _initialized;
        private static readonly object Gate = new();
        private static IReadOnlyList<SolidWorksInstallInfo> _installs = Array.Empty<SolidWorksInstallInfo>();

        public static IReadOnlyList<SolidWorksInstallInfo> InstalledVersions => _installs;

        public static void Initialize()
        {
            lock (Gate)
            {
                if (_initialized)
                    return;

                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                _installs = SolidWorksInstallDiscovery.DiscoverAll();
                SolidWorksInstallInfo? best = SolidWorksInstallDiscovery.DiscoverBest(out SolidWorksInstallSelectionReason reason);
                SolidWorksVersionContext.InitializeFromBootstrap(best, reason);
                _initialized = true;
            }
        }

        public static bool TryValidate(out string userMessage, out string logDetails)
        {
            Initialize();

            var sb = new StringBuilder();
            sb.AppendLine("SOLIDWORKS version router:");
            sb.AppendLine($"  Compiled interop reference: {SolidWorksVersionContext.Current.InteropReferenceVersion}");
            sb.AppendLine($"  Installed SOLIDWORKS found: {_installs.Count}");

            foreach (SolidWorksInstallInfo install in _installs)
            {
                sb.AppendLine($"    - {install.DisplayName}: {install.InstallDirectory} " +
                              $"(interop file {install.InteropAssemblyVersion?.ToString() ?? "?"})");
            }

            if (_installs.Count == 0)
            {
                userMessage =
                    "No SOLIDWORKS 2022–2026 installation was detected on this PC.\n\n" +
                    "Install SOLIDWORKS or run on a machine with SOLIDWORKS registered in Windows.";
                logDetails = sb.ToString();
                return false;
            }

            SolidWorksInstallInfo? selected = SolidWorksVersionContext.Current.SelectedInstall ?? _installs[0];
            string reason = SolidWorksInstallDiscovery.FormatSelectionReason(
                SolidWorksVersionContext.Current.SelectionReason);

            sb.AppendLine($"  Selected for routing: {selected.DisplayName}");
            sb.AppendLine($"  Selection reason: {reason}");
            if (!string.IsNullOrWhiteSpace(selected.InstallDirectory))
                sb.AppendLine($"  Selected install path: {selected.InstallDirectory}");

            string? comDefault = SolidWorksInstallDiscovery.TryGetComDefaultInstallDirectory();
            if (!string.IsNullOrWhiteSpace(comDefault))
                sb.AppendLine($"  Windows COM default path: {comDefault}");

            sb.AppendLine($"  Strategy: {SolidWorksCapabilityRouter.GetStrategyNotes()}");

            userMessage = string.Empty;
            logDetails = sb.ToString();
            return true;
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                string? simpleName = new AssemblyName(args.Name).Name;
                if (simpleName is not ("SolidWorks.Interop.sldworks" or "SolidWorks.Interop.swconst"))
                    return null;

                SolidWorksInstallInfo? install =
                    SolidWorksVersionContext.Current.SelectedInstall
                    ?? _installs.FirstOrDefault(i => i.Year == SolidWorksVersionContext.Current.ProductYear)
                    ?? _installs.FirstOrDefault();

                if (install == null)
                    return null;

                string path = Path.Combine(install.InstallDirectory, simpleName + ".dll");
                if (!File.Exists(path))
                    return null;

                return Assembly.LoadFrom(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
