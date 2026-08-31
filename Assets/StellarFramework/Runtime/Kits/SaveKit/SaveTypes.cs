using System;
using System.Collections.Generic;
using System.Globalization;

namespace StellarFramework
{
    public enum SaveErrorCode
    {
        None = 0,
        NotInitialized,
        InvalidSlotId,
        SlotNotFound,
        Busy,
        Cancelled,
        StorageError,
        ContainerCorrupted,
        UnsupportedContainerVersion,
        ChecksumMismatch,
        InvalidManifest,
        SectionMissing,
        SectionCorrupted,
        SerializerMissing,
        UnsupportedSectionVersion,
        MigrationMissing,
        MigrationFailed,
        ValidationFailed,
        RestoreDependencyCycle,
        RestoreFailed,
        InvalidConfiguration,
        UnknownError
    }

    public enum SaveOperationStatus
    {
        Success = 0,
        SuccessWithBackupRecovery,
        Failed
    }

    public enum MissingSectionPolicy
    {
        Fail = 0,
        UseDefault,
        Ignore
    }

    public enum UnknownSectionPolicy
    {
        Preserve = 0,
        Ignore,
        Fail
    }

    public enum SaveStorageFileKind
    {
        Current = 0,
        Backup,
        Temporary
    }

    public readonly struct SaveSlotId : IEquatable<SaveSlotId>
    {
        public const int MaxLength = 64;
        public string Value { get; }

        private SaveSlotId(string value)
        {
            Value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static SaveSlotId From(string value)
        {
            SaveSlotId result;
            string error;
            if (!TryCreate(value, out result, out error))
            {
                throw new ArgumentException(error, nameof(value));
            }

            return result;
        }

        public static bool TryCreate(string value, out SaveSlotId result, out string error)
        {
            result = default(SaveSlotId);
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Slot ID 不能为空。";
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.Length > MaxLength)
            {
                error = $"Slot ID 长度不能超过 {MaxLength}。";
                return false;
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                {
                    error = "Slot ID 只能包含字母、数字、下划线、短横线和点。";
                    return false;
                }
            }

            result = new SaveSlotId(trimmed);
            return true;
        }

        public bool Equals(SaveSlotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveSlotId && Equals((SaveSlotId)obj);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SaveSlotId left, SaveSlotId right) => left.Equals(right);
        public static bool operator !=(SaveSlotId left, SaveSlotId right) => !left.Equals(right);
    }

    public readonly struct SaveSectionId : IEquatable<SaveSectionId>
    {
        public const int MaxLength = 128;
        public string Value { get; }

        private SaveSectionId(string value)
        {
            Value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static SaveSectionId From(string value)
        {
            SaveSectionId result;
            string error;
            if (!TryCreate(value, out result, out error))
            {
                throw new ArgumentException(error, nameof(value));
            }

            return result;
        }

        public static bool TryCreate(string value, out SaveSectionId result, out string error)
        {
            result = default(SaveSectionId);
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Section ID 不能为空。";
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.Length > MaxLength)
            {
                error = $"Section ID 长度不能超过 {MaxLength}。";
                return false;
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                {
                    error = "Section ID 只能包含字母、数字、下划线、短横线和点。";
                    return false;
                }
            }

            result = new SaveSectionId(trimmed);
            return true;
        }

        public bool Equals(SaveSectionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveSectionId && Equals((SaveSectionId)obj);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SaveSectionId left, SaveSectionId right) => left.Equals(right);
        public static bool operator !=(SaveSectionId left, SaveSectionId right) => !left.Equals(right);
    }

    public sealed class SaveKitOptions
    {
        public const int CurrentContainerVersion = 1;
        public string ApplicationVersion { get; set; } = "0.0.0";
        public int MaxSectionCount { get; set; } = 256;
        public int MaxStringBytes { get; set; } = 4096;
        public long MaxPayloadBytes { get; set; } = 256L * 1024L * 1024L;
        public int MaxCustomMetadataCount { get; set; } = 64;
        public MissingSectionPolicy MissingSectionPolicy { get; set; } = MissingSectionPolicy.Fail;
        public UnknownSectionPolicy UnknownSectionPolicy { get; set; } = UnknownSectionPolicy.Preserve;
        public bool AutoRecoverBackup { get; set; } = true;
        public bool ValidateAfterCapture { get; set; } = true;

