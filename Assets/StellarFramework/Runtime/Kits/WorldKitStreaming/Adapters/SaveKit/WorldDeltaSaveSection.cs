using System;
using StellarFramework;

namespace StellarFramework.WorldKit.Streaming.SaveKitAdapter
{
    public sealed class WorldDeltaSaveSection : SaveSection<WorldDeltaSnapshot>
    {
        private readonly WorldDeltaPersistenceState _state;

        public override SaveSectionId Id => SaveSectionId.From("world.runtime_deltas");
        public override MissingSectionPolicy MissingPolicy => MissingSectionPolicy.UseDefault;

        public WorldDeltaSaveSection(WorldDeltaPersistenceState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public override WorldDeltaSnapshot Capture(SaveCaptureContext context) =>
            _state.CaptureSnapshot();

        public override WorldDeltaSnapshot CreateDefault(SaveRestoreContext context) =>
            _state.CreateEmptySnapshot();

        public override SaveValidationResult Validate(
            WorldDeltaSnapshot data,
            SaveValidationContext context)
        {
            return _state.ValidateSnapshot(data, out string error)
                ? SaveValidationResult.Valid()
                : SaveValidationResult.Invalid("WorldDeltaSnapshotInvalid", error);
        }

        public override void Restore(
            WorldDeltaSnapshot data,
            SaveRestoreContext context)
        {
            _state.RestoreSnapshot(data);
        }
    }
}
