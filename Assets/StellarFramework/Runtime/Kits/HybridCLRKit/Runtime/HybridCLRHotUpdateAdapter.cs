using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.HybridCLR
{
    /// <summary>
    /// HybridCLR 热更生命周期钩子
    /// 职责：提供标准化的代码热更装载与跳转流程，彻底解耦 AOT 环境与 HotUpdate 环境。
    /// </summary>
    public static class HybridCLRHook
    {
        public enum HotUpdateState
        {
            None,
            LoadingMetadata,
            MetadataLoaded,
            LoadingHotUpdateAssembly,
            LoadedHotUpdateAssembly,
            EnteringHotUpdate,
            EnteredHotUpdate,
            Failed
        }

        [Header("热更配置规范")] public static string HotUpdateAssemblyName = "HotUpdate.dll";

        public static string HotUpdateEntryClass = "HotUpdate.HotUpdateMain";
        public static string HotUpdateEntryMethod = "Main";

        /// <summary>
        /// 补充元数据 DLL 列表
        /// </summary>
        public static List<string> AOTMetaAssemblyFiles = new List<string>
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll"
        };

        public static HotUpdateState State { get; private set; } = HotUpdateState.None;
        public static string LastError { get; private set; }
        public static string LoadedAssemblyFullName { get; private set; }

        /// <summary>
        /// 步骤 1：加载 AOT 补充元数据
        /// </summary>
        public static async UniTask<bool> LoadMetadataForAOTAssembliesAsync(
            Func<string, UniTask<byte[]>> dllBytesProvider)
        {
#if !HYBRIDCLR_ENABLE
            State = HotUpdateState.LoadingMetadata;
            LastError = null;
            SetFailed("[HybridCLRHook] HYBRIDCLR_ENABLE is not enabled. AOT metadata loading is unavailable.");
            await UniTask.CompletedTask;
            return false;
#else
            if (dllBytesProvider == null)
            {
                SetFailed("[HybridCLRHook] 加载 AOT 元数据失败: dllBytesProvider 为空");
                return false;
            }

            State = HotUpdateState.LoadingMetadata;
            LastError = null;

            for (int i = 0; i < AOTMetaAssemblyFiles.Count; i++)
            {
                string aotDllName = AOTMetaAssemblyFiles[i];
                if (string.IsNullOrEmpty(aotDllName))
                {
                    SetFailed("[HybridCLRHook] 加载 AOT 元数据失败: 检测到空 DLL 名称");
                    return false;
                }

                byte[] dllBytes = await dllBytesProvider.Invoke(aotDllName);
                if (dllBytes == null || dllBytes.Length == 0)
                {
                    SetFailed($"[HybridCLRHook] 无法获取 AOT 元数据 DLL 字节流: {aotDllName}");
                    return false;
                }

                if (!TryLoadMetadataForAotAssembly(dllBytes, out string metadataError))
                {
                    SetFailed(metadataError);
                    return false;
                }

                LogKit.Log($"[HybridCLRHook] 成功加载 AOT 补充元数据: {aotDllName}");
            }

            State = HotUpdateState.MetadataLoaded;
            return true;
#endif
        }

        /// <summary>
        /// 步骤 2：加载热更程序集并执行跳转
        /// </summary>
        public static bool LoadAndStartHotUpdateAssembly(byte[] hotUpdateDllBytes)
        {
            if (hotUpdateDllBytes == null || hotUpdateDllBytes.Length == 0)
            {
                SetFailed("[HybridCLRHook] 启动热更失败: 热更 DLL 字节流为空");
                return false;
            }

            State = HotUpdateState.LoadingHotUpdateAssembly;
            LastError = null;
            LoadedAssemblyFullName = null;

            Assembly hotUpdateAssembly = null;
            try
            {
                hotUpdateAssembly = Assembly.Load(hotUpdateDllBytes);
            }
            catch (Exception e)
            {
                SetFailed($"[HybridCLRHook] 加载热更程序集失败: Exception={e.Message}");
                return false;
            }

            if (hotUpdateAssembly == null)
            {
                SetFailed("[HybridCLRHook] 加载热更程序集失败: Assembly.Load 返回为空");
                return false;
            }

            LoadedAssemblyFullName = hotUpdateAssembly.FullName;
            State = HotUpdateState.LoadedHotUpdateAssembly;
            LogKit.Log($"[HybridCLRHook] 成功加载热更程序集: {LoadedAssemblyFullName}");

            Type entryType = hotUpdateAssembly.GetType(HotUpdateEntryClass);
            if (entryType == null)
            {
                SetFailed($"[HybridCLRHook] 找不到热更入口类: {HotUpdateEntryClass}");
                return false;
            }

            MethodInfo method = entryType.GetMethod(HotUpdateEntryMethod, BindingFlags.Static | BindingFlags.Public);
            if (method == null)
            {
                SetFailed($"[HybridCLRHook] 找不到热更入口方法: {HotUpdateEntryMethod}, EntryClass={HotUpdateEntryClass}");
                return false;
            }

            State = HotUpdateState.EnteringHotUpdate;
            LogKit.Log("[HybridCLRHook] 正在跨域跳转至热更逻辑...");

            try
            {
                method.Invoke(null, null);
            }
            catch (Exception e)
            {
                SetFailed($"[HybridCLRHook] 执行热更入口失败: Exception={e.Message}\nStackTrace={e.StackTrace}");
                return false;
            }

            State = HotUpdateState.EnteredHotUpdate;
            LogKit.Log("[HybridCLRHook] 热更入口执行完成");
            return true;
        }

        private static bool TryLoadMetadataForAotAssembly(byte[] dllBytes, out string error)
        {
            error = null;
            try
            {
                global::HybridCLR.LoadImageErrorCode result =
                    global::HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(
                        dllBytes,
                        global::HybridCLR.HomologousImageMode.SuperSet);

                if (result != global::HybridCLR.LoadImageErrorCode.OK)
                {
                    error = $"[HybridCLRHook] LoadMetadataForAOTAssembly failed. ErrorCode={result} ({(int)result}).";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"[HybridCLRHook] LoadMetadataForAOTAssembly threw: {ex.Message}";
                return false;
            }

            return true;
        }

        private static void SetFailed(string error)
        {
            State = HotUpdateState.Failed;
            LastError = error;
            LogKit.LogError(error);
        }
    }


    public sealed class HybridCLRCodeHotUpdateStrategy : IHybridCLRCodeUpdateStrategy
    {
        public UniTask<HybridCLRUpdateResult> RunAsync(HotUpdateSettings settings = null,
            IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            return HybridCLRRunner.RunAsync(settings, progress, cancellationToken);
        }
    }

    /// <summary>
    /// Startup-only HybridCLR code-update runner.
    /// Content update/package download must already be complete.
    /// The runner only asks ResKit for dll.bytes assets, verifies them, loads AOT metadata,
    /// and enters the configured hot-update assembly.
    /// </summary>
    public static class HybridCLRRunner
    {
        public static HybridCLRUpdateState State { get; private set; } =
            HybridCLRUpdateState.None;

        public static string LastError { get; private set; }

        public static UniTask<HybridCLRUpdateResult> RunAsync(HotUpdateSettings settings,
            Action<float> onProgress, CancellationToken cancellationToken = default)
        {
            IProgress<float> progress = onProgress != null ? Progress.Create(onProgress) : null;
            return RunAsync(settings, progress, cancellationToken);
        }

        public static async UniTask<HybridCLRUpdateResult> RunAsync(HotUpdateSettings settings = null,
            IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            LastError = null;
            State = HybridCLRUpdateState.None;
            progress?.Report(0f);

#if !HYBRIDCLR_ENABLE
            await UniTask.CompletedTask;
            return Fail("HYBRIDCLR_ENABLE is not enabled. HybridCLR code update is unavailable.");
#else
            if (settings == null)
            {
                settings = HotUpdateSettings.LoadOrCreateDefault();
            }

            if (settings == null)
            {
                return Fail("HotUpdateSettings is null.");
            }

            bool strictProduction = HybridCLRRuntimePolicy.IsStrictProductionRuntime;

            HotUpdateSettingsValidationReport settingsValidation = settings.Validate(strictProduction);
            if (!settingsValidation.IsValid)
            {
                return Fail("HotUpdateSettings validation failed: " + string.Join(" | ", settingsValidation.Errors));
            }

            try
            {
                string loaderKey = settings.ResourceLoaderKey?.Trim();
                using ResScope resources = ResKit.CreateCustomScope(
                    loaderKey,
                    "HybridCLRRunner");
                IResLoader loader = resources.Loader;

                State = HybridCLRUpdateState.LoadingManifest;
                HotUpdateManifest manifest = await LoadManifestAsync(
                    loader,
                    settings.HotUpdateManifestKey,
                    cancellationToken);
                HotUpdateManifestValidationReport manifestValidation = manifest.Validate(strictProduction);
                if (!manifestValidation.IsValid)
                {
                    return Fail("HotUpdateManifest validation failed: " + string.Join(" | ", manifestValidation.Errors));
                }

                string manifestSource = $"ResKit:{loaderKey}:{settings.HotUpdateManifestKey}";
                LogKit.Log($"[HybridCLRRunner] Manifest loaded from {manifestSource}");
                progress?.Report(0.15f);

                State = HybridCLRUpdateState.LoadingBytes;
                (Dictionary<string, byte[]> metadataBytes, byte[] hotUpdateBytes) =
                    await UniTask.WhenAll(
                        LoadMetadataBytesAsync(loader, manifest.aotMetadataKeys, cancellationToken),
                        LoadHotUpdateAssemblyBytesAsync(
                            loader,
                            manifest.hotUpdateAssemblyKey,
                            cancellationToken));

                string actualSha256;
                if (!VerifySha256(hotUpdateBytes, manifest.hotUpdateAssemblySha256, strictProduction, out actualSha256))
                {
                    return Fail(
                        $"Hot update dll SHA256 mismatch. Key={manifest.hotUpdateAssemblyKey}, Expected={manifest.hotUpdateAssemblySha256}, Actual={actualSha256}");
                }

                progress?.Report(0.55f);

                State = HybridCLRUpdateState.LoadingMetadata;
                HybridCLRHook.AOTMetaAssemblyFiles = new List<string>(metadataBytes.Keys);
                bool metadataLoaded = await HybridCLRHook.LoadMetadataForAOTAssembliesAsync(key =>
                {
                    byte[] bytes;
                    metadataBytes.TryGetValue(key, out bytes);
                    return UniTask.FromResult(bytes);
                });

                if (!metadataLoaded)
                {
                    return Fail(HybridCLRHook.LastError);
                }

                progress?.Report(0.78f);

                State = HybridCLRUpdateState.LoadingAssembly;
                if (!string.IsNullOrWhiteSpace(manifest.hotUpdateEntryClass))
                {
                    HybridCLRHook.HotUpdateEntryClass = manifest.hotUpdateEntryClass.Trim();
                }

                if (!string.IsNullOrWhiteSpace(manifest.hotUpdateEntryMethod))
                {
                    HybridCLRHook.HotUpdateEntryMethod = manifest.hotUpdateEntryMethod.Trim();
                }

                HybridCLRHook.HotUpdateAssemblyName = manifest.hotUpdateAssemblyKey.Trim();

                bool assemblyLoaded = HybridCLRHook.LoadAndStartHotUpdateAssembly(hotUpdateBytes);
                if (!assemblyLoaded)
                {
                    return Fail(HybridCLRHook.LastError);
                }

                State = HybridCLRUpdateState.EnteredHotUpdate;
                progress?.Report(1f);
                return new HybridCLRUpdateResult
                {
                    Success = true,
                    State = State,
                    LoadedAssemblyFullName = HybridCLRHook.LoadedAssemblyFullName,
                    Manifest = manifest,
                    ManifestSource = manifestSource
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
#endif
        }

        private static async UniTask<HotUpdateManifest> LoadManifestAsync(
            IResLoader loader,
            string manifestKey,
            CancellationToken cancellationToken)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));

            string key = string.IsNullOrWhiteSpace(manifestKey) ? string.Empty : manifestKey.Trim();
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("HotUpdateManifestKey is empty.");
            }

            TextAsset manifestAsset = await loader.LoadAsync<TextAsset>(key, cancellationToken);
            if (manifestAsset == null || string.IsNullOrWhiteSpace(manifestAsset.text))
            {
                throw new InvalidOperationException($"HotUpdateManifest load failed: {key}");
            }

            HotUpdateManifest manifest = HotUpdateManifest.FromJson(manifestAsset.text);
            if (manifest == null)
            {
                throw new InvalidOperationException($"HotUpdateManifest JSON is invalid: {key}");
            }

            return manifest;
        }

        private static async UniTask<Dictionary<string, byte[]>> LoadMetadataBytesAsync(IResLoader loader,
            IReadOnlyList<string> metadataKeys, CancellationToken cancellationToken)
        {
            Dictionary<string, byte[]> result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (metadataKeys == null)
            {
                return result;
            }

            var normalizedKeys = new List<string>(metadataKeys.Count);
            var loadTasks = new List<UniTask<byte[]>>(metadataKeys.Count);

            for (int i = 0; i < metadataKeys.Count; i++)
            {
                string key = metadataKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                key = key.Trim();
                normalizedKeys.Add(key);
                loadTasks.Add(LoadMetadataAssetBytesAsync(loader, key, cancellationToken));
            }

            if (loadTasks.Count == 0)
            {
                return result;
            }

            byte[][] loadedBytes = await UniTask.WhenAll(loadTasks);
            for (int i = 0; i < normalizedKeys.Count; i++)
            {
                result[normalizedKeys[i]] = loadedBytes[i];
            }

            return result;
        }

        private static async UniTask<byte[]> LoadHotUpdateAssemblyBytesAsync(
            IResLoader loader,
            string assemblyKey,
            CancellationToken cancellationToken)
        {
            string key = string.IsNullOrWhiteSpace(assemblyKey) ? string.Empty : assemblyKey.Trim();
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("Hot update dll.bytes key is empty.");
            }

            TextAsset hotUpdateAsset = await loader.LoadAsync<TextAsset>(key, cancellationToken);
            if (hotUpdateAsset == null || hotUpdateAsset.bytes == null || hotUpdateAsset.bytes.Length == 0)
            {
                throw new InvalidOperationException($"Hot update dll.bytes load failed: {key}");
            }

            return hotUpdateAsset.bytes;
        }

        private static async UniTask<byte[]> LoadMetadataAssetBytesAsync(
            IResLoader loader,
            string key,
            CancellationToken cancellationToken)
        {
            TextAsset metadataAsset = await loader.LoadAsync<TextAsset>(key, cancellationToken);
            if (metadataAsset == null || metadataAsset.bytes == null || metadataAsset.bytes.Length == 0)
            {
                throw new InvalidOperationException($"AOT metadata dll.bytes load failed: {key}");
            }

            return metadataAsset.bytes;
        }

        private static bool VerifySha256(byte[] bytes, string expectedSha256, bool strictProduction, out string actualSha256)
        {
            actualSha256 = ComputeSha256(bytes);
            if (string.IsNullOrWhiteSpace(expectedSha256))
            {
                if (!strictProduction)
                {
                    LogKit.LogWarning(
                        "[HybridCLRHook] HotUpdateAssemblySha256 is empty. Development build skips SHA256 verification, but release (strict) builds will block startup. Re-export dll.bytes to embed the SHA256.");
                }

                return !strictProduction;
            }

            string normalizedExpected = expectedSha256.Trim().Replace("-", string.Empty);
            return string.Equals(normalizedExpected, actualSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        private static HybridCLRUpdateResult Fail(string error)
        {
            State = HybridCLRUpdateState.Failed;
            LastError = string.IsNullOrWhiteSpace(error) ? "Unknown HybridCLR code update error." : error;
            LogKit.LogError($"[HybridCLRRunner] {LastError}");
            return new HybridCLRUpdateResult
            {
                Success = false,
                State = State,
                Error = LastError,
                LoadedAssemblyFullName = HybridCLRHook.LoadedAssemblyFullName
            };
        }
    }
}
