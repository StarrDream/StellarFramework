using System.IO;
using UnityEngine;

namespace StellarFrameworkVerification.Runtime
{
    /// <summary>
    /// Shared names and project-local paths for the maintainer-only HotUpdate release gate.
    /// Keeping them in the Runtime verification assembly lets Editor preparation and PlayMode
    /// tests share one contract without a Runtime-to-Editor assembly dependency.
    /// </summary>
    public static class HotUpdateVerificationPaths
    {
        public const string PackageName = "StellarHotUpdateVerification";
        public const string PackageVersion = "verification-v1";
        public const int AndroidDefaultCdnPort = 18743;

        // Intent keys are duplicated by Tools/AndroidVerification and covered by policy tests.
        public const string AndroidVerifyIntentExtra = "stellar.hotupdate.verify";
        public const string AndroidHostIntentExtra = "stellar.hotupdate.host";
        public const string AndroidPortIntentExtra = "stellar.hotupdate.port";
        public const string AndroidPackageIntentExtra = "stellar.hotupdate.package";
        public const string AndroidVersionIntentExtra = "stellar.hotupdate.version";
        public const string AndroidExpectCacheIntentExtra = "stellar.hotupdate.expectCache";

        private const string VerificationDirectoryName = "StellarHotUpdateVerification";

        public static string RootDirectoryPath => Path.Combine(
            GetProjectRoot(),
            "Temp",
            VerificationDirectoryName).Replace('\\', '/');

        public static string PackageOutputRoot => Path.Combine(
            RootDirectoryPath,
            "Bundles").Replace('\\', '/');

        public static string RemoteCdnDirectoryPath => Path.Combine(
            RootDirectoryPath,
            "RemoteCDN").Replace('\\', '/');

        public static string ClientCacheDirectoryPath => Path.Combine(
            RootDirectoryPath,
            "ClientCache").Replace('\\', '/');

        public static string RuntimeConfigFilePath => Path.Combine(
            RootDirectoryPath,
            "runtime-config.json").Replace('\\', '/');

        public static string RuntimeResultFilePath => Path.Combine(
            RootDirectoryPath,
            "runtime-result.json").Replace('\\', '/');

        public static string AndroidPreparationResultFilePath => Path.Combine(
            RootDirectoryPath,
            "android-release-preparation.json").Replace('\\', '/');

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }
    }
}
