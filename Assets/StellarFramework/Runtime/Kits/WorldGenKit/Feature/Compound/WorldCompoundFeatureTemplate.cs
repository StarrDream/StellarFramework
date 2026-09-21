using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit.Feature
{
    public readonly struct WorldCompoundFeatureElement
    {
        public WorldCompoundSlotId SlotId { get; }
        public WorldCompoundElementTypeId ElementTypeId { get; }
        public double LocalX { get; }
        public double LocalY { get; }
        public double LocalRotationDegrees { get; }

        public WorldCompoundFeatureElement(
            WorldCompoundSlotId slotId,
            WorldCompoundElementTypeId elementTypeId,
            double localX,
            double localY,
            double localRotationDegrees = 0d)
        {
            if (!slotId.IsValid) throw new ArgumentException("Compound slot ID must be valid.", nameof(slotId));
            if (!elementTypeId.IsValid) throw new ArgumentException("Compound element type ID must be valid.", nameof(elementTypeId));
            ValidateFinite(localX, nameof(localX));
            ValidateFinite(localY, nameof(localY));
            ValidateFinite(localRotationDegrees, nameof(localRotationDegrees));
            SlotId = slotId;
            ElementTypeId = elementTypeId;
            LocalX = localX;
            LocalY = localY;
            LocalRotationDegrees = localRotationDegrees;
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public sealed class WorldCompoundFeatureTemplate
    {
        private readonly WorldCompoundFeatureElement[] _elements;
        public WorldCompoundTemplateId Id { get; }
        public ReadOnlySpan<WorldCompoundFeatureElement> Elements => _elements;
        public int Count => _elements.Length;

        public WorldCompoundFeatureTemplate(
            WorldCompoundTemplateId id,
            ReadOnlySpan<WorldCompoundFeatureElement> elements)
        {
            if (!id.IsValid) throw new ArgumentException("Compound template ID must be valid.", nameof(id));
            if (elements.Length == 0) throw new ArgumentException("Compound template must contain at least one element.", nameof(elements));
            Id = id;
            _elements = elements.ToArray();
            HashSet<WorldCompoundSlotId> slots = new HashSet<WorldCompoundSlotId>();
            for (int i = 0; i < _elements.Length; i++)
            {
                if (!slots.Add(_elements[i].SlotId))
                    throw new ArgumentException("Duplicate compound slot ID: " + _elements[i].SlotId, nameof(elements));
            }
        }
    }
}
