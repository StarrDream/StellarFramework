using System;
using NUnit.Framework;
using StellarFramework.GridKit.UnityProjection;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class GridKitUnityProjectionAdapterTests
    {
        [Test]
        public void BakeClassifiesFlatSteepBlockedObstacleAndMissingCells()
        {
            GridRect bounds =
                new GridRect(
                    new GridCoord(0, 0),
                    new GridSize(5, 1));
            GridProjectionBakeSettings settings =
                new GridProjectionBakeSettings(
                    bounds,
                    new Vector3(0f, 10f, 0f),
                    Vector2.one,
                    maxWalkableSlope: 30f,
                    defaultMovementCost: 1000L,
                    steepMovementCost: 2000L,
                    steepSlopeThreshold: 20f,
                    maxDistance: 20f);
            DenseGrid<GridBakeCell> destination =
                new DenseGrid<GridBakeCell>(bounds);
            GridBakeCell[] scratch =
                new GridBakeCell[destination.Count];

            GridProjectionBakeResult result =
                GridProjectionBaker.Bake(
                    new ClassificationSource(),
                    settings,
                    destination,
                    scratch);

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCells, Is.EqualTo(5));
            Assert.That(result.WalkableCells, Is.EqualTo(2));
            Assert.That(result.BlockedCells, Is.EqualTo(3));
            Assert.That(result.MissingSurfaceCells, Is.EqualTo(1));
            Assert.That(destination[new GridCoord(0, 0)].BaseWalkable, Is.True);
            Assert.That(destination[new GridCoord(0, 0)].BaseMovementCost, Is.EqualTo(1000L));
            Assert.That(destination[new GridCoord(1, 0)].BaseWalkable, Is.True);
            Assert.That(destination[new GridCoord(1, 0)].BaseMovementCost, Is.EqualTo(2000L));
            Assert.That(destination[new GridCoord(2, 0)].BaseWalkable, Is.False);
            Assert.That(destination[new GridCoord(3, 0)].HasObstacle, Is.True);
            Assert.That(destination[new GridCoord(4, 0)].HasSurface, Is.False);
        }

        [Test]
        public void FailedSourceLeavesDestinationUnchanged()
        {
            GridRect bounds =
                new GridRect(
                    new GridCoord(0, 0),
                    new GridSize(3, 1));
            GridBakeCell sentinel =
                new GridBakeCell(
                    GridBakeFlags.HasSurface |
                    GridBakeFlags.BaseWalkable,
                    9f,
                    0f,
                    1000L,
                    3);
            DenseGrid<GridBakeCell> destination =
                new DenseGrid<GridBakeCell>(
                    bounds,
                    sentinel);
            GridProjectionBakeSettings settings =
                new GridProjectionBakeSettings(
                    bounds,
                    new Vector3(0f, 10f, 0f),
                    Vector2.one,
                    30f,
                    1000L,
                    2000L);
            GridBakeCell[] scratch =
                new GridBakeCell[destination.Count];

            GridProjectionBakeResult result =
                GridProjectionBaker.Bake(
                    new FailingSource(),
                    settings,
                    destination,
                    scratch);

            Assert.That(result.Error, Is.EqualTo(GridProjectionBakeError.SampleFailed));
            for (int i = 0; i < destination.Count; i++)
            {
                Assert.That(destination.GetRefReadOnlyByIndex(i).Height, Is.EqualTo(9f));
                Assert.That(destination.GetRefReadOnlyByIndex(i).BaseWalkable, Is.True);
            }
        }

        [Test]
        public void ManualOverrideCompositionKeepsHardSafetyExplicit()
        {
            GridBakeCell steepSurface =
                new GridBakeCell(
                    GridBakeFlags.HasSurface,
                    0f,
                    45f,
                    3000L,
                    1);
            GridBakeCell obstacle =
                new GridBakeCell(
                    GridBakeFlags.HasSurface |
                    GridBakeFlags.Obstacle,
                    0f,
                    0f,
                    1000L,
                    1);
            GridBakeCell missing =
                new GridBakeCell(
                    GridBakeFlags.None,
                    0f,
                    0f,
                    1000L,
                    0);
            GridTraversalOverrideCell force =
                GridTraversalOverrideCell.ForceWalkable(750L);
            GridTraversalOverrideCell blocked =
                GridTraversalOverrideCell.ForceBlocked();
            GridTraversalCompositionSettings strict =
                new GridTraversalCompositionSettings(
                    requireSampledSurface: true,
                    obstacleIsHardBlock: true);

            Assert.That(
                GridTraversalComposer.IsWalkable(
                    in steepSurface,
                    in force,
                    in strict),
                Is.True);
            Assert.That(
                GridTraversalComposer.GetMovementCost(
                    in steepSurface,
                    in force),
                Is.EqualTo(750L));
            Assert.That(
                GridTraversalComposer.IsWalkable(
                    in steepSurface,
                    in blocked,
                    in strict),
                Is.False);
            Assert.That(
                GridTraversalComposer.IsWalkable(
                    in missing,
                    in force,
                    in strict),
                Is.False);
            Assert.That(
                GridTraversalComposer.IsWalkable(
                    in obstacle,
                    in force,
                    in strict),
                Is.False);

            GridTraversalCompositionSettings obstacleOverrideAllowed =
                new GridTraversalCompositionSettings(
                    requireSampledSurface: true,
                    obstacleIsHardBlock: false);
            Assert.That(
                GridTraversalComposer.IsWalkable(
                    in obstacle,
                    in force,
                    in obstacleOverrideAllowed),
                Is.True);
        }

        [Test]
        public void TerrainSourceBakesRealTerrainData()
        {
            TerrainData data = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(32f, 10f, 32f)
            };
            float[,] heights = new float[33, 33];
            for (int y = 0; y < 33; y++)
                for (int x = 0; x < 33; x++)
                    heights[y, x] = 0.25f;
            data.SetHeights(0, 0, heights);
            GameObject terrainObject =
                Terrain.CreateTerrainGameObject(data);
            Terrain terrain =
                terrainObject.GetComponent<Terrain>();

            try
            {
                GridRect bounds =
                    new GridRect(
                        new GridCoord(-2, -2),
                        new GridSize(4, 4));
                GridProjectionBakeSettings settings =
                    new GridProjectionBakeSettings(
                        bounds,
                        new Vector3(0f, 20f, 0f),
                        new Vector2(8f, 8f),
                        10f,
                        1000L,
                        2000L,
                        steepSlopeThreshold: 5f,
                        maxDistance: 30f);
                DenseGrid<GridBakeCell> baked =
                    new DenseGrid<GridBakeCell>(bounds);
                GridBakeCell[] scratch =
                    new GridBakeCell[baked.Count];

                GridProjectionBakeResult result =
                    GridProjectionBaker.Bake(
                        new TerrainGridProjectionSource(
                            terrain,
                            surfaceCategory: 7),
                        settings,
                        baked,
                        scratch);

                Assert.That(result.Success, Is.True);
                Assert.That(result.WalkableCells, Is.EqualTo(16));
                Assert.That(result.MissingSurfaceCells, Is.EqualTo(0));
                Assert.That(
                    baked[new GridCoord(-2, -2)].Height,
                    Is.EqualTo(2.5f).Within(0.01f));
                Assert.That(
                    baked[new GridCoord(-2, -2)].SurfaceCategory,
                    Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(terrainObject);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void PhysicsSourceSamplesColliderAndExplicitObstacleMask()
        {
            GameObject cube =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.layer = 30;
            cube.transform.position =
                new Vector3(0f, -0.5f, 0f);
            cube.transform.localScale =
                new Vector3(10f, 1f, 10f);
            Physics.SyncTransforms();

            try
            {
                GridProjectionQuery query =
                    new GridProjectionQuery(
                        new GridCoord(0, 0),
                        new Vector3(0f, 5f, 0f),
                        Vector3.down,
                        10f);
                int defaultLayerMask =
                    1 << cube.layer;
                PhysicsGridProjectionSource surface =
                    new PhysicsGridProjectionSource(
                        defaultLayerMask);
                Assert.That(
                    surface.TrySample(
                        in query,
                        out GridProjectionSample sample),
                    Is.True);
                Assert.That(sample.Hit, Is.True);
                Assert.That(sample.ObstacleHit, Is.False);
                Assert.That(sample.Height, Is.EqualTo(0f).Within(0.01f));

                PhysicsGridProjectionSource obstacle =
                    new PhysicsGridProjectionSource(
                        0,
                        defaultLayerMask);
                Assert.That(
                    obstacle.TrySample(
                        in query,
                        out GridProjectionSample obstacleSample),
                    Is.True);
                Assert.That(obstacleSample.Hit, Is.True);
                Assert.That(obstacleSample.ObstacleHit, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void SettingsRejectInvalidAuthoringInsteadOfClamping()
        {
            GridRect bounds =
                new GridRect(
                    new GridCoord(0, 0),
                    new GridSize(2, 2));
            Assert.That(
                () => new GridProjectionBakeSettings(
                    bounds,
                    Vector3.zero,
                    new Vector2(0f, 1f),
                    30f,
                    1000L,
                    2000L),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new GridProjectionBakeSettings(
                    bounds,
                    Vector3.zero,
                    Vector2.one,
                    20f,
                    1000L,
                    2000L,
                    steepSlopeThreshold: 30f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new GridProjectionBakeSettings(
                    bounds,
                    Vector3.zero,
                    Vector2.one,
                    30f,
                    0L,
                    2000L),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private sealed class ClassificationSource :
            IGridProjectionSource
        {
            public bool TrySample(
                in GridProjectionQuery query,
                out GridProjectionSample sample)
            {
                switch (query.Coord.X)
                {
                    case 0:
                        sample = Surface(0f, false);
                        return true;
                    case 1:
                        sample = Surface(25f, false);
                        return true;
                    case 2:
                        sample = Surface(45f, false);
                        return true;
                    case 3:
                        sample = Surface(0f, true);
                        return true;
                    default:
                        sample = GridProjectionSample.Missing();
                        return true;
                }
            }

            private static GridProjectionSample Surface(
                float slope,
                bool obstacle) =>
                GridProjectionSample.Surface(
                    Vector3.zero,
                    Vector3.up,
                    0f,
                    slope,
                    5,
                    obstacle);
        }

        private sealed class FailingSource :
            IGridProjectionSource
        {
            public bool TrySample(
                in GridProjectionQuery query,
                out GridProjectionSample sample)
            {
                if (query.Coord.X == 1)
                {
                    sample = default(GridProjectionSample);
                    return false;
                }

                sample =
                    GridProjectionSample.Surface(
                        Vector3.zero,
                        Vector3.up,
                        0f,
                        0f,
                        1,
                        false);
                return true;
            }
        }
    }
}
