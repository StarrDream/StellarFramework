using System;

namespace StellarFramework.WorldGenKit.Resources
{
    public readonly struct WorldResourceBudgetEntry
    {
        public WorldResourceId ResourceId { get; }
        public int MaxAccepted { get; }

        public WorldResourceBudgetEntry(WorldResourceId resourceId, int maxAccepted)
        {
            if (!resourceId.IsValid) throw new ArgumentException("Resource ID must be valid.", nameof(resourceId));
            if (maxAccepted < 0) throw new ArgumentOutOfRangeException(nameof(maxAccepted));
            ResourceId = resourceId;
            MaxAccepted = maxAccepted;
        }
    }

    public readonly struct WorldResourceCategoryBudgetEntry
    {
        public WorldResourceCategoryId CategoryId { get; }
        public int MaxAccepted { get; }

        public WorldResourceCategoryBudgetEntry(WorldResourceCategoryId categoryId, int maxAccepted)
        {
            if (!categoryId.IsValid) throw new ArgumentException("Category ID must be valid.", nameof(categoryId));
            if (maxAccepted < 0) throw new ArgumentOutOfRangeException(nameof(maxAccepted));
            CategoryId = categoryId;
            MaxAccepted = maxAccepted;
        }
    }

    public sealed class WorldResourceBudget
    {
        private readonly WorldResourceCategoryBudgetEntry[] _categoryEntries;
        private readonly WorldResourceBudgetEntry[] _resourceEntries;

        public int GlobalMaxAccepted { get; }
        public ReadOnlySpan<WorldResourceCategoryBudgetEntry> CategoryBudgets => _categoryEntries;
        public ReadOnlySpan<WorldResourceBudgetEntry> ResourceBudgets => _resourceEntries;

        public WorldResourceBudget(
            int globalMaxAccepted = -1,
            ReadOnlySpan<WorldResourceCategoryBudgetEntry> categoryBudgets = default(ReadOnlySpan<WorldResourceCategoryBudgetEntry>),
            ReadOnlySpan<WorldResourceBudgetEntry> resourceBudgets = default(ReadOnlySpan<WorldResourceBudgetEntry>))
        {
            if (globalMaxAccepted < -1) throw new ArgumentOutOfRangeException(nameof(globalMaxAccepted));
            GlobalMaxAccepted = globalMaxAccepted;
            _categoryEntries = categoryBudgets.ToArray();
            _resourceEntries = resourceBudgets.ToArray();
            ValidateUniqueCategoryEntries(_categoryEntries);
            ValidateUniqueResourceEntries(_resourceEntries);
        }

        public WorldCompiledResourceBudget Compile(WorldResourceCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            int[] resourceMax = new int[catalog.Count];
            int[] categoryMax = new int[catalog.CategoryCount];
            Fill(resourceMax, -1);
            Fill(categoryMax, -1);

            for (int i = 0; i < _resourceEntries.Length; i++)
            {
                if (!catalog.TryGetIndex(_resourceEntries[i].ResourceId, out int resourceIndex))
                    throw new InvalidOperationException("Budget references unknown resource: " + _resourceEntries[i].ResourceId);
                resourceMax[resourceIndex] = _resourceEntries[i].MaxAccepted;
            }

            for (int i = 0; i < _categoryEntries.Length; i++)
            {
                if (!catalog.TryGetCategoryIndex(_categoryEntries[i].CategoryId, out int categoryIndex))
                    throw new InvalidOperationException("Budget references unknown category: " + _categoryEntries[i].CategoryId);
                categoryMax[categoryIndex] = _categoryEntries[i].MaxAccepted;
            }

            return new WorldCompiledResourceBudget(GlobalMaxAccepted, resourceMax, categoryMax);
        }

        private static void ValidateUniqueCategoryEntries(WorldResourceCategoryBudgetEntry[] entries)
        {
            for (int i = 0; i < entries.Length; i++)
                for (int j = i + 1; j < entries.Length; j++)
                    if (entries[i].CategoryId == entries[j].CategoryId)
                        throw new ArgumentException("Duplicate category budget: " + entries[i].CategoryId);
        }

        private static void ValidateUniqueResourceEntries(WorldResourceBudgetEntry[] entries)
        {
            for (int i = 0; i < entries.Length; i++)
                for (int j = i + 1; j < entries.Length; j++)
                    if (entries[i].ResourceId == entries[j].ResourceId)
                        throw new ArgumentException("Duplicate resource budget: " + entries[i].ResourceId);
        }

        private static void Fill(int[] values, int value)
        {
            for (int i = 0; i < values.Length; i++) values[i] = value;
        }
    }

    public sealed class WorldCompiledResourceBudget
    {
        private readonly int[] _resourceMax;
        private readonly int[] _categoryMax;

        public int GlobalMaxAccepted { get; }
        public int ResourceCount => _resourceMax.Length;
        public int CategoryCount => _categoryMax.Length;

        internal WorldCompiledResourceBudget(int globalMaxAccepted, int[] resourceMax, int[] categoryMax)
        {
            GlobalMaxAccepted = globalMaxAccepted;
            _resourceMax = resourceMax;
            _categoryMax = categoryMax;
        }

        internal int GetResourceMax(int resourceIndex) => _resourceMax[resourceIndex];
        internal int GetCategoryMax(int categoryIndex) => _categoryMax[categoryIndex];
    }
}
