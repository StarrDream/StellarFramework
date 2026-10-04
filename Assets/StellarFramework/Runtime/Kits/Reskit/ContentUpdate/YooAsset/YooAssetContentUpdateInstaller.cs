namespace StellarFramework.Res
{
    /// <summary>Registers YooAsset's HostPlayMode content-update provider independently of its ResKit loader.</summary>
    public static class YooAssetContentUpdateInstaller
    {
        public static void Install()
        {
            ResKit.RegisterContentUpdateProvider<
                YooAssetContentUpdateOptions,
                YooAssetContentUpdateProgress,
                YooAssetContentUpdateResult>(
                    YooAssetResContentUpdateProvider.ProviderId,
                    new YooAssetResContentUpdateProvider());
        }

        public static void Uninstall()
        {
            ResKit.RegisterContentUpdateProvider<
                YooAssetContentUpdateOptions,
                YooAssetContentUpdateProgress,
                YooAssetContentUpdateResult>(YooAssetResContentUpdateProvider.ProviderId, null);
        }
    }
}
