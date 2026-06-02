using System.IO;
using UnityEngine;

namespace StellarFrameworkInstaller
{
    internal static class StellarFrameworkInstallerPathUtility
    {
        public static string ProjectRoot
        {
            get
            {
                DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
                return dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
            }
        }

        public static string ToFullPath(string assetOrProjectPath)
        {
            if (string.IsNullOrWhiteSpace(assetOrProjectPath))
            {
                return ProjectRoot;
            }

            if (Path.IsPathRooted(assetOrProjectPath))
            {
                return Path.GetFullPath(assetOrProjectPath);
            }

            return Path.GetFullPath(Path.Combine(ProjectRoot, assetOrProjectPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : assetPath.Replace('\\', '/').TrimEnd('/');
        }
    }
}
