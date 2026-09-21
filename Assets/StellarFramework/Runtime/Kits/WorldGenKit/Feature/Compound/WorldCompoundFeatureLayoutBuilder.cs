using System;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldCompoundElementInstanceData
    {
        public WorldCompoundSlotId SlotId { get; }
        public WorldCompoundElementTypeId ElementTypeId { get; }
        public double X { get; }
        public double Y { get; }
        public double RotationDegrees { get; }
        public ulong ParentDeterministicKey { get; }
        public int ElementIndex { get; }

        internal WorldCompoundElementInstanceData(
            WorldCompoundSlotId slotId,
            WorldCompoundElementTypeId elementTypeId,
            double x,
            double y,
            double rotationDegrees,
            ulong parentDeterministicKey,
            int elementIndex)
        {
            SlotId = slotId;
            ElementTypeId = elementTypeId;
            X = x;
            Y = y;
            RotationDegrees = rotationDegrees;
            ParentDeterministicKey = parentDeterministicKey;
            ElementIndex = elementIndex;
        }
    }

    public static class WorldCompoundFeatureLayoutBuilder
    {
        public static int Build(
            in WorldFeatureCandidate parent,
            WorldFeatureCatalog catalog,
            WorldCompiledCompoundFeatureProfile profile,
            Span<WorldCompoundElementInstanceData> destination)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if ((uint)parent.FeatureIndex >= (uint)catalog.Count)
                throw new ArgumentOutOfRangeException(nameof(parent));
            if (profile.FeatureCount != catalog.Count)
                throw new InvalidOperationException("Compiled compound profile and feature catalog do not match.");
            WorldFeatureDefinition definition = catalog.GetDefinition(parent.FeatureIndex);
            if (definition.Kind != WorldFeatureKind.Compound)
                throw new InvalidOperationException("Only Compound features can build a compound layout.");
            if (!profile.TryGetTemplate(parent.FeatureIndex, out WorldCompoundFeatureTemplate template))
                return 0;
            if (destination.Length < template.Count)
                throw new ArgumentException("Destination is smaller than the compound template element count.", nameof(destination));

            double radians = parent.RotationDegrees * (Math.PI / 180d);
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            ReadOnlySpan<WorldCompoundFeatureElement> elements = template.Elements;
            for (int i = 0; i < elements.Length; i++)
            {
                WorldCompoundFeatureElement element = elements[i];
                double worldX = parent.X + (element.LocalX * cos) - (element.LocalY * sin);
                double worldY = parent.Y + (element.LocalX * sin) + (element.LocalY * cos);
                double rotation = NormalizeDegrees(parent.RotationDegrees + element.LocalRotationDegrees);
                destination[i] = new WorldCompoundElementInstanceData(
                    element.SlotId,
                    element.ElementTypeId,
                    worldX,
                    worldY,
                    rotation,
                    parent.DeterministicKey,
                    i);
            }
            return elements.Length;
        }

        private static double NormalizeDegrees(double value)
        {
            double normalized = value % 360d;
            return normalized < 0d ? normalized + 360d : normalized;
        }
    }
}
