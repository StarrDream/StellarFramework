using System;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>
    /// Runtime registry that lets optional SDK Editor assemblies provide build adapters
    /// without making the Publisher Hub reference those SDK assemblies directly.
    /// </summary>
    public static class HotUpdatePublisherBuildAdapters
    {
        public static Func<HotUpdateBaseReleaseRepository, IHotUpdateBuildAdapter> HybridCLRFactory { get; set; }
        public static Func<IHotUpdateBuildAdapter> YooAssetFactory { get; set; }
        public static Func<string> HybridCLRPackageVersionProvider { get; set; }

        public static string GetReadinessError()
        {
            if (HybridCLRFactory == null || HybridCLRPackageVersionProvider == null)
                return "HybridCLR Editor adapter is not loaded. Install/enable HybridCLR and wait for its Publisher adapter to compile.";
            if (YooAssetFactory == null)
                return "YooAsset Editor adapter is not loaded. Install/enable YooAsset and wait for its Publisher adapter to compile.";
            return string.Empty;
        }

        public static string GetHybridCLRPackageVersion()
        {
            if (HybridCLRPackageVersionProvider == null)
                throw new InvalidOperationException(GetReadinessError());
            string version = HybridCLRPackageVersionProvider();
            if (string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("HybridCLR package version could not be read from the loaded Editor assembly.");
            return version;
        }

        public static IHotUpdateBuildAdapter Create(HotUpdateBaseReleaseRepository baseReleaseRepository)
        {
            string readinessError = GetReadinessError();
            if (!string.IsNullOrEmpty(readinessError)) throw new InvalidOperationException(readinessError);

            IHotUpdateBuildAdapter hybridCLR = HybridCLRFactory(baseReleaseRepository);
            IHotUpdateBuildAdapter yooAsset = YooAssetFactory();
            if (hybridCLR == null || yooAsset == null)
                throw new InvalidOperationException("A HotUpdate Publisher SDK adapter returned null.");
            return new CompositeHotUpdateBuildAdapter(hybridCLR, yooAsset);
        }
    }
}
