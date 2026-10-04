using System;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Structured rollback outcome. PointerChanged distinguishes preflight failures from post-switch failures.</summary>
    public sealed class HotUpdateRollbackResult
    {
        internal HotUpdateRollbackResult(bool success, bool pointerChanged, string previousReleaseId,
            string restoredReleaseId, string error, Exception exception)
        {
            Success = success;
            PointerChanged = pointerChanged;
            PreviousReleaseId = previousReleaseId ?? string.Empty;
            RestoredReleaseId = restoredReleaseId ?? string.Empty;
            Error = error ?? string.Empty;
            Exception = exception;
        }

        public bool Success { get; }
        public bool PointerChanged { get; }
        public string PreviousReleaseId { get; }
        public string RestoredReleaseId { get; }
        public string Error { get; }
        public Exception Exception { get; }
    }

    /// <summary>
    /// Restores an existing immutable release by checking recorded remote hashes, changing only PackageVersion,
    /// and verifying the restored Manifest/Bundle GET plus Range. It never uploads historic DLLs or bundles.
    /// </summary>
    public sealed class HotUpdateReleaseRollbackService
    {
        private readonly HotUpdateReleaseHistoryRepository _history;
        private readonly IHotUpdatePublishTarget _target;
        private readonly IHotUpdateHistoricalReleaseRemoteVerifier _remoteVerifier;

        public HotUpdateReleaseRollbackService(HotUpdateReleaseHistoryRepository history,
            IHotUpdatePublishTarget target, IHotUpdateHistoricalReleaseRemoteVerifier remoteVerifier)
        {
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _remoteVerifier = remoteVerifier ?? throw new ArgumentNullException(nameof(remoteVerifier));
        }

        public async Task<HotUpdateRollbackResult> RollbackAsync(string currentActiveReleaseId,
            string restoredReleaseId, CancellationToken cancellationToken = default(CancellationToken))
        {
            bool pointerChanged = false;
            HotUpdateReleaseRecord current = null;
            HotUpdateReleaseRecord restored = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                current = _history.Load(currentActiveReleaseId);
                restored = _history.Load(restoredReleaseId);
                ValidatePair(current, restored);

                // Validate all immutable bytes still exist before changing the mutable pointer.
                for (int index = 0; index < restored.Files.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    HotUpdateReleaseFileRecord expected = restored.Files[index];
                    HotUpdatePublishTargetFileInfo remote = await _target.GetInfoAsync(expected.RelativePath, cancellationToken);
                    if (remote == null)
                        return Failed(false, current, restored, $"Remote rollback file '{expected.RelativePath}' is missing.");
                    if (remote.Length != expected.Length ||
                        !string.Equals(remote.Sha256, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                        return Failed(false, current, restored,
                            $"Remote rollback file '{expected.RelativePath}' failed its recorded length/SHA256 check.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                await _target.PublishVersionAsync(new HotUpdateVersionPublishRequest
                {
                    PointerRelativePath = restored.ManifestFiles[3],
                    PackageVersion = restored.PackageVersion,
                    ExpectedCurrentPackageVersion = current.PackageVersion
                }, cancellationToken);
                pointerChanged = true;

                try
                {
                    await _remoteVerifier.VerifyRollbackAsync(restored, cancellationToken);
                }
                catch (Exception exception)
                {
                    // Preserve an explicit event because the pointer has changed even though runtime verification failed.
                    _history.RecordRollbackVerificationFailure(current.ReleaseId, restored.ReleaseId, exception.Message);
                    return Failed(true, current, restored,
                        $"Version pointer changed to '{restored.PackageVersion}', but remote runtime verification failed: {exception.Message}", exception);
                }

                _history.CompleteRollback(current.ReleaseId, restored.ReleaseId);
                return new HotUpdateRollbackResult(true, true, current.ReleaseId, restored.ReleaseId, string.Empty, null);
            }
            catch (OperationCanceledException exception)
            {
                if (pointerChanged && current != null && restored != null)
                    _history.RecordRollbackVerificationFailure(current.ReleaseId, restored.ReleaseId,
                        "Rollback was cancelled after the PackageVersion pointer changed.");
                return Failed(pointerChanged, current, restored, "Rollback was cancelled.", exception);
            }
            catch (Exception exception)
            {
                return Failed(pointerChanged, current, restored,
                    $"Rollback failed{(pointerChanged ? " after changing the version pointer" : " before changing the version pointer")}: {exception.Message}", exception);
            }
        }

        private static void ValidatePair(HotUpdateReleaseRecord current, HotUpdateReleaseRecord restored)
        {
            if (current.Status != HotUpdateReleaseRecordStatus.Active)
                throw new InvalidOperationException($"Current release '{current.ReleaseId}' is not Active in release history.");
            if (restored.Status == HotUpdateReleaseRecordStatus.Active)
                throw new InvalidOperationException($"Release '{restored.ReleaseId}' is already Active.");
            if (current.Platform != restored.Platform ||
                !string.Equals(current.Environment, restored.Environment, StringComparison.Ordinal) ||
                !string.Equals(current.PackageName, restored.PackageName, StringComparison.Ordinal))
                throw new InvalidOperationException("Rollback target must match current package, platform and environment.");
            if (string.Equals(current.PackageVersion, restored.PackageVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Current and restored package versions are identical.");
            if (current.ManifestFiles == null || current.ManifestFiles.Length < 4 ||
                restored.ManifestFiles == null || restored.ManifestFiles.Length < 4 ||
                !string.Equals(current.ManifestFiles[3], restored.ManifestFiles[3], StringComparison.Ordinal))
                throw new InvalidOperationException("Current and historical releases do not share a safe PackageVersion pointer path.");
        }

        private static HotUpdateRollbackResult Failed(bool pointerChanged, HotUpdateReleaseRecord current,
            HotUpdateReleaseRecord restored, string error, Exception exception = null)
        {
            return new HotUpdateRollbackResult(false, pointerChanged, current?.ReleaseId, restored?.ReleaseId, error, exception);
        }
    }
}
