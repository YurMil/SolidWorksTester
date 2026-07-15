using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace SolidWorksTester.Services.SolidWorks
{
    /// <summary>Detected local SOLIDWORKS installation (2022–2026).</summary>
    public sealed class SolidWorksInstallInfo
    {
        public required int Year { get; init; }
        public required string InstallDirectory { get; init; }
        public string? SldWorksExePath { get; init; }
        public Version? InteropAssemblyVersion { get; init; }
        public string DisplayName => $"SOLIDWORKS {Year}";
    }

    /// <summary>Finds SOLIDWORKS installations under Program Files and registry (2022–2026).</summary>
    public static class SolidWorksInstallDiscovery
    {
        private static readonly int[] SupportedYears = { 2026, 2025, 2024, 2023, 2022 };

        public static IReadOnlyList<SolidWorksInstallInfo> DiscoverAll()
        {
            var found = new Dictionary<int, SolidWorksInstallInfo>();

            foreach (int year in SupportedYears)
            {
                foreach (string dir in GetCandidateDirectories(year))
                {
                    if (!TryCreateInstallInfo(year, dir, out SolidWorksInstallInfo? info))
                        continue;

                    if (!found.ContainsKey(year) ||
                        (info.InteropAssemblyVersion ?? new Version(0, 0)) >
                        (found[year].InteropAssemblyVersion ?? new Version(0, 0)))
                    {
                        found[year] = info;
                    }
                }
            }

            return found.Values.OrderByDescending(i => i.Year).ToList();
        }

        public static SolidWorksInstallInfo? DiscoverBest()
        {
            IReadOnlyList<SolidWorksInstallInfo> all = DiscoverAll();
            return all.Count > 0 ? all[0] : null;
        }

        public static bool TryCreateInstallInfo(int year, string directory, out SolidWorksInstallInfo? info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return false;

            string interopPath = Path.Combine(directory, "SolidWorks.Interop.sldworks.dll");
            if (!File.Exists(interopPath))
                return false;

            string exePath = Path.Combine(directory, "SLDWORKS.exe");
            Version? interopVersion = TryGetFileVersion(interopPath);

            info = new SolidWorksInstallInfo
            {
                Year = year,
                InstallDirectory = directory.TrimEnd('\\', '/'),
                SldWorksExePath = File.Exists(exePath) ? exePath : null,
                InteropAssemblyVersion = interopVersion
            };
            return true;
        }

        private static IEnumerable<string> GetCandidateDirectories(int year)
        {
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string corpRoot = Path.Combine(programFiles, "SOLIDWORKS Corp");

            if (year == DiscoverLegacyDefaultYear())
                dirs.Add(Path.Combine(corpRoot, "SOLIDWORKS"));

            dirs.Add(Path.Combine(corpRoot, $"SOLIDWORKS {year}"));

            foreach (string? regDir in ReadRegistryInstallFolder(year))
            {
                if (!string.IsNullOrWhiteSpace(regDir))
                    dirs.Add(regDir);
            }

            return dirs.Where(Directory.Exists);
        }

        private static IEnumerable<string?> ReadRegistryInstallFolder(int year)
        {
            string[] keyPaths =
            {
                $@"SOFTWARE\SolidWorks\SOLIDWORKS {year}\Setup",
                $@"SOFTWARE\SolidWorks\SolidWorks {year}\Setup",
                $@"SOFTWARE\WOW6432Node\SolidWorks\SOLIDWORKS {year}\Setup"
            };

            foreach (string keyPath in keyPaths)
            {
                string? dir = ReadRegistryString(Registry.LocalMachine, keyPath, "SolidWorks Folder")
                    ?? ReadRegistryString(Registry.LocalMachine, keyPath, "SWInstallDir");
                if (!string.IsNullOrWhiteSpace(dir))
                    yield return dir;
            }
        }

        private static string? ReadRegistryString(RegistryKey root, string subKey, string valueName)
        {
            try
            {
                using RegistryKey? key = root.OpenSubKey(subKey);
                return key?.GetValue(valueName) as string;
            }
            catch
            {
                return null;
            }
        }

        private static int DiscoverLegacyDefaultYear() => 2025;

        private static Version? TryGetFileVersion(string path)
        {
            try
            {
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                if (Version.TryParse(info.FileVersion, out Version? version))
                    return version;
            }
            catch
            {
                // ignore
            }

            return null;
        }

        /// <summary>Maps API revision major (from <see cref="ISldWorks.RevisionNumber"/>) to product year.</summary>
        public static int MapRevisionMajorToYear(int revisionMajor) => revisionMajor switch
        {
            >= 34 => 2026,
            33 => 2025,
            32 => 2024,
            31 => 2023,
            30 => 2022,
            _ => Math.Clamp(2022 + (revisionMajor - 30), 2022, 2026)
        };
    }
}
