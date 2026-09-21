using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldResourceModifierEntry
    {
        public WorldResourceId ResourceId { get; }
        public WorldResourceGenerationMultiplier Multiplier { get; }

        public WorldResourceModifierEntry(WorldResourceId resourceId, WorldResourceGenerationMultiplier multiplier)
        {
            if (!resourceId.IsValid) throw new ArgumentException("Resource ID must be valid.", nameof(resourceId));
            ResourceId = resourceId;
            Multiplier = multiplier;
        }
    }

    public readonly struct WorldResourceCategoryModifierEntry
    {
        public WorldResourceCategoryId CategoryId { get; }
        public WorldResourceGenerationMultiplier Multiplier { get; }

        public WorldResourceCategoryModifierEntry(WorldResourceCategoryId categoryId, WorldResourceGenerationMultiplier multiplier)
        {
            if (!categoryId.IsValid) throw new ArgumentException("Resource category ID must be valid.", nameof(categoryId));
            CategoryId = categoryId;
            Multiplier = multiplier;
        }
    }

    public readonly struct WorldResolvedResourceGenerationSettings
    {
        public WorldResourceDistributionMode Mode { get; }
        public double Occurrence { get; }
        public int ClusterSize { get; }
        public double Richness { get; }
        public double MinSpacing { get; }

        internal WorldResolvedResourceGenerationSettings(
            WorldResourceDistributionMode mode,
            double occurrence,
            int clusterSize,
            double richness,
            double minSpacing)
        {
            Mode = mode;
            Occurrence = occurrence;
            ClusterSize = clusterSize;
            Richness = richness;
            MinSpacing = minSpacing;
        }
    }

    public sealed class WorldResourceGenerationSettings
    {
        private readonly WorldResourceCategoryModifierEntry[] _categoryEntries;
        private readonly WorldResourceModifierEntry[] _resourceEntries;
        private readonly Dictionary<WorldResourceCategoryId, WorldResourceGenerationMultiplier> _categoryModifiers;
        private readonly Dictionary<WorldResourceId, WorldResourceGenerationMultiplier> _resourceModifiers;

        public WorldResourceGenerationMultiplier GlobalMultiplier { get; }
        public WorldResourceGenerationApplicationPolicy ApplicationPolicy { get; }
        public ReadOnlySpan<WorldResourceCategoryModifierEntry> CategoryModifiers => _categoryEntries;
        public ReadOnlySpan<WorldResourceModifierEntry> ResourceModifiers => _resourceEntries;

        public WorldResourceGenerationSettings(
            WorldResourceGenerationMultiplier globalMultiplier,
            ReadOnlySpan<WorldResourceCategoryModifierEntry> categoryModifiers = default(ReadOnlySpan<WorldResourceCategoryModifierEntry>),
            ReadOnlySpan<WorldResourceModifierEntry> resourceModifiers = default(ReadOnlySpan<WorldResourceModifierEntry>),
            WorldResourceGenerationApplicationPolicy applicationPolicy = WorldResourceGenerationApplicationPolicy.NewChunksOnly)
        {
            if ((int)applicationPolicy < (int)WorldResourceGenerationApplicationPolicy.NewChunksOnly ||
                (int)applicationPolicy > (int)WorldResourceGenerationApplicationPolicy.FullRegenerate)
                throw new ArgumentOutOfRangeException(nameof(applicationPolicy));

            GlobalMultiplier = globalMultiplier;
            ApplicationPolicy = applicationPolicy;
            _categoryEntries = categoryModifiers.ToArray();
            _resourceEntries = resourceModifiers.ToArray();
            _categoryModifiers = new Dictionary<WorldResourceCategoryId, WorldResourceGenerationMultiplier>(_categoryEntries.Length);
            _resourceModifiers = new Dictionary<WorldResourceId, WorldResourceGenerationMultiplier>(_resourceEntries.Length);

            for (int i = 0; i < _categoryEntries.Length; i++)
            {
                WorldResourceCategoryModifierEntry entry = _categoryEntries[i];
                if (_categoryModifiers.ContainsKey(entry.CategoryId))
                    throw new ArgumentException("Duplicate resource category modifier: " + entry.CategoryId, nameof(categoryModifiers));
                _categoryModifiers.Add(entry.CategoryId, entry.Multiplier);
            }

            for (int i = 0; i < _resourceEntries.Length; i++)
            {
                WorldResourceModifierEntry entry = _resourceEntries[i];
                if (_resourceModifiers.ContainsKey(entry.ResourceId))
                    throw new ArgumentException("Duplicate resource modifier: " + entry.ResourceId, nameof(resourceModifiers));
                _resourceModifiers.Add(entry.ResourceId, entry.Multiplier);
            }
        }

        public WorldResolvedResourceGenerationSettings Resolve(WorldResourceDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            WorldResourceGenerationMultiplier effective = GlobalMultiplier;
            if (_categoryModifiers.TryGetValue(definition.CategoryId, out WorldResourceGenerationMultiplier category))
                effective = effective.Multiply(in category);
            if (_resourceModifiers.TryGetValue(definition.Id, out WorldResourceGenerationMultiplier resource))
                effective = effective.Multiply(in resource);

            double occurrence = definition.Distribution.Occurrence * effective.Occurrence;
            if (occurrence > 1d) occurrence = 1d;

            double clusterValue = definition.Distribution.ClusterSize * effective.ClusterSize;
            int clusterSize = clusterValue >= int.MaxValue
                ? int.MaxValue
                : Math.Max(1, (int)Math.Round(clusterValue, MidpointRounding.AwayFromZero));
            double richness = definition.Distribution.Richness * effective.Richness;
            if (double.IsInfinity(richness)) throw new OverflowException("Resolved resource richness overflowed.");

            return new WorldResolvedResourceGenerationSettings(
                definition.Distribution.Mode,
                occurrence,
                clusterSize,
                richness,
                definition.Distribution.MinSpacing);
        }
    }
}
