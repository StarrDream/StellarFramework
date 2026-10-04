using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Resolves configured runtime metadata keys against the selected BaseRelease snapshot.</summary>
    public static class HotUpdateAotMetadataSelection
    {
        private const string DllBytesExtension = ".dll.bytes";

        public static IReadOnlyList<string> SelectBaseReleasePaths(
            IEnumerable<string> baseReleasePaths,
            IEnumerable<string> metadataKeys)
        {
            if (baseReleasePaths == null) throw new ArgumentNullException(nameof(baseReleasePaths));
            if (metadataKeys == null) throw new ArgumentNullException(nameof(metadataKeys));

            var availablePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in baseReleasePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new InvalidDataException("Selected BaseRelease contains an empty AOT metadata path.");

                string fileName = Path.GetFileName(path);
                if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Selected BaseRelease AOT metadata '{fileName}' is not a DLL.");
                if (!availablePaths.TryAdd(fileName, path))
                    throw new InvalidDataException($"Selected BaseRelease contains duplicate AOT metadata '{fileName}'.");
            }

            List<string> names = GetMetadataNames(metadataKeys);
            if (names.Count == 0)
                throw new InvalidDataException("HotUpdateSettings must select at least one AOT metadata key.");

            var selectedPaths = new List<string>(names.Count);
            foreach (string name in names)
            {
                string fileName = name + ".dll";
                if (!availablePaths.TryGetValue(fileName, out string path))
                    throw new FileNotFoundException(
                        $"HotUpdateSettings selects AOT metadata '{fileName}', but it is missing from the selected BaseRelease.");
                selectedPaths.Add(path);
            }

            return selectedPaths;
        }

        public static string[] GetGeneratedAssetPaths(string outputRoot, IEnumerable<string> metadataKeys)
        {
            if (string.IsNullOrWhiteSpace(outputRoot)) throw new ArgumentException("Output root is required.", nameof(outputRoot));
            List<string> names = GetMetadataNames(metadataKeys);
            if (names.Count == 0)
                throw new InvalidDataException("HotUpdateSettings must select at least one AOT metadata key.");

            string normalizedRoot = outputRoot.Replace('\\', '/').TrimEnd('/');
            return names.Select(name => normalizedRoot + "/Metadata/" + name + DllBytesExtension).ToArray();
        }

        private static List<string> GetMetadataNames(IEnumerable<string> metadataKeys)
        {
            if (metadataKeys == null) throw new ArgumentNullException(nameof(metadataKeys));

            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawKey in metadataKeys)
            {
                string key = (rawKey ?? string.Empty).Trim().Replace('\\', '/');
                int separatorIndex = key.LastIndexOf('/');
                string fileName = separatorIndex < 0 ? key : key.Substring(separatorIndex + 1);
                if (!fileName.EndsWith(DllBytesExtension, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        $"AOT metadata key '{rawKey}' must identify a '.dll.bytes' asset.");

                string name = fileName.Substring(0, fileName.Length - DllBytesExtension.Length);
                if (string.IsNullOrWhiteSpace(name) || name == "." || name == "..")
                    throw new InvalidDataException($"AOT metadata key '{rawKey}' has an invalid file name.");
                if (seen.Add(name)) names.Add(name);
            }

            return names;
        }
    }
}
