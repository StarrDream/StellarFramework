using System;
using StellarFramework;
using StellarFramework.WorldGenKit.Feature.WorldKitAdapter;

namespace StellarFramework.WorldGenKit.Feature.SaveKitAdapter
{
    public sealed class WorldFeatureUsageSaveSection : SaveSection<WorldFeatureUsageSnapshot>
    {
        private readonly WorldFeatureUsageState _state;

        public override SaveSectionId Id =>
            SaveSectionId.From("worldgen.feature.usage");
        public override MissingSectionPolicy MissingPolicy =>
            MissingSectionPolicy.UseDefault;

        public WorldFeatureUsageSaveSection(WorldFeatureUsageState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public override WorldFeatureUsageSnapshot Capture(SaveCaptureContext context) =>
            _state.CaptureSnapshot();

        public override WorldFeatureUsageSnapshot CreateDefault(SaveRestoreContext context) =>
            new WorldFeatureUsageSnapshot();

        public override SaveValidationResult Validate(
            WorldFeatureUsageSnapshot data,
            SaveValidationContext context)
        {
            return _state.ValidateSnapshot(data, out string error)
                ? SaveValidationResult.Valid()
                : SaveValidationResult.Invalid(
                    "WorldFeatureUsageInvalid",
                    error);
        }

        public override void Restore(
            WorldFeatureUsageSnapshot data,
            SaveRestoreContext context)
        {
            _state.RestoreSnapshot(data);
        }
    }
}