        public SaveKitOptions Clone()
        {
            return new SaveKitOptions
            {
                ApplicationVersion = ApplicationVersion,
                MaxSectionCount = MaxSectionCount,
                MaxStringBytes = MaxStringBytes,
                MaxPayloadBytes = MaxPayloadBytes,
                MaxCustomMetadataCount = MaxCustomMetadataCount,
                MissingSectionPolicy = MissingSectionPolicy,
                UnknownSectionPolicy = UnknownSectionPolicy,
                AutoRecoverBackup = AutoRecoverBackup,
                ValidateAfterCapture = ValidateAfterCapture
            };
        }

        internal bool Validate(out string error)
        {
            error = null;
            if (MaxSectionCount < 1 || MaxSectionCount > 4096)
            {
                error = "MaxSectionCount 必须在 1 到 4096 之间。";
                return false;
            }

            if (MaxStringBytes < 16 || MaxStringBytes > 1024 * 1024)
            {
                error = "MaxStringBytes 必须在 16 到 1048576 之间。";
                return false;
            }

            if (MaxPayloadBytes < 1024 || MaxPayloadBytes > 2L * 1024L * 1024L * 1024L)
            {
                error = "MaxPayloadBytes 超出允许范围。";
                return false;
            }

            if (MaxCustomMetadataCount < 0 || MaxCustomMetadataCount > 1024)
            {
                error = "MaxCustomMetadataCount 超出允许范围。";
                return false;
            }

            return true;
        }
    }

