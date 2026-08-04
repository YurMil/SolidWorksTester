using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

    /// <summary>Why <see cref="SolidWorksInstallDiscovery.DiscoverBest"/> picked an install.</summary>
    public enum SolidWorksInstallSelectionReason
    {
        None,
        WindowsComDefault,
        NewestInstalled
    }

    /// <summary>Finds SOLIDWORKS installations under Program Files and registry (2022–2026).</summary>
    public static class SolidWorksInstallDiscovery
    {
        private static readonly int[] SupportedYears = { 2026, 2025, 2024, 2023, 2022 };
        private static readonly Guid SldWorksClsid = new("96749377-3391-11D2-9EE3-00C04F797396");

        public static IReadOnlyList<SolidWorksInstallInfo> DiscoverAll()
        {
            var found = new Dictionary<int, SolidWorksInstallInfo>();

            foreach (string dir in EnumerateCandidateDirectories())
            {
                if (!TryCreateInstallInfoFromDirectory(dir, out SolidWorksInstallInfo? info) || info == null)
                    continue;

                if (!found.ContainsKey(info.Year) ||
                    (info.InteropAssemblyVersion ?? new Version(0, 0)) >
                    (found[info.Year].InteropAssemblyVersion ?? new Version(0, 0)))
                {
                    found[info.Year] = info;
                }
            }

            return found.Values.OrderByDescending(i => i.Year).ToList();
        }

        /// <summary>
        /// Prefers the Windows COM-registered default (<c>SldWorks.Application</c>), then newest install.
        /// </summary>
        public static SolidWorksInstallInfo? DiscoverBest() =>
            DiscoverBest(out _);

        public static SolidWorksInstallInfo? DiscoverBest(out SolidWorksInstallSelectionReason reason)
        {
            IReadOnlyList<SolidWorksInstallInfo> all = DiscoverAll();
            if (all.Count == 0)
            {
                reason = SolidWorksInstallSelectionReason.None;
                return null;
            }

            string? comDir = TryGetComDefaultInstallDirectory();
            if (!string.IsNullOrWhiteSpace(comDir))
            {
                SolidWorksInstallInfo? comMatch = FindByInstallDirectory(all, comDir);
                if (comMatch != null)
                {
                    reason = SolidWorksInstallSelectionReason.WindowsComDefault;
                    return comMatch;
                }
            }

            reason = SolidWorksInstallSelectionReason.NewestInstalled;
            return all[0];
        }

        public static SolidWorksInstallInfo? FindByYear(int year) =>
            DiscoverAll().FirstOrDefault(i => i.Year == year);

        public static SolidWorksInstallInfo? FindByInstallDirectory(
            IEnumerable<SolidWorksInstallInfo> installs,
            string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return null;

            string normalized = NormalizePath(directory);
            return installs.FirstOrDefault(i =>
                string.Equals(NormalizePath(i.InstallDirectory), normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryCreateInstallInfo(int year, string directory, out SolidWorksInstallInfo? info)
        {
            if (!TryCreateInstallInfoFromDirectory(directory, out info) || info == null)
                return false;

            // Keep explicit year when caller knows it (registry year keys), but prefer interop-inferred year if it differs.
            if (info.Year != year && year is >= 2022 and <= 2026)
            {
                info = new SolidWorksInstallInfo
                {
                    Year = year,
                    InstallDirectory = info.InstallDirectory,
                    SldWorksExePath = info.SldWorksExePath,
                    InteropAssemblyVersion = info.InteropAssemblyVersion
                };
            }

            return true;
        }

        public static bool TryCreateInstallInfoFromDirectory(string directory, out SolidWorksInstallInfo? info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return false;

            string interopPath = Path.Combine(directory, "SolidWorks.Interop.sldworks.dll");
            if (!File.Exists(interopPath))
                return false;

            Version? interopVersion = TryGetFileVersion(interopPath);
            int year = InferYearFromInteropOrExe(directory, interopVersion);
            if (year is < 2022 or > 2026)
                return false;

            string exePath = Path.Combine(directory, "SLDWORKS.exe");
            info = new SolidWorksInstallInfo
            {
                Year = year,
                InstallDirectory = directory.TrimEnd('\\', '/'),
                SldWorksExePath = File.Exists(exePath) ? exePath : null,
                InteropAssemblyVersion = interopVersion
            };
            return true;
        }

        /// <summary>
        /// Install folder registered for ProgID <c>SldWorks.Application</c> (Windows default launch target).
        /// </summary>
        public static string? TryGetComDefaultInstallDirectory()
        {
            try
            {
                string? clsidText = ReadRegistryString(Registry.ClassesRoot, @"SldWorks.Application\CLSID", null)
                    ?? ReadRegistryString(Registry.ClassesRoot, @"SldWorks.Application\CLSID", string.Empty);

                Guid clsid = SldWorksClsid;
                if (!string.IsNullOrWhiteSpace(clsidText) && Guid.TryParse(clsidText.Trim(), out Guid parsed))
                    clsid = parsed;

                string clsidKey = $@"CLSID\{clsid:B}";
                string? localServer =
                    ReadRegistryString(Registry.ClassesRoot, clsidKey + @"\LocalServer32", null)
                    ?? ReadRegistryString(Registry.ClassesRoot, clsidKey + @"\LocalServer32", string.Empty)
                    ?? ReadRegistryString(Registry.ClassesRoot, @"WOW6432Node\" + clsidKey + @"\LocalServer32", null)
                    ?? ReadRegistryString(Registry.ClassesRoot, @"WOW6432Node\" + clsidKey + @"\LocalServer32", string.Empty);

                string? exe = ExtractExecutablePath(localServer);
                if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                    return null;

                return Path.GetDirectoryName(exe);
            }
            catch
            {
                return null;
            }
        }

        public static string FormatSelectionReason(SolidWorksInstallSelectionReason reason) => reason switch
        {
            SolidWorksInstallSelectionReason.WindowsComDefault => "Windows COM default (SldWorks.Application)",
            SolidWorksInstallSelectionReason.NewestInstalled => "newest installed (no COM default match)",
            _ => "none"
        };

        private static IEnumerable<string> EnumerateCandidateDirectories()
        {
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string corpRoot = Path.Combine(programFiles, "SOLIDWORKS Corp");
            if (Directory.Exists(corpRoot))
            {
                foreach (string dir in Directory.EnumerateDirectories(corpRoot, "SOLIDWORKS*"))
                    dirs.Add(dir);
            }

            foreach (int year in SupportedYears)
            {
                foreach (string? regDir in ReadRegistryInstallFolder(year))
                {
                    if (!string.IsNullOrWhiteSpace(regDir))
                        dirs.Add(regDir);
                }
            }

            string? comDir = TryGetComDefaultInstallDirectory();
            if (!string.IsNullOrWhiteSpace(comDir))
                dirs.Add(comDir);

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

        private static string? ReadRegistryString(RegistryKey root, string subKey, string? valueName)
        {
            try
            {
                using RegistryKey? key = root.OpenSubKey(subKey);
                if (key == null)
                    return null;

                object? value = valueName == null
                    ? key.GetValue(null)
                    : key.GetValue(valueName);
                return value as string;
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractExecutablePath(string? localServerValue)
        {
            if (string.IsNullOrWhiteSpace(localServerValue))
                return null;

            string raw = localServerValue.Trim();
            if (raw.StartsWith('"'))
            {
                int end = raw.IndexOf('"', 1);
                if (end > 1)
                    return raw.Substring(1, end - 1);
            }

            // "C:\...\SLDWORKS.exe" /automation  OR  C:\...\SLDWORKS.exe /automation
            Match match = Regex.Match(raw, @"^(?<path>.+?SLDWORKS\.exe)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups["path"].Value.Trim().Trim('"');

            string[] parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].Trim('"') : null;
        }

        private static int InferYearFromInteropOrExe(string directory, Version? interopVersion)
        {
            if (interopVersion != null && interopVersion.Major > 0)
                return MapRevisionMajorToYear(interopVersion.Major);

            string exePath = Path.Combine(directory, "SLDWORKS.exe");
            Version? exeVersion = TryGetFileVersion(exePath);
            if (exeVersion != null && exeVersion.Major > 0)
                return MapRevisionMajorToYear(exeVersion.Major);

            // Folder name hints: "SOLIDWORKS 2024", "SOLIDWORKS (2)" has no year — leave unsupported.
            Match yearMatch = Regex.Match(Path.GetFileName(directory.TrimEnd('\\', '/')), @"20(2[2-6])");
            if (yearMatch.Success && int.TryParse(yearMatch.Value, out int folderYear))
                return folderYear;

            return 0;
        }

        private static Version? TryGetFileVersion(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                var info = FileVersionInfo.GetVersionInfo(path);
                if (Version.TryParse(info.FileVersion, out Version? version))
                    return version;
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path.Trim().TrimEnd('\\', '/'));
            }
            catch
            {
                return path.Trim().TrimEnd('\\', '/');
            }
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
