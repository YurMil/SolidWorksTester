using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksTester.Services.Analysis
{
    public sealed record CustomPropertySnapshot(
        IReadOnlyDictionary<string, string> FileProperties,
        IReadOnlyDictionary<string, string> ConfigurationProperties,
        string ActiveConfiguration);

    /// <summary>Reads file-level and configuration-level custom properties via COM.</summary>
    public static class CustomPropertyReader
    {
        public static CustomPropertySnapshot Read(IModelDoc2 model)
        {
            string configName = model.ConfigurationManager?.ActiveConfiguration?.Name ?? string.Empty;
            var fileProps = ReadScope(model, configurationName: string.Empty);
            var configProps = string.IsNullOrEmpty(configName)
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : ReadScope(model, configName);

            return new CustomPropertySnapshot(fileProps, configProps, configName);
        }

        public static string? ResolveFirst(
            CustomPropertySnapshot snapshot,
            IEnumerable<string> keys)
        {
            foreach (string key in keys)
            {
                if (snapshot.ConfigurationProperties.TryGetValue(key, out string? configValue) &&
                    !string.IsNullOrWhiteSpace(configValue))
                    return configValue.Trim();

                if (snapshot.FileProperties.TryGetValue(key, out string? fileValue) &&
                    !string.IsNullOrWhiteSpace(fileValue))
                    return fileValue.Trim();
            }

            return null;
        }

        private static Dictionary<string, string> ReadScope(IModelDoc2 model, string configurationName)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ModelDocExtension? ext = model.Extension;
            if (ext == null)
                return result;

            CustomPropertyManager? cpm = ext.CustomPropertyManager[configurationName];
            if (cpm == null)
                return result;

            object? namesObj = cpm.GetNames();
            if (namesObj is not string[] names)
                return result;

            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string raw = string.Empty;
                string resolved = string.Empty;
                cpm.Get4(name, false, out raw, out resolved);

                string value = !string.IsNullOrWhiteSpace(resolved) ? resolved : raw;
                if (!string.IsNullOrWhiteSpace(value))
                    result[name.Trim()] = value.Trim();
            }

            return result;
        }
    }
}
