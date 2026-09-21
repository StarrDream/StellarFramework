using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class GridTopologyTests
    {
        [Test]
        public void OrthogonalTopologiesPreserveExistingNeighborOrderAndDistance()
        {
            GridCoord center = new GridCoord(10, -4);

            Span<GridCoord> fourBuffer = stackalloc GridCoord[4];
            Orthogonal4Topology four = new Orthogonal4Topology();
            Assert.That(four.WriteNeighbors(center, fourBuffer), Is.EqualTo(4));
            Assert.That(fourBuffer[0], Is.EqualTo(new GridCoord(10, -3)));
            Assert.That(fourBuffer[1], Is.EqualTo(new GridCoord(11, -4)));
            Assert.That(fourBuffer[2], Is.EqualTo(new GridCoord(10, -5)));
            Assert.That(fourBuffer[3], Is.EqualTo(new GridCoord(9, -4)));
            Assert.That(four.GetDistance(new GridCoord(0, 0), new GridCoord(3, -2)), Is.EqualTo(5));

            Span<GridCoord> eightBuffer = stackalloc GridCoord[8];
            Orthogonal8Topology eight = new Orthogonal8Topology();
            Assert.That(eight.WriteNeighbors(center, eightBuffer), Is.EqualTo(8));
            Assert.That(eightBuffer[0], Is.EqualTo(new GridCoord(10, -3)));
            Assert.That(eightBuffer[1], Is.EqualTo(new GridCoord(11, -3)));
            Assert.That(eightBuffer[7], Is.EqualTo(new GridCoord(9, -3)));
            Assert.That(eight.GetDistance(new GridCoord(0, 0), new GridCoord(3, -2)), Is.EqualTo(3));
        }

        [Test]
        public void TopologiesRejectUndersizedCallerBuffers()
        {
            Orthogonal4Topology four = new Orthogonal4Topology();
            Orthogonal8Topology eight = new Orthogonal8Topology();
            HexTopology hex = new HexTopology();

            Assert.That(() => four.WriteNeighbors(default(GridCoord), new GridCoord[3].AsSpan()),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => eight.WriteNeighbors(default(GridCoord), new GridCoord[7].AsSpan()),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => hex.WriteNeighbors(default(HexCoord), new HexCoord[5].AsSpan()),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void HexNeighborsUseStableAxialOrderAndSupportNegativeCoordinates()
        {
            HexCoord center = new HexCoord(-3, 5);
            HexTopology topology = new HexTopology();
            Span<HexCoord> buffer = stackalloc HexCoord[6];

            Assert.That(topology.WriteNeighbors(center, buffer), Is.EqualTo(6));
            Assert.That(buffer[0], Is.EqualTo(new HexCoord(-2, 5)));
            Assert.That(buffer[1], Is.EqualTo(new HexCoord(-2, 4)));
            Assert.That(buffer[2], Is.EqualTo(new HexCoord(-3, 4)));
            Assert.That(buffer[3], Is.EqualTo(new HexCoord(-4, 5)));
            Assert.That(buffer[4], Is.EqualTo(new HexCoord(-4, 6)));
            Assert.That(buffer[5], Is.EqualTo(new HexCoord(-3, 6)));

            Assert.That(topology.GetDistance(center, center), Is.EqualTo(0));
            Assert.That(topology.GetDistance(new HexCoord(0, 0), new HexCoord(3, -2)), Is.EqualTo(3));
            Assert.That(topology.GetDistance(new HexCoord(3, -2), new HexCoord(0, 0)), Is.EqualTo(3));
        }

        [Test]
        public void HexNeighborOverflowIsExplicit()
        {
            HexTopology topology = new HexTopology();
            HexCoord maxQ = new HexCoord(int.MaxValue, 0);

            Assert.That(topology.TryGetNeighbor(maxQ, HexDirection.East, out _), Is.False);
            Assert.That(() => topology.GetNeighbor(maxQ, HexDirection.East), Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void HexRingHasExpectedCountUniquenessAndDistance()
        {
            HexTopology topology = new HexTopology();
            HexCoord center = new HexCoord(2, -3);
            HexCoord[] radiusZero = new HexCoord[1];
            Assert.That(topology.WriteRing(center, 0, radiusZero.AsSpan()), Is.EqualTo(1));
            Assert.That(radiusZero[0], Is.EqualTo(center));

            const int radius = 3;
            HexCoord[] ring = new HexCoord[(int)HexTopology.GetRingCellCount(radius)];
            int written = topology.WriteRing(center, radius, ring.AsSpan());
            Assert.That(written, Is.EqualTo(18));

            HashSet<HexCoord> unique = new HashSet<HexCoord>(ring);
            Assert.That(unique.Count, Is.EqualTo(written));
            for (int i = 0; i < written; i++)
            {
                Assert.That(topology.GetDistance(center, ring[i]), Is.EqualTo(radius));
            }
        }

        [Test]
        public void HexRangeHasExpectedCountUniquenessAndMaximumDistance()
        {
            HexTopology topology = new HexTopology();
            HexCoord center = new HexCoord(-5, -7);
            const int radius = 2;
            HexCoord[] range = new HexCoord[(int)HexTopology.GetRangeCellCount(radius)];
            int written = topology.WriteRange(center, radius, range.AsSpan());

            Assert.That(written, Is.EqualTo(19));
            HashSet<HexCoord> unique = new HashSet<HexCoord>(range);
            Assert.That(unique.Count, Is.EqualTo(written));
            Assert.That(unique.Contains(center), Is.True);
            for (int i = 0; i < written; i++)
            {
                Assert.That(topology.GetDistance(center, range[i]), Is.LessThanOrEqualTo(radius));
            }
        }

        [Test]
        public void HexRegionValidationIsAtomicBeforeWriting()
        {
            HexTopology topology = new HexTopology();
            HexCoord[] ring = new HexCoord[6];
            for (int i = 0; i < ring.Length; i++) ring[i] = new HexCoord(123, 456);

            Assert.That(() => topology.WriteRing(new HexCoord(int.MaxValue, 0), 1, ring.AsSpan()),
                Throws.TypeOf<OverflowException>());
            for (int i = 0; i < ring.Length; i++)
            {
                Assert.That(ring[i], Is.EqualTo(new HexCoord(123, 456)));
            }

            Assert.That(() => topology.WriteRange(default(HexCoord), -1, Span<HexCoord>.Empty),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void HexEdgesAreCanonicalAndSharedByAdjacentCells()
        {
            HexTopology topology = new HexTopology();
            HexCoord center = new HexCoord(0, 0);
            HexCoord east = topology.GetNeighbor(center, HexDirection.East);

            HexEdge fromCenter = topology.GetEdge(center, HexDirection.East);
            HexEdge fromNeighbor = topology.GetEdge(east, HexDirection.West);
            Assert.That(fromCenter, Is.EqualTo(fromNeighbor));
            Assert.That(fromCenter.IsValid, Is.True);
            Assert.That(topology.TryGetAdjacentCells(fromCenter, out HexCoord first, out HexCoord second), Is.True);
            Assert.That(topology.GetDistance(first, second), Is.EqualTo(1));

            Span<HexEdge> edges = stackalloc HexEdge[6];
            Assert.That(topology.WriteEdges(center, edges), Is.EqualTo(6));
            HashSet<HexEdge> unique = new HashSet<HexEdge>();
            for (int i = 0; i < edges.Length; i++) unique.Add(edges[i]);
            Assert.That(unique.Count, Is.EqualTo(6));
        }

        [Test]
        public void HexVerticesAreCanonicalAcrossSharingCells()
        {
            HexTopology topology = new HexTopology();
            HexCoord center = new HexCoord(0, 0);
            HexCoord east = topology.GetNeighbor(center, HexDirection.East);

            HexVertex fromCenter = topology.GetVertex(center, 0);
            HexVertex fromEast = topology.GetVertex(east, 2);
            Assert.That(fromCenter, Is.EqualTo(fromEast));
            Assert.That(fromCenter.IsValid, Is.True);

            Span<HexVertex> vertices = stackalloc HexVertex[6];
            Assert.That(topology.WriteVertices(center, vertices), Is.EqualTo(6));
            HashSet<HexVertex> unique = new HashSet<HexVertex>();
            for (int i = 0; i < vertices.Length; i++) unique.Add(vertices[i]);
            Assert.That(unique.Count, Is.EqualTo(6));

            Span<HexCoord> adjacent = stackalloc HexCoord[3];
            Assert.That(topology.WriteAdjacentCells(fromCenter, adjacent), Is.EqualTo(3));
            Assert.That(adjacent[0] == center || adjacent[1] == center || adjacent[2] == center, Is.True);
            for (int i = 0; i < adjacent.Length; i++)
            {
                for (int j = i + 1; j < adjacent.Length; j++)
                {
                    Assert.That(topology.GetDistance(adjacent[i], adjacent[j]), Is.EqualTo(1));
                }
            }
        }
    }
}
