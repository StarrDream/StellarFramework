using System;
using System.Text.RegularExpressions;

namespace StellarFrameworkInstaller
{
    internal static class StellarFrameworkPackageManifestUtility
    {
        public static bool ManifestContainsPackage(string manifestJson, string packageId)
        {
            if (string.IsNullOrWhiteSpace(manifestJson) || string.IsNullOrWhiteSpace(packageId))
            {
                return false;
            }

            string escapedPackageId = Regex.Escape(packageId.Trim());
            return Regex.IsMatch(manifestJson, "\"" + escapedPackageId + "\"\\s*:", RegexOptions.CultureInvariant);
        }

        public static string BuildPackageSource(string packageId, string version, string gitUrl)
        {
            if (!string.IsNullOrWhiteSpace(gitUrl))
            {
                return gitUrl.Trim();
            }

            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package id is empty.", nameof(packageId));
            }

            return string.IsNullOrWhiteSpace(version)
                ? packageId.Trim()
                : packageId.Trim() + "@" + version.Trim();
        }

        public static string BuildFilePackageSource(string localPath)
        {
            if (string.IsNullOrWhiteSpace(localPath))
            {
                throw new ArgumentException("Local package path is empty.", nameof(localPath));
            }

            string fullPath = StellarFrameworkInstallerPathUtility.ToFullPath(localPath).Replace('\\', '/');
            return fullPath.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? fullPath : "file:" + fullPath;
        }
    }
}
