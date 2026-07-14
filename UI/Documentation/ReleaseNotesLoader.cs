using System.IO;
using System.Reflection;

namespace SolidWorksTester.UI.Documentation
{
    /// <summary>
    /// Loads version-specific release notes from Markdown files.
    /// File naming: Docs/ReleaseNotes/v{AppMetadata.Version}.md
    /// </summary>
    internal static class ReleaseNotesLoader
    {
        private const string FolderName = "Docs/ReleaseNotes";
        private const string EmbeddedPrefix = "SolidWorksTester.Docs.ReleaseNotes.";

        public static string GetMarkdownForCurrentVersion() =>
            GetMarkdownForVersion(AppMetadata.Version);

        public static string GetMarkdownForVersion(string version)
        {
            string fileName = AppMetadata.GetReleaseNotesFileName(version);

            string? fromDisk = TryLoadFromDisk(fileName);
            if (fromDisk != null)
                return fromDisk;

            string? fromResource = TryLoadFromEmbeddedResource(fileName);
            if (fromResource != null)
                return fromResource;

            return BuildMissingVersionDocument(version, fileName);
        }

        private static string? TryLoadFromDisk(string fileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, FolderName, fileName);
            if (!File.Exists(path))
                return null;

            return File.ReadAllText(path);
        }

        private static string? TryLoadFromEmbeddedResource(string fileName)
        {
            string resourceName = EmbeddedPrefix + fileName;
            Assembly assembly = Assembly.GetExecutingAssembly();

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static string BuildMissingVersionDocument(string version, string fileName) =>
            $"""
            # {AppMetadata.ApplicationTitle}

            ## Document control

            | Field | Value |
            | --- | --- |
            | Version | {version} |
            | Author | {AppMetadata.Author} |
            | Status | Documentation missing |

            ---

            ## Release notes not found

            Expected Markdown file:

            `{FolderName}/{fileName}`

            Add the file and rebuild, or place it next to the executable under `Docs/ReleaseNotes/`.
            """;
    }
}
