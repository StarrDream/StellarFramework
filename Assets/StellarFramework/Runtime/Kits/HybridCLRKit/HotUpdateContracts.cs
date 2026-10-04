using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace StellarFramework.HybridCLR
{
    /// <summary>
    /// HybridCLR code-update runner state.
    /// Resource package/version/download work is intentionally outside this state machine.
    /// </summary>
    public enum HybridCLRUpdateState
    {
        None,
        LoadingManifest,
        LoadingBytes,
        LoadingMetadata,
        LoadingAssembly,
        EnteredHotUpdate,
        Failed
    }

    /// <summary>
    /// Result of one HybridCLR code-update startup run.
    /// </summary>
    public struct HybridCLRUpdateResult
    {
        public bool Success;
        public HybridCLRUpdateState State;
        public string Error;
        public string LoadedAssemblyFullName;
        public HotUpdateManifest Manifest;
        public string ManifestSource;
    }

    /// <summary>
    /// Optional HybridCLR runtime implementation boundary.
    /// The core facade stays available even when the HybridCLR package is not exported.
    /// </summary>
    public interface IHybridCLRCodeUpdateStrategy
    {
        UniTask<HybridCLRUpdateResult> RunAsync(
            HotUpdateSettings settings = null,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);
    }

    internal sealed class UnavailableHybridCLRCodeUpdateStrategy : IHybridCLRCodeUpdateStrategy
    {
        private const string UnavailableMessage =
            "HybridCLR code update is unavailable. Install the StellarFramework HybridCLR adapter.";

        public UniTask<HybridCLRUpdateResult> RunAsync(
            HotUpdateSettings settings = null,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(new HybridCLRUpdateResult
            {
                Success = false,
                State = HybridCLRUpdateState.Failed,
                Error = UnavailableMessage
            });
        }
    }

    /// <summary>
    /// HybridCLR code-update facade.
    /// </summary>
    /// <remarks>
    /// HybridCLRKit does not initialize Addressables/YooAsset, check content versions,
    /// download bundles, or clear resource caches. Content update must finish before this
    /// facade runs. The configured ResKit backend only supplies dll.bytes/TextAsset data.
    /// </remarks>
    public static class HybridCLRKit
    {
        private static IHybridCLRCodeUpdateStrategy _strategy =
            new UnavailableHybridCLRCodeUpdateStrategy();
        private static HotUpdateSettings _settings;

        public static IHybridCLRCodeUpdateStrategy Strategy => _strategy;

        public static HotUpdateSettings Settings =>
            _settings ?? HotUpdateSettings.LoadOrCreateDefault();

        public static void Configure(
            IHybridCLRCodeUpdateStrategy strategy = null,
            HotUpdateSettings settings = null)
        {
            if (strategy != null)
            {
                _strategy = strategy;
            }

            if (settings != null)
            {
                _settings = settings;
            }
        }

        public static void SetStrategy(IHybridCLRCodeUpdateStrategy strategy)
        {
            if (strategy == null)
            {
                LogKit.LogError("[HybridCLRKit] SetStrategy failed: strategy is null.");
                return;
            }

            _strategy = strategy;
        }

        public static UniTask<HybridCLRUpdateResult> RunAsync(
            HotUpdateSettings settings = null,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            HotUpdateSettings resolvedSettings = settings ?? Settings;
            if (resolvedSettings == null)
            {
                return UniTask.FromResult(Fail("HotUpdateSettings is null."));
            }

            HotUpdateSettingsValidationReport validation = resolvedSettings.Validate();
            if (!validation.IsValid)
            {
                return UniTask.FromResult(Fail(
                    "HotUpdateSettings validation failed: " +
                    string.Join(" | ", validation.Errors)));
            }

            return _strategy.RunAsync(resolvedSettings, progress, cancellationToken);
        }

        private static HybridCLRUpdateResult Fail(string error)
        {
            LogKit.LogError($"[HybridCLRKit] {error}");
            return new HybridCLRUpdateResult
            {
                Success = false,
                State = HybridCLRUpdateState.Failed,
                Error = error
            };
        }
    }
}
