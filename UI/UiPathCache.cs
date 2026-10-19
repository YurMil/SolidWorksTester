using System.Text.Json;

namespace SolidWorksTester.UI
{
    /// <summary>
    /// Remembers the drawing template and sheet-format paths in the current user's temp folder.
    /// A new machine has no file, so both fields start empty until the user sets them.
    /// </summary>
    internal static class UiPathCache
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        internal static string CacheFilePath { get; set; } = Path.Combine(
            Path.GetTempPath(),
            "SolidWorksTester",
            "paths.json");

        public static (string TemplatePath, string SheetFormatPath) Load()
        {
            try
            {
                if (!File.Exists(CacheFilePath))
                    return ("", "");

                PathDto? dto = JsonSerializer.Deserialize<PathDto>(File.ReadAllText(CacheFilePath));
                return (Clean(dto?.TemplatePath), Clean(dto?.SheetFormatPath));
            }
            catch
            {
                return ("", "");
            }
        }

        public static void Save(string? templatePath, string? sheetFormatPath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(CacheFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(
                    new PathDto
                    {
                        TemplatePath = templatePath ?? "",
                        SheetFormatPath = sheetFormatPath ?? ""
                    },
                    JsonOptions);

                string temporary = CacheFilePath + ".tmp";
                File.WriteAllText(temporary, json);
                File.Move(temporary, CacheFilePath, overwrite: true);
            }
            catch
            {
                // The drawing run does not depend on the cache.
            }
        }

        private static string Clean(string? value) => value?.Trim() ?? "";

        private sealed class PathDto
        {
            public string TemplatePath { get; set; } = "";
            public string SheetFormatPath { get; set; } = "";
        }
    }
}
