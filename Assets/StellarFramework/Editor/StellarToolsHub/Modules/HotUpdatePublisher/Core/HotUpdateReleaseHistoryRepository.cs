using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Durable project-local release history; it never stores credentials.</summary>
    public sealed class HotUpdateReleaseHistoryRepository
    {
        public const string DefaultRelativeRoot = "BuildArtifacts/HotUpdate/ReleaseHistory";
        private readonly string _root;

        public HotUpdateReleaseHistoryRepository(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Release history root is required.", nameof(root));
            _root = Path.GetFullPath(root);
        }

        public static HotUpdateReleaseHistoryRepository CreateForProject(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Project root is required.", nameof(projectRoot));
            return new HotUpdateReleaseHistoryRepository(Path.Combine(projectRoot, DefaultRelativeRoot));
        }

        /// <summary>Creates a new active record and supersedes older active records for the same package/environment/platform.</summary>
        public void SaveActivated(HotUpdateReleaseRecord record)
        {
            ValidateRecord(record);
            string destination = GetRecordPath(record.ReleaseId);
            if (File.Exists(destination)) throw new IOException($"Release history already contains ReleaseId '{record.ReleaseId}'. Records are not overwritten on create.");

            IReadOnlyList<HotUpdateReleaseRecord> records = List();
            record.Status = HotUpdateReleaseRecordStatus.Active;
            if (record.CreatedAtUtc == default(DateTime)) record.CreatedAtUtc = DateTime.UtcNow;
            AtomicWrite(destination, JsonUtility.ToJson(record, true));

            for (int index = 0; index < records.Count; index++)
            {
                HotUpdateReleaseRecord existing = records[index];
                if (existing.Status == HotUpdateReleaseRecordStatus.Active &&
                    existing.Platform == record.Platform &&
                    string.Equals(existing.Environment, record.Environment, StringComparison.Ordinal) &&
                    string.Equals(existing.PackageName, record.PackageName, StringComparison.Ordinal))
                {
                    existing.Status = HotUpdateReleaseRecordStatus.Superseded;
                    AtomicWrite(GetRecordPath(existing.ReleaseId), JsonUtility.ToJson(existing, true));
                }
            }

            AppendEvent(new HotUpdateReleaseHistoryEvent
            {
                ReleaseId = record.ReleaseId,
                EventType = "Activated",
                ToVersion = record.PackageVersion,
                Diagnostic = "Release version pointer was published and history was finalized.",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        public HotUpdateReleaseRecord Load(string releaseId)
        {
            string path = GetRecordPath(releaseId);
            if (!File.Exists(path)) throw new FileNotFoundException($"ReleaseId '{releaseId}' was not found in release history.", path);
            HotUpdateReleaseRecord record = JsonUtility.FromJson<HotUpdateReleaseRecord>(File.ReadAllText(path));
            ValidateRecord(record);
            return record;
        }

        public IReadOnlyList<HotUpdateReleaseRecord> List()
        {
            if (!Directory.Exists(_root)) return Array.Empty<HotUpdateReleaseRecord>();
            string[] paths = Directory.GetFiles(_root, "release-*.json", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            var records = new List<HotUpdateReleaseRecord>(paths.Length);
            for (int index = 0; index < paths.Length; index++)
            {
                HotUpdateReleaseRecord record = JsonUtility.FromJson<HotUpdateReleaseRecord>(File.ReadAllText(paths[index]));
                ValidateRecord(record);
                records.Add(record);
            }
            records.Sort((left, right) => right.CreatedAtUtc.CompareTo(left.CreatedAtUtc));
            return records.AsReadOnly();
        }

        public void SetStatus(string releaseId, HotUpdateReleaseRecordStatus status, HotUpdateReleaseHistoryEvent historyEvent)
        {
            HotUpdateReleaseRecord record = Load(releaseId);
            HotUpdateReleaseRecordStatus previous = record.Status;
            record.Status = status;
            AtomicWrite(GetRecordPath(releaseId), JsonUtility.ToJson(record, true));
            if (historyEvent == null) historyEvent = new HotUpdateReleaseHistoryEvent();
            historyEvent.ReleaseId = releaseId;
            historyEvent.FromVersion = historyEvent.FromVersion ?? record.PackageVersion;
            historyEvent.ToVersion = historyEvent.ToVersion ?? string.Empty;
            historyEvent.EventType = historyEvent.EventType ?? (previous + "To" + status);
            if (historyEvent.CreatedAtUtc == default(DateTime)) historyEvent.CreatedAtUtc = DateTime.UtcNow;
            AppendEvent(historyEvent);
        }

        public void CompleteRollback(string previousActiveReleaseId, string restoredReleaseId)
        {
            HotUpdateReleaseRecord previous = Load(previousActiveReleaseId);
            HotUpdateReleaseRecord restored = Load(restoredReleaseId);
            if (previous.Status != HotUpdateReleaseRecordStatus.Active)
                throw new InvalidOperationException($"Current release '{previousActiveReleaseId}' is not marked Active.");
            if (previous.Platform != restored.Platform ||
                !string.Equals(previous.Environment, restored.Environment, StringComparison.Ordinal) ||
                !string.Equals(previous.PackageName, restored.PackageName, StringComparison.Ordinal))
                throw new InvalidOperationException("Rollback history records must target the same package, platform and environment.");

            restored.Status = HotUpdateReleaseRecordStatus.Active;
            previous.Status = HotUpdateReleaseRecordStatus.RolledBack;
            AtomicWrite(GetRecordPath(restored.ReleaseId), JsonUtility.ToJson(restored, true));
            AtomicWrite(GetRecordPath(previous.ReleaseId), JsonUtility.ToJson(previous, true));
            AppendEvent(new HotUpdateReleaseHistoryEvent
            {
                ReleaseId = restored.ReleaseId,
                EventType = "Rollback",
                FromVersion = previous.PackageVersion,
                ToVersion = restored.PackageVersion,
                Diagnostic = $"Release '{previous.ReleaseId}' was rolled back to '{restored.ReleaseId}'.",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        public void RecordRollbackVerificationFailure(string previousReleaseId, string restoredReleaseId, string diagnostic)
        {
            HotUpdateReleaseRecord previous = Load(previousReleaseId);
            HotUpdateReleaseRecord restored = Load(restoredReleaseId);
            if (previous.Status != HotUpdateReleaseRecordStatus.Active)
                throw new InvalidOperationException($"Current release '{previousReleaseId}' is not marked Active.");
            if (previous.Platform != restored.Platform ||
                !string.Equals(previous.Environment, restored.Environment, StringComparison.Ordinal) ||
                !string.Equals(previous.PackageName, restored.PackageName, StringComparison.Ordinal))
                throw new InvalidOperationException("Rollback history records must target the same package, platform and environment.");

            // The pointer write already succeeded. Reflect that state, but mark the target separately because
            // its Manifest/Bundle GET or Range verification did not prove that clients can consume it.
            previous.Status = HotUpdateReleaseRecordStatus.RolledBack;
            restored.Status = HotUpdateReleaseRecordStatus.RollbackUnverified;
            AtomicWrite(GetRecordPath(previous.ReleaseId), JsonUtility.ToJson(previous, true));
            AtomicWrite(GetRecordPath(restored.ReleaseId), JsonUtility.ToJson(restored, true));
            AppendEvent(new HotUpdateReleaseHistoryEvent
            {
                ReleaseId = restoredReleaseId,
                EventType = "RollbackVerificationFailed",
                FromVersion = Load(previousReleaseId).PackageVersion,
                ToVersion = Load(restoredReleaseId).PackageVersion,
                Diagnostic = diagnostic ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        public void AppendEvent(HotUpdateReleaseHistoryEvent historyEvent)
        {
            if (historyEvent == null) throw new ArgumentNullException(nameof(historyEvent));
            if (string.IsNullOrWhiteSpace(historyEvent.ReleaseId)) throw new ArgumentException("History event ReleaseId is required.", nameof(historyEvent));
            historyEvent.EventId = Guid.NewGuid().ToString("N");
            if (historyEvent.CreatedAtUtc == default(DateTime)) historyEvent.CreatedAtUtc = DateTime.UtcNow;
            string directory = Path.Combine(_root, "Events");
            Directory.CreateDirectory(directory);
            AtomicWrite(Path.Combine(directory, historyEvent.EventId + ".json"), JsonUtility.ToJson(historyEvent, true));
        }

        private string GetRecordPath(string releaseId)
        {
            if (string.IsNullOrWhiteSpace(releaseId)) throw new ArgumentException("ReleaseId is required.", nameof(releaseId));
            for (int index = 0; index < releaseId.Length; index++)
            {
                char value = releaseId[index];
                if (!char.IsLetterOrDigit(value) && value != '-' && value != '_')
                    throw new ArgumentException("ReleaseId may contain only letters, digits, '-' and '_'.", nameof(releaseId));
            }
            return Path.Combine(_root, "release-" + releaseId + ".json");
        }

        private static void ValidateRecord(HotUpdateReleaseRecord record)
        {
            if (record == null) throw new InvalidDataException("Release history record could not be parsed.");
            if (string.IsNullOrWhiteSpace(record.ReleaseId) || string.IsNullOrWhiteSpace(record.PackageVersion) ||
                string.IsNullOrWhiteSpace(record.PackageName) || string.IsNullOrWhiteSpace(record.Environment) ||
                string.IsNullOrWhiteSpace(record.BaseAppVersion) || string.IsNullOrWhiteSpace(record.GateResult) ||
                !IsSha256(record.HotUpdateDllSha256) || record.ChangeClassification == null)
                throw new InvalidDataException("Release history record is missing required identity, gate, DLL SHA256 or change classification data.");
            if (record.Files == null) record.Files = Array.Empty<HotUpdateReleaseFileRecord>();
            if (record.Files.Length == 0) throw new InvalidDataException("Release history record has no immutable file evidence for rollback validation.");
            if (record.ManifestFiles == null || record.ManifestFiles.Length < 4)
                throw new InvalidDataException("Release history record is missing the YooAsset manifest/version pointer filenames required for rollback.");
            for (int index = 0; index < record.ManifestFiles.Length; index++)
                if (!IsSafeRelativePath(record.ManifestFiles[index]))
                    throw new InvalidDataException($"Release history ManifestFiles entry at index {index} is unsafe.");

            bool hasJsonManifest = false;
            for (int index = 0; index < record.Files.Length; index++)
            {
                HotUpdateReleaseFileRecord file = record.Files[index];
                if (file == null || !IsSafeRelativePath(file.RelativePath) || file.Length < 0 || !IsSha256(file.Sha256))
                    throw new InvalidDataException($"Release history file evidence at index {index} is invalid.");
                if (string.Equals(file.RelativePath, record.ManifestFiles[1], StringComparison.OrdinalIgnoreCase)) hasJsonManifest = true;
            }
            if (!hasJsonManifest) throw new InvalidDataException("Release history file evidence does not contain its JSON Manifest.");
        }

        private static bool IsSafeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal)) return false;
            string[] segments = normalized.Split('/');
            for (int index = 0; index < segments.Length; index++)
                if (string.IsNullOrWhiteSpace(segments[index]) || segments[index] == "." || segments[index] == "..") return false;
            return true;
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            for (int index = 0; index < value.Length; index++)
                if (!Uri.IsHexDigit(value[index])) return false;
            return true;
        }

        private static void AtomicWrite(string path, string contents)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, contents, new System.Text.UTF8Encoding(false));
            try
            {
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    /// <summary>Persists the release record only after remote version publication succeeded.</summary>
    public sealed class HotUpdateReleaseHistoryFinalizeStageHandler : IHotUpdatePublishStageHandler
    {
        private readonly HotUpdateReleaseHistoryRepository _repository;
        public HotUpdatePublishStage Stage => HotUpdatePublishStage.Finalize;

        public HotUpdateReleaseHistoryFinalizeStageHandler(HotUpdateReleaseHistoryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public Task<HotUpdatePublishStepResult> ExecuteAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            if (context.ReleaseGateReport == null || !context.ReleaseGateReport.Passed)
                return Task.FromResult(Failed("Release history cannot finalize without passing Release Gate evidence."));
            if (!context.RemotePublishVerificationCompleted)
                return Task.FromResult(Failed("Release history cannot finalize before remote verification and version publication."));
            if (context.ChangeClassification == null || context.YooAssetBuildOutput == null || context.PublishFiles == null || context.PublishFiles.Count == 0)
                return Task.FromResult(Failed("Release history requires change classification and validated build file evidence."));

            var changeSummary = new HotUpdateReleaseChangeClassification
            {
                GreenCount = context.ChangeClassification.GreenCount,
                YellowCount = context.ChangeClassification.YellowCount,
                RedCount = context.ChangeClassification.RedCount,
                DependencyViolationCount = context.ChangeClassification.DependencyViolations.Count,
                Safety = context.ChangeClassification.RedCount > 0 || context.ChangeClassification.DependencyViolations.Count > 0
                    ? "RED"
                    : context.ChangeClassification.YellowCount > 0 ? "YELLOW" : "GREEN"
            };
            var files = new HotUpdateReleaseFileRecord[context.PublishFiles.Count];
            for (int index = 0; index < context.PublishFiles.Count; index++)
            {
                HotUpdatePublishFile file = context.PublishFiles[index];
                files[index] = new HotUpdateReleaseFileRecord
                {
                    RelativePath = file.RelativePath,
                    Length = file.Length,
                    Sha256 = file.Sha256
                };
            }

            var record = new HotUpdateReleaseRecord
            {
                ReleaseId = context.ReleaseId,
                PackageName = context.PackageName,
                PackageVersion = context.PackageVersion,
                BaseAppVersion = context.BaseAppVersion,
                Platform = context.Platform,
                Environment = context.Environment,
                ReleaseNotes = context.ReleaseNotes,
                GitCommit = context.GitCommit,
                GitBranch = context.GitBranch,
                GitDirty = context.GitDirty,
                HotUpdateDllSha256 = context.HybridCLRBuildOutput?.HotUpdateDllSha256 ?? string.Empty,
                BundleCount = context.YooAssetBuildOutput.BundleCount,
                ChangeClassification = changeSummary,
                Files = files,
                ManifestFiles = context.YooAssetBuildOutput.ManifestFiles == null
                    ? Array.Empty<string>()
                    : (string[])context.YooAssetBuildOutput.ManifestFiles.Clone(),
                TotalBytes = context.YooAssetBuildOutput.TotalBytes,
                GateResult = context.ReleaseGateReport.RequiredLevel == HotUpdateReleaseGateLevel.Full ? "FAST_PASS;FULL_PASS" : "FAST_PASS",
                ServerRoot = context.ServerRoot,
                CreatedAtUtc = DateTime.UtcNow,
                Status = HotUpdateReleaseRecordStatus.Active
            };
            _repository.SaveActivated(record);
            context.ReleaseRecord = record;
            return Task.FromResult(HotUpdatePublishStepResult.Succeeded());
        }

        private static HotUpdatePublishStepResult Failed(string error) =>
            HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.StageFailed, error);
    }
}
