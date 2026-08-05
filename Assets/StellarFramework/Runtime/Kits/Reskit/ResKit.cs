using System;
using System.Collections.Generic;
using StellarFramework.Pool;

namespace StellarFramework.Res
{
    /// <summary>
    /// 内置后端枚举。
    /// 仅作为内置后端的强类型别名与资产序列化兼容入口。
    /// 新增后端无需修改此枚举：统一通过 ResKit.RegisterLoader(string key, factory) 注册即可。
    /// </summary>
    public enum ResLoadBackend
    {
        Default = 0,
        Resources = 1,
        AssetBundle = 3,
        Custom = 100
    }

    public struct ResLoaderRequest
    {
        public ResLoadBackend Backend;
        public string OwnerName;
        public string CustomKey;

        public static ResLoaderRequest Default(string ownerName = null)
        {
            return new ResLoaderRequest
            {
                Backend = ResLoadBackend.Default,
                OwnerName = ownerName
            };
        }

        public static ResLoaderRequest For(ResLoadBackend backend, string ownerName = null)
        {
            return new ResLoaderRequest
            {
                Backend = backend,
                OwnerName = ownerName
            };
        }

        public static ResLoaderRequest Custom(string customKey, string ownerName = null)
        {
            return new ResLoaderRequest
            {
                Backend = ResLoadBackend.Custom,
                CustomKey = customKey,
                OwnerName = ownerName
            };
        }
    }

    public delegate IResLoader ResLoaderFactory(ResLoaderRequest request);

    /// <summary>
    /// ResKit 统一资源加载门户。
    /// 内部使用"字符串 key → 工厂"统一注册表：内置后端与自定义后端共用一套机制，
    /// 新增后端只需 RegisterLoader(key, factory)，无需修改 ResLoadBackend 枚举或 switch 分支。
    /// 枚举入口（Allocate(ResLoadBackend)、RegisterLoaderFactory(ResLoadBackend)）保留为兼容层。
    /// </summary>
    public static class ResKit
    {
        public const string KeyResources = "Resources";
        public const string KeyAssetBundle = "AssetBundle";

        private static readonly Dictionary<string, ResLoaderFactory> _factories =
            new Dictionary<string, ResLoaderFactory>(StringComparer.Ordinal);

        private static ResLoadBackend _configuredDefaultBackend = ResLoadBackend.Default;
        private static string _configuredDefaultCustomKey = string.Empty;
        private static ResKitRuntimeSettings _configuredRuntimeSettings;

        static ResKit()
        {
            // 内置后端预注册进统一注册表。
            // 注册仅是登记工厂委托，不会触发实例化，因此无初始化顺序/循环依赖风险。
            _factories[KeyResources] = request => Allocate<ResourceLoader>();
            _factories[KeyAssetBundle] = request => Allocate<AssetBundleLoader>();
        }

        /// <summary>
        /// 按类型从 PoolKit 分配加载器。保留兼容，但业务侧请优先使用 Allocate(ResLoaderRequest) 统一走后端注册表。
        /// </summary>
        public static T Allocate<T>() where T : ResLoader, new()
        {
            return PoolKit.Allocate<T>();
        }

        /// <summary>
        /// 配置默认后端。传 ResLoadBackend.Default 时由 ResKitRuntimeSettings 决定，最后回退 Resources。
        /// </summary>
        public static void Configure(ResLoadBackend defaultBackend = ResLoadBackend.Default,
            ResKitRuntimeSettings runtimeSettings = null,
            string defaultCustomLoaderKey = null)
        {
            _configuredDefaultBackend = defaultBackend;
            _configuredDefaultCustomKey = NormalizeCustomKey(defaultCustomLoaderKey);
            _configuredRuntimeSettings = runtimeSettings;
        }

        /// <summary>
        /// 统一注册入口（推荐）。
        /// key 可为内置常量（KeyResources / KeyAssetBundle）或任意自定义 key（如 "YooAsset"、"Addressables"）。
        /// 传入 null factory 表示移除该 key 的注册。
        /// </summary>
        public static void RegisterLoader(string loaderKey, ResLoaderFactory factory)
        {
            string key = NormalizeCustomKey(loaderKey);
            if (string.IsNullOrEmpty(key))
            {
                LogKit.LogError("[ResKit] RegisterLoader failed: loaderKey is empty.");
                return;
            }

            if (factory == null)
            {
                _factories.Remove(key);
                return;
            }

            _factories[key] = factory;
        }

        /// <summary>
        /// 兼容入口：按内置枚举注册（可覆盖内置后端实现）。
        /// </summary>
        public static void RegisterLoaderFactory(ResLoadBackend backend, ResLoaderFactory factory)
        {
            if (backend == ResLoadBackend.Default || backend == ResLoadBackend.Custom)
            {
                LogKit.LogError($"[ResKit] RegisterLoaderFactory failed: backend cannot be {backend}.");
                return;
            }

            RegisterLoader(BackendToKey(backend), factory);
        }

