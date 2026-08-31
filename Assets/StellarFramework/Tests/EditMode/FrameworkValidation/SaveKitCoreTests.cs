using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using StellarFramework.SaveKitAdapters.NewtonsoftJson;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class SaveKitCoreTests
    {
        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStorage();
            SaveKit.Initialize(builder => builder
                .UseStorage(_storage)
                .SetApplicationVersion("test")
                .Configure(options =>
                {
                    options.UnknownSectionPolicy = UnknownSectionPolicy.Preserve;
                    options.MissingSectionPolicy = MissingSectionPolicy.Fail;
                }));
        }

        [TearDown]
        public void TearDown() { }

        [Test]
        public void SlotAndSectionIdsRejectPathTraversalAndOversizedInput()
        {
            Assert.That(SaveSlotId.TryCreate("../slot", out SaveSlotId ignoredSlot, out string slotError), Is.False);
            Assert.That(slotError, Does.Contain("只能包含"));
            Assert.That(SaveSectionId.TryCreate(new string('x', SaveSectionId.MaxLength + 1), out SaveSectionId ignoredSection, out string sectionError), Is.False);
            Assert.That(sectionError, Does.Contain("长度"));
        }

        [Test]
        public void ContainerRoundTripAndChecksumAreStable()
        {
            SaveSlotId slot = SaveSlotId.From("roundtrip");
            byte[] payload = { 1, 2, 3, 4 };
            var metadata = new SaveMetadata
            {
                SlotId = slot,
                Revision = 7,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                ApplicationVersion = "1.0",
                ContainerVersion = SaveContainerFormat.ContainerVersion
            };
            metadata.CustomMetadata["worldDate"] = "1-1-1";
            var entry = new SaveSectionEntry
            {
                Descriptor = new SaveSectionDescriptor
                {
                    Id = SaveSectionId.From("player"),
                    SchemaVersion = 1,
                    SerializerId = "raw-bytes",
                    PayloadLength = payload.Length,
                    Checksum = new XxHash64Checksum().Compute(payload, 0, payload.Length)
                },
                Payload = payload
            };

            using (var stream = new MemoryStream())
            {
                SaveContainerWriter.Write(stream, metadata, new[] { entry }, SaveKit.Options);
                stream.Position = 0;
                Assert.That(SaveContainerReader.TryRead(stream, SaveKit.Options, out SaveSnapshot snapshot,
                    out SaveErrorCode code, out string message), Is.True, $"{code}: {message}");
                Assert.That(snapshot.Metadata.Revision, Is.EqualTo(7));
                Assert.That(snapshot.Sections.Single().Payload, Is.EqualTo(payload));
            }
        }

        [Test]
        public void SaveLoadUsesTwoPhaseRestoreAndRevisionOnlyCommits()
        {
            var section = new IntSection("player");
            SaveKit.Register(section);
            SaveResult save = SaveKit.SaveAsync("slot1").GetAwaiter().GetResult();
            Assert.That(save.IsSuccess, Is.True, save.ErrorMessage);
            Assert.That(save.Revision, Is.EqualTo(1));

            section.Value = 42;
            SaveResult second = SaveKit.SaveAsync("slot1").GetAwaiter().GetResult();
            Assert.That(second.IsSuccess, Is.True, second.ErrorMessage);
            Assert.That(second.Revision, Is.EqualTo(2));

            section.Value = 0;
            SaveResult load = SaveKit.LoadAsync("slot1").GetAwaiter().GetResult();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(section.Value, Is.EqualTo(42));
            Assert.That(SaveKit.GetDiagnostics().Revision, Is.EqualTo(2));
        }

        [Test]
        public void MissingSectionCanUseDefaultAndRestoreDependenciesAreOrdered()
        {
            var order = new List<string>();
            SaveKit.Register(new IntSection("world", 1, order));
            SaveResult empty = SaveKit.SaveAsync("defaults").GetAwaiter().GetResult();
            Assert.That(empty.IsSuccess, Is.True, empty.ErrorMessage);

            SaveKit.Initialize(builder => builder.UseStorage(_storage).Configure(options => options.MissingSectionPolicy = MissingSectionPolicy.UseDefault));
            var restored = new List<string>();
            SaveKit.Register(new IntSection("world", 1, restored));
            SaveKit.Register(new DependentSection("player", SaveSectionId.From("world"), restored));
            SaveResult load = SaveKit.LoadAsync("defaults").GetAwaiter().GetResult();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(restored, Is.EqualTo(new[] { "world", "player" }));
        }

        [Test]
        public void MigrationChainIsRequiredAndAppliedBeforeRestore()
        {
            SaveKit.Initialize(builder => builder.UseStorage(_storage));
            var old = new IntSection("player", 1);
            SaveKit.Register(old);
            old.Value = 10;
            Assert.That(SaveKit.SaveAsync("migration").GetAwaiter().GetResult().IsSuccess, Is.True);

            SaveKit.Initialize(builder => builder.UseStorage(_storage));
            var current = new IntSection("player", 2);
            SaveKit.Register(current);
            Assert.That(SaveKit.RegisterMigration(SaveSectionId.From("player"), new AddVersionMigration()), Is.True);
            SaveResult load = SaveKit.LoadAsync("migration").GetAwaiter().GetResult();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(current.Value, Is.EqualTo(11));
        }

        [Test]
        public void UnknownSectionIsPreservedAcrossSave()
        {
            var known = new IntSection("known");
            SaveKit.Register(known);
            Assert.That(SaveKit.SaveAsync("unknown").GetAwaiter().GetResult().IsSuccess, Is.True);

            SaveKit.Initialize(builder => builder.UseStorage(_storage).Configure(options => options.MissingSectionPolicy = MissingSectionPolicy.Ignore));
            SaveKit.Register(new IntSection("new"));
            SaveResult load = SaveKit.LoadAsync("unknown").GetAwaiter().GetResult();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(SaveKit.SaveAsync("unknown").GetAwaiter().GetResult().IsSuccess, Is.True);
            using (var stream = _storage.OpenReadAsync(SaveSlotId.From("unknown"), SaveStorageFileKind.Current, default).GetAwaiter().GetResult())
            {
                Assert.That(SaveContainerReader.TryRead(stream, SaveKit.Options, out SaveSnapshot snapshot, out SaveErrorCode code, out string message), Is.True, $"{code}: {message}");
                Assert.That(snapshot.Sections.Select(entry => entry.Descriptor.Id.Value), Does.Contain("known"));
                Assert.That(snapshot.Sections.Select(entry => entry.Descriptor.Id.Value), Does.Contain("new"));
            }
        }

        [Test]
        public void LoadPreparesEverySectionBeforeAnyRestore()
        {
            var first = new IntSection("prepare-a") { Value = 1 };
            var second = new IntSection("prepare-b") { Value = 2 };
            SaveKit.Register(first);
            SaveKit.Register(second);
            Assert.That(SaveKit.SaveAsync("prepare").GetAwaiter().GetResult().IsSuccess, Is.True);

            first.RestoreCount = 0;
            second.RestoreCount = 0;
            second.RejectValidation = true;
            SaveResult load = SaveKit.LoadAsync("prepare").GetAwaiter().GetResult();
            Assert.That(load.ErrorCode, Is.EqualTo(SaveErrorCode.ValidationFailed));
            Assert.That(first.RestoreCount, Is.EqualTo(0));
            Assert.That(second.RestoreCount, Is.EqualTo(0));
        }

        [Test]
        public void MigrationChainAndUnsupportedNewerVersionAreRejected()
        {
            var old = new IntSection("chain", 1) { Value = 5 };
            SaveKit.Register(old);
            Assert.That(SaveKit.SaveAsync("chain").GetAwaiter().GetResult().IsSuccess, Is.True);

            SaveKit.Initialize(builder => builder.UseStorage(_storage));
            var current = new IntSection("chain", 3);
            SaveKit.Register(current);
            Assert.That(SaveKit.RegisterMigration(SaveSectionId.From("chain"), new AddVersionMigration()), Is.True);
            Assert.That(SaveKit.RegisterMigration(SaveSectionId.From("chain"), new AddVersionMigration2()), Is.True);
            SaveResult migrated = SaveKit.LoadAsync("chain").GetAwaiter().GetResult();
            Assert.That(migrated.IsSuccess, Is.True, migrated.ErrorMessage);
            Assert.That(current.Value, Is.EqualTo(7));
            Assert.That(SaveKit.SaveAsync("chain").GetAwaiter().GetResult().IsSuccess, Is.True);

            SaveKit.Initialize(builder => builder.UseStorage(_storage));
            var newer = new IntSection("chain", 1);
            SaveKit.Register(newer);
            SaveResult rejected = SaveKit.LoadAsync("chain").GetAwaiter().GetResult();
            Assert.That(rejected.ErrorCode, Is.EqualTo(SaveErrorCode.UnsupportedSectionVersion));
        }

        [Test]
        public void ContainerRejectsRandomAndTruncatedExternalFiles()
        {
            byte[] random = { 0x13, 0x37, 0x42, 0x99 };
            using (var stream = new MemoryStream(random, false))
            {
                Assert.That(SaveContainerReader.TryRead(stream, SaveKit.Options, out SaveSnapshot ignored,
                    out SaveErrorCode code, out string message), Is.False);
                Assert.That(code, Is.EqualTo(SaveErrorCode.ContainerCorrupted));
            }

            var section = new IntSection("truncated") { Value = 8 };
            SaveKit.Register(section);
            Assert.That(SaveKit.SaveAsync("truncated").GetAwaiter().GetResult().IsSuccess, Is.True);
            byte[] bytes = _storage.GetBytes("truncated", SaveStorageFileKind.Current);
            Array.Resize(ref bytes, bytes.Length - 1);
            using (var stream = new MemoryStream(bytes, false))
            {
                Assert.That(SaveContainerReader.TryRead(stream, SaveKit.Options, out SaveSnapshot ignored,
                    out SaveErrorCode code, out string message), Is.False);
                Assert.That(code, Is.EqualTo(SaveErrorCode.ContainerCorrupted));
            }
        }

        [Test]
        public void ContainerRejectsNegativePayloadLengthAndUnknownSerializer()
        {
            var section = new IntSection("manifest") { Value = 3 };
            SaveKit.Register(section);
            Assert.That(SaveKit.SaveAsync("manifest").GetAwaiter().GetResult().IsSuccess, Is.True);
            byte[] bytes = _storage.GetBytes("manifest", SaveStorageFileKind.Current);
            int payloadLengthOffset = FindFirstPayloadLengthOffset(bytes);
            Buffer.BlockCopy(BitConverter.GetBytes(-1L), 0, bytes, payloadLengthOffset, sizeof(long));
            using (var stream = new MemoryStream(bytes, false))
            {
                Assert.That(SaveContainerReader.TryRead(stream, SaveKit.Options, out SaveSnapshot ignored,
                    out SaveErrorCode code, out string message), Is.False);
                Assert.That(code, Is.EqualTo(SaveErrorCode.InvalidManifest));
            }

            SaveKit.Initialize(builder => builder.UseStorage(_storage));
            byte[] payload = { 1, 2, 3 };
            var metadata = new SaveMetadata
            {
                SlotId = SaveSlotId.From("serializer"),
                Revision = 1,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                ApplicationVersion = "test",
                ContainerVersion = SaveContainerFormat.ContainerVersion
            };
            var entry = new SaveSectionEntry
            {
                Descriptor = new SaveSectionDescriptor
                {
                    Id = SaveSectionId.From("unknown-serializer"),
                    SchemaVersion = 1,
                    SerializerId = "serializer-does-not-exist",
                    PayloadLength = payload.Length,
                    Checksum = new XxHash64Checksum().Compute(payload, 0, payload.Length)
                },
                Payload = payload
            };
            using (var container = new MemoryStream())
            {
                SaveContainerWriter.Write(container, metadata, new[] { entry }, SaveKit.Options);
                _storage.SetBytes("serializer", SaveStorageFileKind.Current, container.ToArray());
            }
            SaveKit.Initialize(builder => builder.UseStorage(_storage));
            SaveKit.Register(new RawSection("unknown-serializer", "serializer-does-not-exist"));
            SaveResult result = SaveKit.LoadAsync("serializer").GetAwaiter().GetResult();
            Assert.That(result.ErrorCode, Is.EqualTo(SaveErrorCode.SerializerMissing));
        }

        [Test]
        public void NewtonsoftAdapterIgnoresTypeMetadataFromExternalJson()
        {
            var serializer = new NewtonsoftJsonSaveSerializer();
            string json = "{\"$type\":\"System.IO.FileInfo, mscorlib\",\"Value\":9}";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), false))
            {
                IntData data = serializer.DeserializeAsync(typeof(IntData), stream, System.Threading.CancellationToken.None).GetAwaiter().GetResult() as IntData;
                Assert.That(data, Is.Not.Null);
                Assert.That(data.Value, Is.EqualTo(9));
            }
        }

        [Test]
        public void FailedCommitDoesNotReplaceCurrent()
        {
            var section = new IntSection("player") { Value = 1 };
            SaveKit.Register(section);
            Assert.That(SaveKit.SaveAsync("transaction").GetAwaiter().GetResult().IsSuccess, Is.True);
            byte[] before = _storage.GetBytes("transaction", SaveStorageFileKind.Current);
            _storage.FailCommit = true;
            section.Value = 2;
            SaveResult failure = SaveKit.SaveAsync("transaction").GetAwaiter().GetResult();
            Assert.That(failure.IsSuccess, Is.False);
            Assert.That(_storage.GetBytes("transaction", SaveStorageFileKind.Current), Is.EqualTo(before));
        }

        [Test]
        public void CorruptCurrentIsNotRotatedIntoBackupBySave()
        {
            var section = new IntSection("save-corrupt") { Value = 1 };
            SaveKit.Register(section);
            Assert.That(SaveKit.SaveAsync("save-corrupt").GetAwaiter().GetResult().IsSuccess, Is.True);
            byte[] before = _storage.GetBytes("save-corrupt", SaveStorageFileKind.Current);
            before[before.Length - 1] ^= 0x40;
            _storage.SetBytes("save-corrupt", SaveStorageFileKind.Current, before);

            SaveResult result = SaveKit.SaveAsync("save-corrupt").GetAwaiter().GetResult();
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(SaveErrorCode.ContainerCorrupted).Or.EqualTo(SaveErrorCode.ChecksumMismatch));
            Assert.That(_storage.GetBytes("save-corrupt", SaveStorageFileKind.Backup), Is.Null);
        }

        [Test]
        public void FailedWriteDoesNotReplaceCurrentOrLeaveTemporaryData()
        {
            var section = new IntSection("write-fail") { Value = 1 };
            SaveKit.Register(section);
            Assert.That(SaveKit.SaveAsync("write-fail").GetAwaiter().GetResult().IsSuccess, Is.True);
            byte[] before = _storage.GetBytes("write-fail", SaveStorageFileKind.Current);
            _storage.FailWrite = true;
            section.Value = 2;
            SaveResult result = SaveKit.SaveAsync("write-fail").GetAwaiter().GetResult();
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(SaveErrorCode.StorageError));
            Assert.That(_storage.GetBytes("write-fail", SaveStorageFileKind.Current), Is.EqualTo(before));
            Assert.That(_storage.GetBytes("write-fail", SaveStorageFileKind.Temporary), Is.Null);
        }

        [Test]
        public void LifecycleHooksBracketCaptureAndRestore()
        {
            var events = new List<string>();
            SaveKit.Initialize(builder => builder
                .UseStorage(_storage)
                .UseLifecycleHooks(new RecordingHooks(events)));
            var section = new IntSection("hooks") { Value = 4 };
            SaveKit.Register(section);
            Assert.That(SaveKit.SaveAsync("hooks").GetAwaiter().GetResult().IsSuccess, Is.True);
            section.Value = 0;
            Assert.That(SaveKit.LoadAsync("hooks").GetAwaiter().GetResult().IsSuccess, Is.True);
            Assert.That(events, Is.EqualTo(new[] { "BeforeCapture", "AfterCapture", "BeforeRestore", "AfterRestore" }));
        }

        [Test]
        public void CorruptCurrentLoadsBackupAndRecoversWithoutPartialRestore()
        {
            var section = new IntSection("recovery") { Value = 11 };
            SaveKit.Register(section);
            Assert.That(SaveKit.SaveAsync("recovery").GetAwaiter().GetResult().IsSuccess, Is.True);

            section.Value = 22;
            Assert.That(SaveKit.SaveAsync("recovery").GetAwaiter().GetResult().IsSuccess, Is.True);
            byte[] corrupt = _storage.GetBytes("recovery", SaveStorageFileKind.Current);
            corrupt[corrupt.Length - 1] ^= 0x5A;
            _storage.SetBytes("recovery", SaveStorageFileKind.Current, corrupt);

            section.Value = 0;
            SaveResult load = SaveKit.LoadAsync("recovery").GetAwaiter().GetResult();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(load.UsedBackup, Is.True);
            Assert.That(section.Value, Is.EqualTo(11));
            Assert.That(SaveKit.GetDiagnostics().BackupUsed, Is.True);
        }

        [Test]
        public void NewtonsoftAdapterUsesSectionSerializerWithoutChangingCoreContainer()
        {
            SaveKit.Initialize(builder => builder
                .UseStorage(_storage)
                .UseSerializer(new NewtonsoftJsonSaveSerializer())
                .SetDefaultSerializer("newtonsoft-json"));
            var section = new IntSection("json");
            SaveKit.Register(section);
            section.Value = 73;
            SaveResult save = SaveKit.SaveAsync("json").GetAwaiter().GetResult();
            Assert.That(save.IsSuccess, Is.True, save.ErrorMessage);
            section.Value = 0;
            SaveResult load = SaveKit.LoadAsync("json").GetAwaiter().GetResult();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(section.Value, Is.EqualTo(73));
        }

        [Serializable]
        public sealed class IntData { public int Value; }

        private class IntSection : SaveSection<IntData>
        {
            private readonly List<string> _order;
            public readonly int Version;
            public int Value;
            public int RestoreCount;
            public bool RejectValidation;
            public override SaveSectionId Id { get; }
            public override int SchemaVersion => Version;

            public IntSection(string id, int version = 1, List<string> order = null)
            {
                Id = SaveSectionId.From(id);
                Version = version;
                _order = order;
            }

            public override IntData Capture(SaveCaptureContext context) => new IntData { Value = Value };
            public override SaveValidationResult Validate(IntData data, SaveValidationContext context)
            {
                return RejectValidation ? SaveValidationResult.Invalid("Rejected", "test validation failure") : SaveValidationResult.Valid();
            }
            public override void Restore(IntData data, SaveRestoreContext context)
            {
                Value = data == null ? 0 : data.Value;
                RestoreCount++;
                _order?.Add(Id.Value);
            }
        }

        private sealed class DependentSection : IntSection
        {
            private readonly IReadOnlyList<SaveSectionId> _after;
            public override IReadOnlyList<SaveSectionId> RestoreAfter => _after;

            public DependentSection(string id, SaveSectionId after, List<string> order)
                : base(id, 1, order) { _after = new[] { after }; }
        }

        private sealed class AddVersionMigration : SaveMigration<IntData, IntData>
        {
            public override int FromVersion => 1;
            public override int ToVersion => 2;
            public override IntData Migrate(IntData data, SaveMigrationContext context)
            {
                data.Value++;
                return data;
            }
        }

        private sealed class AddVersionMigration2 : SaveMigration<IntData, IntData>
        {
            public override int FromVersion => 2;
            public override int ToVersion => 3;
            public override IntData Migrate(IntData data, SaveMigrationContext context)
            {
                data.Value++;
                return data;
            }
        }

        private sealed class RecordingHooks : ISaveLifecycleHooks
        {
            private readonly List<string> _events;
            public RecordingHooks(List<string> events) { _events = events; }
            public void BeforeCapture(SaveCaptureContext context) { _events.Add("BeforeCapture"); }
            public void AfterCapture(SaveCaptureContext context) { _events.Add("AfterCapture"); }
            public void BeforeRestore(SaveRestoreContext context) { _events.Add("BeforeRestore"); }
            public void AfterRestore(SaveRestoreContext context) { _events.Add("AfterRestore"); }
        }

        private sealed class RawSection : SaveSection<byte[]>
        {
            private readonly string _serializerId;
            public override SaveSectionId Id { get; }
            public override string SerializerId => _serializerId;

            public RawSection(string id, string serializerId)
            {
                Id = SaveSectionId.From(id);
                _serializerId = serializerId;
            }

            public override byte[] Capture(SaveCaptureContext context) => new byte[] { 1, 2, 3 };
            public override void Restore(byte[] data, SaveRestoreContext context) { }
        }

        private static int FindFirstPayloadLengthOffset(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                reader.ReadBytes(SaveContainerFormat.Magic.Length);
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt64();
                reader.ReadInt64();
                reader.ReadInt64();
                ReadContainerString(reader);
                ReadContainerString(reader);
                int customCount = reader.ReadInt32();
                for (int i = 0; i < customCount; i++)
                {
                    ReadContainerString(reader);
                    ReadContainerString(reader);
                }

                Assert.That(reader.ReadInt32(), Is.GreaterThan(0));
                ReadContainerString(reader);
                reader.ReadInt32();
                ReadContainerString(reader);
                return checked((int)stream.Position);
            }
        }

        private static string ReadContainerString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }
    }
}
