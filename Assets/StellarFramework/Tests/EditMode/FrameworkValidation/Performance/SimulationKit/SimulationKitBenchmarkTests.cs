using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    /// <summary>
    /// SimulationKit 趋势基准。测试只验证确定性、完整性和调度语义，不设置固定毫秒门槛。
    /// </summary>
    public sealed class SimulationKitBenchmarkTests
    {
        [Test, Category("Benchmark")]
        public void SimulationKitBenchmark_100kRegistrationLookupMutation()
        {
            const int entityCount = 100000;
            SimulationScheduler scheduler = new SimulationScheduler(entityCount);
            SimulationId[] ids = new SimulationId[entityCount];
            long[] intervals = new long[entityCount];
            long[] delays = new long[entityCount];
            for (int i = 0; i < entityCount; i++)
            {
                ids[i] = new SimulationId(i + 1);
                intervals[i] = 3 + (i % 17);
                delays[i] = i % intervals[i];
            }

            long checksum = 0L;
            int registrationFailures = 0;
            long allocatedBefore = GC.GetTotalMemory(false);
            Stopwatch registerWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                SimulationMutationResult result = scheduler.TryRegister(ids[i], 0, intervals[i], delays[i]);
                if (!result.Success) registrationFailures++;
            }
            registerWatch.Stop();

            int lookupFailures = 0;
            Stopwatch lookupWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                if (!scheduler.Contains(ids[i])) lookupFailures++;
                if (scheduler.TryGetInterval(ids[i], out long interval)) checksum += interval;
                else lookupFailures++;
                if (scheduler.TryGetNextDueTick(ids[i], out long due)) checksum += due;
                else lookupFailures++;
            }
            lookupWatch.Stop();

            int setIntervalFailures = 0;
            Stopwatch setIntervalWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                long nextInterval = 17 + (i % 19);
                SimulationMutationResult result = scheduler.TrySetInterval(ids[i], 1000000, nextInterval);
                if (!result.Success) setIntervalFailures++;
                checksum += nextInterval;
            }
            setIntervalWatch.Stop();

            int unregisterFailures = 0;
            Stopwatch unregisterWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++)
            {
                SimulationMutationResult result = scheduler.TryUnregister(ids[i]);
                if (!result.Success) unregisterFailures++;
                checksum += ids[i].Value;
            }
            unregisterWatch.Stop();
            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;

            string message = string.Format(
                "SimulationKit 100k registration env={0} EntityCount={1} RegisterMs={2:F3} LookupMs={3:F3} SetIntervalMs={4:F3} UnregisterMs={5:F3} Checksum={6} ManagedHeapDelta={7}",
                Application.unityVersion, entityCount, registerWatch.Elapsed.TotalMilliseconds,
                lookupWatch.Elapsed.TotalMilliseconds, setIntervalWatch.Elapsed.TotalMilliseconds,
                unregisterWatch.Elapsed.TotalMilliseconds, checksum, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
            Assert.That(scheduler.Count, Is.EqualTo(0));
            Assert.That(registrationFailures, Is.EqualTo(0));
            Assert.That(lookupFailures, Is.EqualTo(0));
            Assert.That(setIntervalFailures, Is.EqualTo(0));
            Assert.That(unregisterFailures, Is.EqualTo(0));
        }

        [Test, Category("Benchmark")]
        public void SimulationKitBenchmark_100kExplicitBacklogDrainThroughput()
        {
            const int entityCount = 100000;
            const int budget = 512;
            SimulationScheduler scheduler = new SimulationScheduler(entityCount);
            SimulationId[] ids = new SimulationId[entityCount];
            int[] seen = new int[entityCount];
            int registrationFailures = 0;
            for (int i = 0; i < entityCount; i++)
            {
                ids[i] = new SimulationId(i + 1);
                if (!scheduler.TryRegister(ids[i], 0, 100, 100).Success) registrationFailures++;
            }

            SimulationId[] destination = new SimulationId[budget];
            long checksum = 0L;
            int writtenTotal = 0;
            int collectCalls = 0;
            int invalidIds = 0;
            int duplicateIds = 0;
            long allocatedBefore = GC.GetTotalMemory(false);
            Stopwatch collectWatch = Stopwatch.StartNew();
            SimulationCollectResult result;
            do
            {
                result = scheduler.CollectDue(100, destination.AsSpan());
                collectCalls++;
                for (int i = 0; i < result.WrittenCount; i++)
                {
                    int index = destination[i].Value - 1;
                    if (index < 0 || index >= entityCount) invalidIds++;
                    else
                    {
                        seen[index]++;
                    }
                    checksum += destination[i].Value;
                    writtenTotal++;
                }
            } while (result.HasBacklog);
            collectWatch.Stop();
            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;

            for (int i = 0; i < entityCount; i++) if (seen[i] != 1) duplicateIds++;
            Assert.That(registrationFailures, Is.EqualTo(0));
            Assert.That(invalidIds, Is.EqualTo(0));
            Assert.That(duplicateIds, Is.EqualTo(0));
            Assert.That(writtenTotal, Is.EqualTo(entityCount));
            Assert.That(collectCalls, Is.EqualTo((entityCount + budget - 1) / budget));
            Assert.That(scheduler.CollectDue(100, destination.AsSpan()).WrittenCount, Is.EqualTo(0));
            Assert.That(scheduler.Count, Is.EqualTo(entityCount));

            string message = string.Format(
                "SimulationKit 100k explicit backlog drain throughput env={0} EntityCount={1} Budget={2} CollectCalls={3} Dispatch={4} CollectMs={5:F3} Checksum={6} ManagedHeapDelta={7}",
                Application.unityVersion, entityCount, budget, collectCalls, writtenTotal,
                collectWatch.Elapsed.TotalMilliseconds, checksum, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
        }

        [Test, Category("Benchmark")]
        public void SimulationKitBenchmark_1MStorageNoDueAndSampledMutation()
        {
            const int entityCount = 1000000;
            const int noDueIterations = 10000;
            SimulationScheduler scheduler = new SimulationScheduler(entityCount);
            SimulationId[] ids = new SimulationId[entityCount];
            int registrationFailures = 0;
            for (int i = 0; i < entityCount; i++)
            {
                ids[i] = new SimulationId(i + 1);
                if (!scheduler.TryRegister(ids[i], 0, 1000).Success) registrationFailures++;
            }

            SimulationId[] destination = new SimulationId[1];
            long checksum = 0L;
            int noDueWritten = 0;
            long allocatedBefore = GC.GetTotalMemory(false);
            Stopwatch noDueWatch = Stopwatch.StartNew();
            for (int i = 0; i < noDueIterations; i++)
            {
                SimulationCollectResult result = scheduler.CollectDue(999, destination.AsSpan());
                noDueWritten += result.WrittenCount;
                if (result.HasBacklog) checksum++;
            }
            noDueWatch.Stop();

            int lookupCount = 0;
            Stopwatch lookupWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i += 16)
            {
                if (scheduler.Contains(ids[i])) checksum += ids[i].Value;
                if (scheduler.TryGetInterval(ids[i], out long interval)) checksum += interval;
                if (scheduler.TryGetNextDueTick(ids[i], out long due)) checksum += due;
                lookupCount++;
            }
            lookupWatch.Stop();

            int setCount = 0;
            int setFailures = 0;
            Stopwatch setWatch = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i += 32)
            {
                SimulationMutationResult result = scheduler.TrySetInterval(ids[i], 999, 2000 + (i % 31));
                if (!result.Success) setFailures++;
                setCount++;
                checksum += i;
            }
            setWatch.Stop();

            Stopwatch clearWatch = Stopwatch.StartNew();
            scheduler.Clear();
            clearWatch.Stop();
            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;

            Assert.That(noDueWritten, Is.EqualTo(0));
            Assert.That(registrationFailures, Is.EqualTo(0));
            Assert.That(setFailures, Is.EqualTo(0));
            Assert.That(scheduler.Count, Is.EqualTo(0));
            string message = string.Format(
                "SimulationKit 1M storage env={0} EntityCount={1} NoDueIterations={2} NoDueMs={3:F3} LookupCount={4} LookupMs={5:F3} SetIntervalCount={6} SetIntervalMs={7:F3} ClearMs={8:F3} Checksum={9} ManagedHeapDelta={10}",
                Application.unityVersion, entityCount, noDueIterations, noDueWatch.Elapsed.TotalMilliseconds,
                lookupCount, lookupWatch.Elapsed.TotalMilliseconds, setCount, setWatch.Elapsed.TotalMilliseconds,
                clearWatch.Elapsed.TotalMilliseconds, checksum, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
        }

        [Test, Category("Benchmark")]
        public void SimulationKitBenchmark_100kStaggeredBacklogDrainThroughput()
        {
            const int entityCount = 100000;
            const int budget = 512;
            const int stepCount = 101;
            SimulationScheduler scheduler = new SimulationScheduler(entityCount);
            SimulationId[] ids = new SimulationId[entityCount];
            int[] seen = new int[entityCount];
            int[] roundDispatches = new int[stepCount];
            int registrationFailures = 0;
            for (int i = 0; i < entityCount; i++)
            {
                ids[i] = new SimulationId(i + 1);
                if (!scheduler.TryRegister(ids[i], 0, 100, i % 100).Success) registrationFailures++;
            }

            SimulationId[] destination = new SimulationId[budget];
            long checksum = 0L;
            int collectCalls = 0;
            int totalDispatch = 0;
            long allocatedBefore = GC.GetTotalMemory(false);
            Stopwatch dispatchWatch = Stopwatch.StartNew();
            for (int step = 0; step < stepCount; step++)
            {
                long nowTick = step * 10L;
                bool backlog;
                do
                {
                    SimulationCollectResult result = scheduler.CollectDue(nowTick, destination.AsSpan());
                    collectCalls++;
                    for (int i = 0; i < result.WrittenCount; i++)
                    {
                        int index = destination[i].Value - 1;
                        if (index >= 0 && index < entityCount) seen[index]++;
                        roundDispatches[step]++;
                        totalDispatch++;
                        checksum += destination[i].Value;
                    }

                    backlog = result.HasBacklog;
                } while (backlog);
            }
            dispatchWatch.Stop();
            long allocatedDelta = GC.GetTotalMemory(false) - allocatedBefore;

            int missingIds = 0;
            for (int i = 0; i < entityCount; i++) if (seen[i] == 0) missingIds++;
            Assert.That(registrationFailures, Is.EqualTo(0));
            Assert.That(missingIds, Is.EqualTo(0));
            Assert.That(totalDispatch, Is.GreaterThanOrEqualTo(entityCount));
            Assert.That(scheduler.Count, Is.EqualTo(entityCount));
            string message = string.Format(
                "SimulationKit 100k staggered backlog drain throughput env={0} EntityCount={1} StepCount={2} Budget={3} CollectCalls={4} Dispatch={5} DispatchMs={6:F3} RoundDispatchChecksum={7} Checksum={8} ManagedHeapDelta={9}",
                Application.unityVersion, entityCount, stepCount, budget, collectCalls, totalDispatch,
                dispatchWatch.Elapsed.TotalMilliseconds, Sum(roundDispatches), checksum, allocatedDelta);
            TestContext.Progress.WriteLine(message);
            UnityEngine.Debug.Log(message);
        }

        private static int Sum(int[] values)
        {
            int sum = 0;
            for (int i = 0; i < values.Length; i++) sum += values[i];
            return sum;
        }
    }
}
