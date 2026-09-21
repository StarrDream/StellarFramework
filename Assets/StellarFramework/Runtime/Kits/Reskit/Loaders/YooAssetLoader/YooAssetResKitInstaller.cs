using StellarFramework.Pool;

namespace StellarFramework.Res
{
    /// <summary>
    /// 把 YooAsset ResourcePackage 注册为 ResKit 可选后端。
    /// </summary>
    public static class YooAssetResKitInstaller
    {
        /// <summary>ResKit 注册表中的 YooAsset 后端 Key。</summary>
        public const string LoaderKey = "YooAsset";

        /// <summary>YooAsset 官方默认 Package 名称。</summary>
        public const string DefaultPackageName = "DefaultPackage";

        /// <summary>
        /// 注册 YooAsset Loader。
        /// Package 必须在真正 Load 之前由项目启动流程创建并初始化成功。
        /// </summary>
        /// <param name="packageName">要绑定的 YooAsset ResourcePackage 名称。</param>
        public static void Install(string packageName = DefaultPackageName)
        {
            string normalizedPackageName = string.IsNullOrWhiteSpace(packageName)
                ? DefaultPackageName
                : packageName.Trim();

            ResKit.RegisterLoader(LoaderKey, request =>
            {
                YooAssetLoader loader = PoolKit.Allocate<YooAssetLoader>();
                loader.Configure(normalizedPackageName);
                return loader;
            });
        }

        /// <summary>
        /// 注销 YooAsset Loader factory。
        /// 已经分配出去的 Loader 不受影响，仍应由各自 Scope/Owner 正常释放。
        /// </summary>
        public static void Uninstall()
        {
            ResKit.RegisterLoader(LoaderKey, null);
        }
    }
}
