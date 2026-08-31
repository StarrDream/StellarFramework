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

        private struct BenchmarkRecord
        {
            public int Id;
            public float Value;
            public short State;
        }

        private sealed class NoOpMigration : ISaveMigration
        {
            public int FromVersion => 1;
            public int ToVersion => 2;
            public object Migrate(object data, SaveMigrationContext context) => data;
        }

    }
}
