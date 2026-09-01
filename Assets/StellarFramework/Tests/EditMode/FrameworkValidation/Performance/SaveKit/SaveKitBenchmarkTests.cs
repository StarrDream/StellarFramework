using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace StellarFramework.Tests.FrameworkValidation
{
    /// <summary>
    /// 仅记录结构性性能趋势，不设固定毫秒阈值。运行方式：Unity Test Runner -> EditMode -> Benchmark。
    /// </summary>
    public sealed class SaveKitBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void ContainerBenchmark_100000StructRecords()
        {
            const int recordCount = 100000;
            long allocatedBefore = GC.GetTotalMemory(false);
            var records = new BenchmarkRecord[recordCount];
            Stopwatch captureWatch = Stopwatch.StartNew();
            for (int i = 0; i < records.Length; i++)
            {
                records[i] = new BenchmarkRecord { Id = i, Value = i * 0.25f, State = (short)(i % 7) };
            }
            captureWatch.Stop();

            Stopwatch serializeWatch = Stopwatch.StartNew();
            var payload = new byte[recordCount * 20];
            using (var stream = new MemoryStream(payload, true))
            using (var writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < records.Length; i++)
                {
                    writer.Write(records[i].Id);
                    writer.Write(records[i].Value);
                    writer.Write(records[i].State);
                    writer.Write((short)0);
                    writer.Write((long)i * 1000L);
                }
            }
            serializeWatch.Stop();

            Stopwatch checksumWatch = Stopwatch.StartNew();
            ulong checksum = new XxHash64Checksum().Compute(payload, 0, payload.Length);
            checksumWatch.Stop();

            var metadata = new SaveMetadata
            {
                SlotId = SaveSlotId.From("benchmark"),
                Revision = 1,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                ApplicationVersion = "benchmark",
                ContainerVersion = SaveContainerFormat.ContainerVersion
            };
            var entry = new SaveSectionEntry
            {
                Descriptor = new SaveSectionDescriptor
                {
                    Id = SaveSectionId.From("records"),
                    SchemaVersion = 1,
                    SerializerId = "raw-bytes",
                    PayloadLength = payload.Length,
                    Checksum = checksum
                },
                Payload = payload
            };

            Stopwatch writeWatch = Stopwatch.StartNew();
            var container = new MemoryStream();
            SaveContainerWriter.Write(container, metadata, new[] { entry }, new SaveKitOptions());
            writeWatch.Stop();

            Stopwatch readWatch = Stopwatch.StartNew();
            container.Position = 0;
            Assert.That(SaveContainerReader.TryRead(container, new SaveKitOptions(), out SaveSnapshot snapshot,
                out SaveErrorCode errorCode, out string errorMessage), Is.True, $"{errorCode}: {errorMessage}");
            readWatch.Stop();

            Stopwatch deserializeWatch = Stopwatch.StartNew();
            using (var payloadStream = new MemoryStream(snapshot.Sections[0].Payload, false))
            {
                byte[] restored = new RawBytesSaveSerializer()
                    .DeserializeAsync(typeof(byte[]), payloadStream, CancellationToken.None)
                    .GetAwaiter().GetResult() as byte[];
                Assert.That(restored, Is.EqualTo(payload));
            }
            deserializeWatch.Stop();

            Stopwatch migrationWatch = Stopwatch.StartNew();
            object migrated = new NoOpMigration().Migrate(payload, null);
            migrationWatch.Stop();
            Assert.That(migrated, Is.SameAs(payload));

            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;
            TestContext.Progress.WriteLine(
                "SaveKit benchmark records={0} rawBytes={1} finalBytes={2} captureMs={3:F3} serializeMs={4:F3} writeMs={5:F3} readMs={6:F3} deserializeMs={7:F3} migrationMs={8:F3} checksumMs={9:F3} allocationDelta={10}",
                recordCount, payload.Length, container.Length, captureWatch.Elapsed.TotalMilliseconds, serializeWatch.Elapsed.TotalMilliseconds,
                writeWatch.Elapsed.TotalMilliseconds, readWatch.Elapsed.TotalMilliseconds,
                deserializeWatch.Elapsed.TotalMilliseconds, migrationWatch.Elapsed.TotalMilliseconds,
                checksumWatch.Elapsed.TotalMilliseconds, allocatedDelta);
        }

        [Test, Category("Benchmark")]
        public void EndToEndBenchmark_100000CropRecords()
        {
            const int recordCount = 100000;
            var storage = new InMemorySaveStorage();
            var serializer = new CropBinarySerializer();
            var section = new CropSection("crops");
            section.Records = new CropSaveRecord[recordCount];
            for (int i = 0; i < recordCount; i++)
            {
                section.Records[i] = new CropSaveRecord
                {
                    CropTypeId = i % 32,
                    CellX = i % 512,
                    CellY = i / 512,
                    PlantTick = i * 10L,
                    Stage = (byte)(i % 6)
                };
            }

            long allocatedBefore = GC.GetTotalMemory(false);
            SaveKit.Initialize(builder => builder.UseStorage(storage).UseSerializer(serializer).SetDefaultSerializer(serializer.Id));
            SaveKit.Register(section);
            Stopwatch saveWatch = Stopwatch.StartNew();
            SaveResult save = SaveKit.SaveAsync("crops-e2e").GetAwaiter().GetResult();
            saveWatch.Stop();
            Assert.That(save.IsSuccess, Is.True, save.ErrorMessage);
            SaveOperationDiagnostics saveDiagnostics = save.Diagnostics;
            long fileSize = storage.GetBytes("crops-e2e", SaveStorageFileKind.Current).LongLength;

            section.Records = Array.Empty<CropSaveRecord>();
            Stopwatch loadWatch = Stopwatch.StartNew();
            SaveResult load = SaveKit.LoadAsync("crops-e2e").GetAwaiter().GetResult();
            loadWatch.Stop();
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(section.Records.Length, Is.EqualTo(recordCount));
            SaveOperationDiagnostics loadDiagnostics = load.Diagnostics;
            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;

            string benchmarkMessage = string.Format(
                "SaveKit E2E benchmark records={0} fileBytes={1} saveTotalMs={2:F3} saveCaptureMs={3:F3} saveSerializeMs={4:F3} saveChecksumMs={5:F3} saveIoMs={6:F3} saveCommitMs={7:F3} loadWallMs={8:F3} loadDeserializeMs={9:F3} loadMigrationMs={10:F3} loadValidationMs={11:F3} loadRestoreMs={12:F3} allocationDelta={13}",
                recordCount, fileSize, saveWatch.Elapsed.TotalMilliseconds, saveDiagnostics.CaptureDurationMs,
                saveDiagnostics.SerializeDurationMs, saveDiagnostics.ChecksumDurationMs, saveDiagnostics.IoDurationMs,
                saveDiagnostics.CommitDurationMs, loadWatch.Elapsed.TotalMilliseconds, loadDiagnostics.DeserializeDurationMs,
                loadDiagnostics.MigrationDurationMs, loadDiagnostics.ValidationDurationMs, loadDiagnostics.RestoreDurationMs,
                allocatedDelta);
            TestContext.Progress.WriteLine(benchmarkMessage);
            UnityEngine.Debug.Log(benchmarkMessage);
        }

        private struct BenchmarkRecord
        {
            public int Id;
            public float Value;
            public short State;
        }

        private sealed class NoOpMigration : ISaveMigration
        {
            public SaveSectionId SectionId => default(SaveSectionId);
            public int FromVersion => 1;
            public int ToVersion => 2;
            public Type FromType => typeof(byte[]);
            public Type ToType => typeof(byte[]);
            public object Migrate(object data, SaveMigrationContext context) => data;
        }

        private struct CropSaveRecord
        {
            public int CropTypeId;
            public int CellX;
            public int CellY;
            public long PlantTick;
            public byte Stage;
        }

        private sealed class CropSection : SaveSection<CropSaveRecord[]>
        {
            public override SaveSectionId Id { get; }
            public CropSaveRecord[] Records = Array.Empty<CropSaveRecord>();

            public CropSection(string id) { Id = SaveSectionId.From(id); }
            public override CropSaveRecord[] Capture(SaveCaptureContext context) => Records;
            public override void Restore(CropSaveRecord[] data, SaveRestoreContext context) { Records = data ?? Array.Empty<CropSaveRecord>(); }
        }

        private sealed class CropBinarySerializer : ISaveSerializer, ISaveSerializerCapabilities
        {
            public string Id => "benchmark-crop-binary";
            public SaveSerializerCapabilities Capabilities => SaveSerializerCapabilities.BackgroundExecution |
                SaveSerializerCapabilities.ThreadSafe;

            public UniTask SerializeAsync(Type dataType, object value, Stream destination, CancellationToken cancellationToken)
            {
                if (dataType != typeof(CropSaveRecord[]) || !(value is CropSaveRecord[] records))
                    throw new InvalidDataException("CropBinarySerializer 需要 CropSaveRecord[]。");
                using (var writer = new BinaryWriter(destination, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(records.Length);
                    for (int i = 0; i < records.Length; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        writer.Write(records[i].CropTypeId);
                        writer.Write(records[i].CellX);
                        writer.Write(records[i].CellY);
                        writer.Write(records[i].PlantTick);
                        writer.Write(records[i].Stage);
                    }
                }
                return UniTask.CompletedTask;
            }

            public UniTask<object> DeserializeAsync(Type dataType, Stream source, CancellationToken cancellationToken)
            {
                if (dataType != typeof(CropSaveRecord[])) throw new InvalidDataException("CropBinarySerializer 类型不匹配。");
                using (var reader = new BinaryReader(source, System.Text.Encoding.UTF8, true))
                {
                    int count = reader.ReadInt32();
                    if (count < 0 || count > 1000000) throw new InvalidDataException("Crop record count 非法。");
                    var records = new CropSaveRecord[count];
                    for (int i = 0; i < count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        records[i] = new CropSaveRecord
                        {
                            CropTypeId = reader.ReadInt32(),
                            CellX = reader.ReadInt32(),
                            CellY = reader.ReadInt32(),
                            PlantTick = reader.ReadInt64(),
                            Stage = reader.ReadByte()
                        };
                    }
                    return UniTask.FromResult<object>(records);
                }
            }
        }

    }
}
