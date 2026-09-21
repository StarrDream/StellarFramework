using System;
using System.Collections.Generic;

namespace StellarFramework.WorldGenKit.Resources
{
    public enum WorldResourceExposureValidationError
    {
        None = 0,
        GlobalMultiplierNotAllowed = 1,
        CategoryNotExposed = 2,
        CategoryMultiplierNotAllowed = 3,
        ResourceNotExposed = 4,
        ResourceMultiplierNotAllowed = 5
    }

    public sealed class WorldResourceGenerationExposureProfile
    {
        private readonly Dictionary<WorldResourceCategoryId, WorldResourceGenerationExposure> _categoryExposure;
        private readonly Dictionary<WorldResourceId, WorldResourceGenerationExposure> _resourceExposure;

        public WorldResourceGenerationExposure GlobalExposure { get; }

        public WorldResourceGenerationExposureProfile(
            WorldResourceGenerationExposure globalExposure,
            ReadOnlySpan<WorldResourceCategoryExposureEntry> categoryExposure = default(ReadOnlySpan<WorldResourceCategoryExposureEntry>),
            ReadOnlySpan<WorldResourceExposureEntry> resourceExposure = default(ReadOnlySpan<WorldResourceExposureEntry>))
        {
            GlobalExposure = globalExposure;
            _categoryExposure = new Dictionary<WorldResourceCategoryId, WorldResourceGenerationExposure>(categoryExposure.Length);
            _resourceExposure = new Dictionary<WorldResourceId, WorldResourceGenerationExposure>(resourceExposure.Length);

            for (int i = 0; i < categoryExposure.Length; i++)
            {
                WorldResourceCategoryExposureEntry entry = categoryExposure[i];
                if (_categoryExposure.ContainsKey(entry.CategoryId))
                    throw new ArgumentException("Duplicate category exposure: " + entry.CategoryId, nameof(categoryExposure));
                _categoryExposure.Add(entry.CategoryId, entry.Exposure);
            }

            for (int i = 0; i < resourceExposure.Length; i++)
            {
                WorldResourceExposureEntry entry = resourceExposure[i];
                if (_resourceExposure.ContainsKey(entry.ResourceId))
                    throw new ArgumentException("Duplicate resource exposure: " + entry.ResourceId, nameof(resourceExposure));
                _resourceExposure.Add(entry.ResourceId, entry.Exposure);
            }
        }

        public bool Validate(
            WorldResourceGenerationSettings settings,
            out WorldResourceExposureValidationError error,
            out int entryIndex)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            WorldResourceGenerationMultiplier globalMultiplier = settings.GlobalMultiplier;
            if (!GlobalExposure.Allows(in globalMultiplier))
            {
                error = WorldResourceExposureValidationError.GlobalMultiplierNotAllowed;
                entryIndex = -1;
                return false;
            }

            ReadOnlySpan<WorldResourceCategoryModifierEntry> categories = settings.CategoryModifiers;
            for (int i = 0; i < categories.Length; i++)
            {
                if (!_categoryExposure.TryGetValue(categories[i].CategoryId, out WorldResourceGenerationExposure exposure))
                {
                    error = WorldResourceExposureValidationError.CategoryNotExposed;
                    entryIndex = i;
                    return false;
                }
                WorldResourceGenerationMultiplier multiplier = categories[i].Multiplier;
                if (!exposure.Allows(in multiplier))
                {
                    error = WorldResourceExposureValidationError.CategoryMultiplierNotAllowed;
                    entryIndex = i;
                    return false;
                }
            }

            ReadOnlySpan<WorldResourceModifierEntry> resources = settings.ResourceModifiers;
            for (int i = 0; i < resources.Length; i++)
            {
                if (!_resourceExposure.TryGetValue(resources[i].ResourceId, out WorldResourceGenerationExposure exposure))
                {
                    error = WorldResourceExposureValidationError.ResourceNotExposed;
                    entryIndex = i;
                    return false;
                }
                WorldResourceGenerationMultiplier multiplier = resources[i].Multiplier;
                if (!exposure.Allows(in multiplier))
                {
                    error = WorldResourceExposureValidationError.ResourceMultiplierNotAllowed;
                    entryIndex = i;
                    return false;
                }
            }

            error = WorldResourceExposureValidationError.None;
            entryIndex = -1;
            return true;
        }
    }
}
