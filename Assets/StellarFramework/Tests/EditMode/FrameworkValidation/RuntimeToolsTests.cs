using System.Collections.Generic;
using NUnit.Framework;
using StellarFramework.RuntimeTools;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class RuntimeToolsTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void WeightedRandomUsesDeterministicSampleWithoutAllocatingAWorkingCollection()
        {
            var entries = new[]
            {
                new WeightedValue<string>("Never", 0f),
                new WeightedValue<string>("Common", 1f),
                new WeightedValue<string>("Rare", 3f)
            };

            Assert.That(WeightedRandom.TryChoose(entries, 0f, out string first), Is.True);
            Assert.That(first, Is.EqualTo("Common"));

            Assert.That(WeightedRandom.TryChoose(entries, 0.25f, out string second), Is.True);
            Assert.That(second, Is.EqualTo("Rare"));

            Assert.That(WeightedRandom.TryChoose(entries, 1f, out string last), Is.True);
            Assert.That(last, Is.EqualTo("Rare"));
        }

        [Test]
        public void WeightedRandomRejectsInvalidWeightsAndEmptyTotals()
        {
            var invalid = new[] { new WeightedValue<int>(1, -1f) };
            var zero = new[] { new WeightedValue<int>(1, 0f) };

            Assert.That(WeightedRandom.TryChoose(invalid, 0.5f, out _), Is.False);
            Assert.That(WeightedRandom.TryChoose(zero, 0.5f, out _), Is.False);
        }

        [Test]
        public void TransformSnapshotRestoresLocalTransform()
        {
            Transform transform = Create("snapshot").transform;
            transform.localPosition = new Vector3(1f, 2f, 3f);
            transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            transform.localScale = new Vector3(2f, 3f, 4f);

            TransformSnapshot snapshot = TransformSnapshot.Capture(transform);
            transform.ResetLocal();
            snapshot.Restore(transform);

            Assert.That(transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(Quaternion.Angle(transform.localRotation, Quaternion.Euler(10f, 20f, 30f)), Is.LessThan(0.001f));
            Assert.That(transform.localScale, Is.EqualTo(new Vector3(2f, 3f, 4f)));
        }

        [Test]
        public void TransformUtilChangesOnlyRequestedAxis()
        {
            Transform transform = Create("transform-util").transform;
            transform.position = new Vector3(1f, 2f, 3f);
            transform.SetPositionY(9f);
            Assert.That(transform.position, Is.EqualTo(new Vector3(1f, 9f, 3f)));

            transform.localPosition = new Vector3(4f, 5f, 6f);
            transform.SetLocalPositionX(8f);
            Assert.That(transform.localPosition, Is.EqualTo(new Vector3(8f, 5f, 6f)));
        }

        [Test]
        public void RandomPointUtilitiesStayInsideRequestedRegions()
        {
            var bounds = new Bounds(new Vector3(2f, 3f, 4f), new Vector3(10f, 6f, 8f));
            for (int i = 0; i < 128; i++)
            {
                Vector3 circle = RandomPointUtil.InsideCircleXZ(3f);
                Assert.That(new Vector2(circle.x, circle.z).magnitude, Is.LessThanOrEqualTo(3.0001f));
                Assert.That(circle.y, Is.EqualTo(0f));

                Vector3 sphere = RandomPointUtil.InsideSphere(2f);
                Assert.That(sphere.magnitude, Is.LessThanOrEqualTo(2.0001f));

                Assert.That(bounds.Contains(RandomPointUtil.InsideBounds(bounds)), Is.True);
            }
        }

        [Test]
        public void FollowTargetPreservesCapturedOffset()
        {
            Transform target = Create("target").transform;
            Transform follower = Create("follower").transform;
            target.position = new Vector3(10f, 0f, 0f);
            follower.position = new Vector3(12f, 0f, 0f);

            FollowTarget component = follower.gameObject.AddComponent<FollowTarget>();
            component.SetTarget(target, true);
            target.position = new Vector3(20f, 0f, 0f);
            component.Tick(1f / 60f);

            Assert.That(Vector3.Distance(follower.position, new Vector3(22f, 0f, 0f)), Is.LessThan(0.001f));
        }

        [Test]
        public void RotatorCanBeDrivenByExternalTick()
        {
            GameObject go = Create("rotator");
            Rotator rotator = go.AddComponent<Rotator>();
            rotator.SetSpeed(new Vector3(0f, 90f, 0f));
            rotator.Tick(1f);

            Assert.That(Quaternion.Angle(go.transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.001f));
        }

        [Test]
        public void BillboardFacesExplicitCamera()
        {
            GameObject cameraObject = Create("camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, 10f);

            GameObject go = Create("billboard");
            UniversalBillboard billboard = go.AddComponent<UniversalBillboard>();
            billboard.SetCamera(camera);
            billboard.Tick(1f / 60f);

            Assert.That(Vector3.Dot(go.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));
        }

        [Test]
        public void PhysicsProbeAndBoundsUtilityUseRealUnityPhysics()
        {
            GameObject cube = Create("cube");
            cube.transform.position = new Vector3(0f, 0f, 5f);
            BoxCollider collider = cube.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 2f, 2f);
            Physics.SyncTransforms();

            PhysicsProbeRequest request = PhysicsProbeRequest.Ray(
                Vector3.zero,
                Vector3.forward,
                10f,
                ~0,
                QueryTriggerInteraction.Ignore);
            Assert.That(PhysicsProbe.TryCast(in request, out PhysicsProbeHit hit), Is.True);
            Assert.That(hit.Collider, Is.EqualTo(collider));

            Assert.That(BoundsUtility.TryCalculateColliderBounds(cube.transform, true, out Bounds bounds), Is.True);
            Assert.That(bounds.center, Is.EqualTo(cube.transform.position));
            Assert.That(bounds.size, Is.EqualTo(new Vector3(2f, 2f, 2f)));
        }

        [Test]
        public void GroundCheckerUsesStableFramesBeforeReportingGrounded()
        {
            GameObject ground = Create("ground");
            ground.transform.position = Vector3.zero;
            ground.AddComponent<BoxCollider>().size = new Vector3(4f, 1f, 4f);

            GameObject actor = Create("actor");
            actor.transform.position = new Vector3(0f, 0.75f, 0f);
            GroundChecker checker = actor.AddComponent<GroundChecker>();
            checker.ConfigureProbe(PhysicsProbeShape.Sphere, Vector3.zero, Vector3.down, 0.25f, 0.2f);
            checker.ConfigureFilter(~0, QueryTriggerInteraction.Ignore, 2);
            Physics.SyncTransforms();

            Assert.That(checker.Evaluate(), Is.False);
            Assert.That(checker.Evaluate(), Is.True);
            Assert.That(checker.LastHit.Collider, Is.Not.Null);
        }

        [Test]
        public void PhysicsRelayFilterAppliesLayerMask()
        {
            GameObject target = Create("relay-target");
            target.layer = 8;

            PhysicsRelayFilter filter = PhysicsRelayFilter.All;
            filter.Configure(1 << 8);
            Assert.That(filter.Matches(target), Is.True);

            filter.Configure(1 << 9);
            Assert.That(filter.Matches(target), Is.False);
        }

        [Test]
        public void FrameRateSamplerMaintainsFixedWindowStatistics()
        {
            var sampler = new FrameRateSampler(3, 0f);

            Assert.That(sampler.PushFrame(1f / 60f), Is.True);
            Assert.That(sampler.PushFrame(1f / 30f), Is.True);
            Assert.That(sampler.PushFrame(1f / 20f), Is.True);

            FrameRateSnapshot first = sampler.Snapshot;
            Assert.That(first.SampleCount, Is.EqualTo(3));
            Assert.That(first.CurrentFps, Is.EqualTo(20f).Within(0.001f));
            Assert.That(first.AverageFps, Is.EqualTo(110f / 3f).Within(0.001f));
            Assert.That(first.MinFps, Is.EqualTo(20f).Within(0.001f));
            Assert.That(first.MaxFps, Is.EqualTo(60f).Within(0.001f));

            Assert.That(sampler.PushFrame(1f / 10f), Is.True);
            FrameRateSnapshot wrapped = sampler.Snapshot;
            Assert.That(wrapped.CurrentFps, Is.EqualTo(10f).Within(0.001f));
            Assert.That(wrapped.AverageFps, Is.EqualTo(20f).Within(0.001f));
            Assert.That(wrapped.MinFps, Is.EqualTo(10f).Within(0.001f));
            Assert.That(wrapped.MaxFps, Is.EqualTo(30f).Within(0.001f));

            Assert.That(sampler.PushFrame(0f), Is.False);
            Assert.That(sampler.Snapshot.CurrentFps, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void PhysicsOverlapUsesCallerBufferAndLayerMask()
        {
            GameObject included = Create("overlap-included");
            included.layer = 8;
            included.transform.position = new Vector3(0.5f, 0f, 0f);
            Collider includedCollider = included.AddComponent<BoxCollider>();

            GameObject excluded = Create("overlap-excluded");
            excluded.layer = 9;
            excluded.transform.position = new Vector3(-0.5f, 0f, 0f);
            excluded.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            var results = new Collider[4];
            PhysicsOverlapRequest request = PhysicsOverlapRequest.Sphere(
                Vector3.zero,
                2f,
                1 << 8,
                QueryTriggerInteraction.Ignore);

            int count = PhysicsOverlap.QueryNonAlloc(in request, results);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(includedCollider));

            Assert.That(PhysicsOverlap.QueryNonAlloc(in request, null), Is.EqualTo(0));
        }

        [Test]
        public void RendererPropertyBlockControllerPreservesExistingProperties()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "property-block";
            _createdObjects.Add(go);

            Renderer renderer = go.GetComponent<Renderer>();
            int existingId = Shader.PropertyToID("_RuntimeToolsExisting");
            int valueId = Shader.PropertyToID("_RuntimeToolsValue");
            var block = new MaterialPropertyBlock();
            block.SetFloat(existingId, 7f);
            renderer.SetPropertyBlock(block);

            RendererPropertyBlockController controller = go.AddComponent<RendererPropertyBlockController>();
            Assert.That(controller.SetFloat(valueId, 3.5f), Is.True);

            block.Clear();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat(existingId), Is.EqualTo(7f).Within(0.001f));
            Assert.That(block.GetFloat(valueId), Is.EqualTo(3.5f).Within(0.001f));

            Assert.That(controller.ClearAll(), Is.True);
            block.Clear();
            renderer.GetPropertyBlock(block);
            Assert.That(block.isEmpty, Is.True);
        }

        [Test]
        public void TransformShakeRestoresCapturedBaseTransform()
        {
            Transform transform = Create("shake").transform;
            transform.localPosition = new Vector3(2f, 3f, 4f);
            transform.localRotation = Quaternion.Euler(5f, 10f, 15f);
            Vector3 basePosition = transform.localPosition;
            Quaternion baseRotation = transform.localRotation;

            TransformShake shake = transform.gameObject.AddComponent<TransformShake>();
            shake.AddTrauma(1f);
            shake.Tick(0.1f);

            bool moved = Vector3.Distance(transform.localPosition, basePosition) > 0.0001f;
            bool rotated = Quaternion.Angle(transform.localRotation, baseRotation) > 0.0001f;
            Assert.That(moved || rotated, Is.True);

            shake.Stop();
            Assert.That(Vector3.Distance(transform.localPosition, basePosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(transform.localRotation, baseRotation), Is.LessThan(0.0001f));
        }

        [Test]
        public void FrameRateSamplerHotPathDoesNotAllocateAfterWarmup()
        {
            var sampler = new FrameRateSampler(16, 0f);
            for (int i = 0; i < 32; i++)
            {
                sampler.PushFrame(1f / 60f);
            }

            float checksum = 0f;
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                sampler.PushFrame(1f / 60f);
                checksum += sampler.Snapshot.CurrentFps;
            }

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(checksum, Is.GreaterThan(0f));
            Assert.That(allocated, Is.EqualTo(0L));
        }

        [Test]
        public void PhysicsOverlapHotPathDoesNotAllocateAfterWarmup()
        {
            GameObject target = Create("overlap-allocation-target");
            target.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            var results = new Collider[4];
            PhysicsOverlapRequest request = PhysicsOverlapRequest.Sphere(
                Vector3.zero,
                2f,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < 8; i++)
            {
                PhysicsOverlap.QueryNonAlloc(in request, results);
            }

            int checksum = 0;
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                checksum += PhysicsOverlap.QueryNonAlloc(in request, results);
            }

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(checksum, Is.GreaterThan(0));
            Assert.That(allocated, Is.EqualTo(0L));
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _createdObjects.Add(go);
            return go;
        }
    }
}
