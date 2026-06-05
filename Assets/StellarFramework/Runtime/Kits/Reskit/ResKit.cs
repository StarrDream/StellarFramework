using System;
using System.Collections.Generic;
using StellarFramework.Pool;

namespace StellarFramework.Res
{
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
    /// ResKit unified loading portal.
    /// Keeps typed loader allocation while adding backend-based allocation for production projects.
    /// </summary>
    public static class ResKit
    {
        private static readonly Dictionary<ResLoadBackend, ResLoaderFactory> _backendFactories =
            new Dictionary<ResLoadBackend, ResLoaderFactory>();

        private static readonly Dictionary<string, ResLoaderFactory> _customFactories =
            new Dictionary<string, ResLoaderFactory>(StringComparer.Ordinal);

        private static ResLoadBackend _configuredDefaultBackend = ResLoadBackend.Default;
        private static string _configuredDefaultCustomKey = string.Empty;
        private static ResKitRuntimeSettings _configuredRuntimeSettings;

        /// <summary>
        /// Allocates a typed loader from PoolKit. This is kept for backward compatibility.
        /// </summary>
        public static T Allocate<T>() where T : ResLoader, new()
        {
            return PoolKit.Allocate<T>();
        }

        /// <summary>
        /// Configures the default backend used by Allocate(ResLoaderRequest.Default()).
        /// Passing ResLoadBackend.Default lets ResKitRuntimeSettings decide, then falls back to Resources.
        /// </summary>
        public static void Configure(ResLoadBackend defaultBackend = ResLoadBackend.Default,
            ResKitRuntimeSettings runtimeSettings = null,
            string defaultCustomLoaderKey = null)
        {
            _configuredDefaultBackend = defaultBackend;
            _configuredDefaultCustomKey = NormalizeCustomKey(defaultCustomLoaderKey);
            _configuredRuntimeSettings = runtimeSettings;
        }

        public static void RegisterLoaderFactory(ResLoadBackend backend, ResLoaderFactory factory)
        {
            if (backend == ResLoadBackend.Default || backend == ResLoadBackend.Custom)
            {
                LogKit.LogError($"[ResKit] RegisterLoaderFactory failed: backend cannot be {backend}.");
                return;
            }

            if (factory == null)
            {
                _backendFactories.Remove(backend);
                return;
            }

            _backendFactories[backend] = factory;
        }

        public static void RegisterCustomLoader(string customKey, ResLoaderFactory factory)
        {
            string key = NormalizeCustomKey(customKey);
            if (string.IsNullOrEmpty(key))
            {
                LogKit.LogError("[ResKit] RegisterCustomLoader failed: customKey is empty.");
                return;
            }

            if (factory == null)
            {
                _customFactories.Remove(key);
                return;
            }

            _customFactories[key] = factory;
        }

        public static void UnregisterCustomLoader(string customKey)
        {
            string key = NormalizeCustomKey(customKey);
            if (!string.IsNullOrEmpty(key))
            {
                _customFactories.Remove(key);
            }
        }

        /// <summary>
        /// Allocates a loader by backend request. This is the recommended production entry.
        /// </summary>
        public static IResLoader Allocate(ResLoaderRequest request)
        {
            ResLoaderRequest resolvedRequest = ResolveRequest(request);
            ResLoadBackend backend = resolvedRequest.Backend;
            IResLoader loader = null;

            if (backend == ResLoadBackend.Custom)
            {
                loader = AllocateCustom(resolvedRequest);
            }
            else if (_backendFactories.TryGetValue(backend, out ResLoaderFactory factory))
            {
                loader = factory.Invoke(resolvedRequest);
            }
            else
            {
                loader = AllocateBuiltin(backend);
            }

            if (loader == null)
            {
                LogKit.LogError(
                    $"[ResKit] Allocate failed: Backend={backend}, CustomKey={request.CustomKey ?? "null"}");
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
        /// Recycles a typed loader. Resource release is handled by the pool recycle hook.
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
        /// Recycles an interface loader through its runtime implementation.
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

        private static IResLoader AllocateBuiltin(ResLoadBackend backend)
        {
            switch (backend)
            {
                case ResLoadBackend.Resources:
                    return Allocate<ResourceLoader>();
                case ResLoadBackend.AssetBundle:
                    return Allocate<AssetBundleLoader>();
                default:
                    LogKit.LogError($"[ResKit] Unsupported builtin backend: {backend}");
                    return null;
            }
        }

        private static IResLoader AllocateCustom(ResLoaderRequest request)
        {
            string key = NormalizeCustomKey(request.CustomKey);
            if (string.IsNullOrEmpty(key))
            {
                LogKit.LogError("[ResKit] Custom loader allocation failed: CustomKey is empty. Register your custom backend first, then pass a non-empty CustomKey.");
                return null;
            }

            if (!_customFactories.TryGetValue(key, out ResLoaderFactory factory))
            {
                LogKit.LogError($"[ResKit] Custom loader allocation failed: factory is not registered. Key={key}. Call ResKit.RegisterCustomLoader before Allocate(ResLoaderRequest.Custom(...)).");
                return null;
            }

            return factory.Invoke(request);
        }

        private static string NormalizeCustomKey(string customKey)
        {
            return string.IsNullOrWhiteSpace(customKey) ? string.Empty : customKey.Trim();
        }
    }
}