    public sealed class SaveMetadata
    {
        public SaveSlotId SlotId { get; set; }
        public long Revision { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string ApplicationVersion { get; set; }
        public int ContainerVersion { get; set; }
        public IDictionary<string, string> CustomMetadata { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public SaveMetadata Clone()
        {
            var clone = new SaveMetadata
            {
                SlotId = SlotId,
                Revision = Revision,
                CreatedUtc = CreatedUtc,
                UpdatedUtc = UpdatedUtc,
                ApplicationVersion = ApplicationVersion,
                ContainerVersion = ContainerVersion
            };
            foreach (KeyValuePair<string, string> pair in CustomMetadata)
            {
                clone.CustomMetadata[pair.Key] = pair.Value;
            }

            return clone;
        }
    }

    public sealed class SaveSlotInfo
    {
        public SaveMetadata Metadata { get; internal set; }
        public long FileSizeBytes { get; internal set; }
        public bool HasBackup { get; internal set; }
        public bool IsHealthy { get; internal set; }
        public string HealthMessage { get; internal set; }
    }

    public sealed class SaveSectionDescriptor
    {
        public SaveSectionId Id { get; set; }
        public int SchemaVersion { get; set; }
        public string SerializerId { get; set; }
        public long PayloadOffset { get; set; }
        public long PayloadLength { get; set; }
        public ulong Checksum { get; set; }
        public byte Flags { get; set; }
    }

    public sealed class SaveSectionEntry
    {
        public SaveSectionDescriptor Descriptor { get; set; }
        public byte[] Payload { get; set; }
        public bool IsUnknown { get; set; }
    }

    public sealed class SaveSnapshot
    {
        public SaveMetadata Metadata { get; internal set; }
        public IReadOnlyList<SaveSectionEntry> Sections { get; internal set; }
    }

    public sealed class SaveValidationIssue
    {
        public string Code { get; }
        public string Message { get; }

        public SaveValidationIssue(string code, string message)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString() => string.IsNullOrEmpty(Code) ? Message : $"{Code}: {Message}";
    }

    public sealed class SaveValidationResult
    {
        private readonly List<SaveValidationIssue> _issues = new List<SaveValidationIssue>();
        public bool IsValid => _issues.Count == 0;
        public IReadOnlyList<SaveValidationIssue> Issues => _issues;

        public static SaveValidationResult Valid() => new SaveValidationResult();

        public static SaveValidationResult Invalid(string code, string message)
        {
            var result = new SaveValidationResult();
            result.Add(code, message);
            return result;
        }

        public void Add(string code, string message)
        {
            _issues.Add(new SaveValidationIssue(code, message));
        }

        public override string ToString() => IsValid ? "Valid" : string.Join(" | ", _issues);
    }

    public sealed class SaveResult
    {
        public SaveOperationStatus Status { get; internal set; }
        public SaveErrorCode ErrorCode { get; internal set; }
        public string ErrorMessage { get; internal set; }
        public SaveSlotId SlotId { get; internal set; }
        public long Revision { get; internal set; }
        public bool UsedBackup { get; internal set; }
        public SaveOperationDiagnostics Diagnostics { get; internal set; }
        public bool IsSuccess => Status == SaveOperationStatus.Success || Status == SaveOperationStatus.SuccessWithBackupRecovery;

        internal static SaveResult Success(SaveSlotId slot, long revision, SaveOperationDiagnostics diagnostics, bool usedBackup = false)
        {
            return new SaveResult
            {
                Status = usedBackup ? SaveOperationStatus.SuccessWithBackupRecovery : SaveOperationStatus.Success,
                ErrorCode = SaveErrorCode.None,
                SlotId = slot,
                Revision = revision,
                UsedBackup = usedBackup,
                Diagnostics = diagnostics
            };
        }

        internal static SaveResult Failure(SaveSlotId slot, SaveErrorCode code, string message, SaveOperationDiagnostics diagnostics)
        {
            return new SaveResult
            {
                Status = SaveOperationStatus.Failed,
                ErrorCode = code,
                ErrorMessage = message,
                SlotId = slot,
                Diagnostics = diagnostics
            };
        }
    }

    public sealed class SaveCaptureContext
    {
        public SaveSlotId SlotId { get; }
        public DateTime UtcNow { get; }
        public SaveKitOptions Options { get; }

        internal SaveCaptureContext(SaveSlotId slotId, DateTime utcNow, SaveKitOptions options)
        {
            SlotId = slotId;
            UtcNow = utcNow;
            Options = options;
        }
    }

    /// <summary>
    /// Optional application lifecycle callbacks. SaveKit never references TimeKit or
    /// drives simulation; the host decides what to pause/resume around a snapshot.
    /// </summary>
    public interface ISaveLifecycleHooks
    {
        void BeforeCapture(SaveCaptureContext context);
        void AfterCapture(SaveCaptureContext context);
        void BeforeRestore(SaveRestoreContext context);
        void AfterRestore(SaveRestoreContext context);
    }

    public sealed class SaveRestoreContext
    {
        public SaveSlotId SlotId { get; }
        public SaveMetadata Metadata { get; }
        public SaveKitOptions Options { get; }

        internal SaveRestoreContext(SaveSlotId slotId, SaveMetadata metadata, SaveKitOptions options)
        {
            SlotId = slotId;
            Metadata = metadata;
            Options = options;
        }
    }

    public sealed class SaveValidationContext
    {
        public SaveSlotId SlotId { get; }
        public SaveMetadata Metadata { get; }
        public SaveKitOptions Options { get; }

        internal SaveValidationContext(SaveSlotId slotId, SaveMetadata metadata, SaveKitOptions options)
        {
            SlotId = slotId;
            Metadata = metadata;
            Options = options;
        }
    }

    public sealed class SaveMigrationContext
    {
        public SaveSlotId SlotId { get; }
        public SaveSectionId SectionId { get; }
        public int FromVersion { get; }
        public int ToVersion { get; }

        internal SaveMigrationContext(SaveSlotId slotId, SaveSectionId sectionId, int fromVersion, int toVersion)
        {
            SlotId = slotId;
            SectionId = sectionId;
            FromVersion = fromVersion;
            ToVersion = toVersion;
        }
    }
}