        /// <summary>
        /// 兼容入口：注册自定义字符串 key 加载器。
        /// </summary>
        public static void RegisterCustomLoader(string customKey, ResLoaderFactory factory)
        {
            RegisterLoader(customKey, factory);
        }

        public static void UnregisterCustomLoader(string customKey)
        {
            RegisterLoader(customKey, null);
        }

        /// <summary>
        /// 按后端请求分配加载器（推荐入口）。
        /// </summary>
        public static IResLoader Allocate(ResLoaderRequest request)
        {
            ResLoaderRequest resolvedRequest = ResolveRequest(request);

            string key = resolvedRequest.Backend == ResLoadBackend.Custom
                ? NormalizeCustomKey(resolvedRequest.CustomKey)
                : BackendToKey(resolvedRequest.Backend);

            if (string.IsNullOrEmpty(key))
            {
                LogKit.LogError(
                    $"[ResKit] Allocate failed: Backend={resolvedRequest.Backend}, CustomKey={resolvedRequest.CustomKey ?? "null"}");
                return null;
            }

            if (!_factories.TryGetValue(key, out ResLoaderFactory factory))
            {
                LogKit.LogError(
                    $"[ResKit] Allocate failed: factory is not registered. Key={key}. Call ResKit.RegisterLoader(\"{key}\", factory) before allocation.");
                return null;
            }

            IResLoader loader = factory.Invoke(resolvedRequest);
            if (loader == null)
            {
                LogKit.LogError(
                    $"[ResKit] Allocate failed: factory returned null. Key={key}, Backend={resolvedRequest.Backend}");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(resolvedRequest.OwnerName) && loader is ResLoader resLoader)
            {
                resLoader.SetOwnerName(resolvedRequest.OwnerName);
            }

            return loader;
        }

        public static IResLoader Allocate(ResLoadBackend backend, string ownerName = null)
        {
            return Allocate(ResLoaderRequest.For(backend, ownerName));
        }

        /// <summary>
        /// 回收类型化加载器。资源释放由对象池回收钩子处理。
        /// </summary>
        public static void Recycle<T>(T loader) where T : ResLoader, new()
        {
            if (loader == null)
            {
                LogKit.LogError("[ResKit] Recycle failed: loader is null.");
                return;
            }

            PoolKit.Recycle(loader);
        }

        /// <summary>
        /// 通过接口运行时实现回收加载器。
        /// </summary>
        public static void Recycle(IResLoader loader)
        {
            if (loader == null)
            {
                LogKit.LogError("[ResKit] Recycle(IResLoader) failed: loader is null.");
                return;
            }

            loader.RecycleToPool();
        }

        private static ResLoaderRequest ResolveRequest(ResLoaderRequest request)
        {
            if (request.Backend != ResLoadBackend.Default)
            {
                return request;
            }

            if (_configuredDefaultBackend != ResLoadBackend.Default)
            {
                return BuildResolvedRequest(
                    _configuredDefaultBackend,
                    _configuredDefaultCustomKey,
                    request.OwnerName);
            }

            ResKitRuntimeSettings settings = _configuredRuntimeSettings ??
                                             ResKitRuntimeSettings.LoadOrCreateDefault();
            if (settings != null && settings.DefaultLoadBackend != ResLoadBackend.Default)
            {
                return BuildResolvedRequest(
                    settings.DefaultLoadBackend,
                    settings.DefaultCustomLoaderKey,
                    request.OwnerName);
            }

            return ResLoaderRequest.For(ResLoadBackend.Resources, request.OwnerName);
        }

        private static ResLoaderRequest BuildResolvedRequest(ResLoadBackend backend, string customKey,
            string ownerName)
        {
            return backend == ResLoadBackend.Custom
                ? ResLoaderRequest.Custom(customKey, ownerName)
                : ResLoaderRequest.For(backend, ownerName);
        }

        /// <summary>
        /// 内置枚举 → 注册表字符串 key。
        /// 仅兼容层使用：新增后端不经过这里，直接 RegisterLoader(string key, factory)。
        /// </summary>
        private static string BackendToKey(ResLoadBackend backend)
        {
            switch (backend)
            {
                case ResLoadBackend.Resources:
                    return KeyResources;
                case ResLoadBackend.AssetBundle:
                    return KeyAssetBundle;
                default:
                    return string.Empty;
            }
        }

        private static string NormalizeCustomKey(string customKey)
        {
            return string.IsNullOrWhiteSpace(customKey) ? string.Empty : customKey.Trim();
        }
    }
}
