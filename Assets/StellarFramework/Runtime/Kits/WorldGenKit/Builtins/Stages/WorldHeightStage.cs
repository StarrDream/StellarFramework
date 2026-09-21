namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldHeightStage : IWorldGenerationStage
    {
        public static readonly WorldGenerationStageId StableId = WorldGenerationStageId.From("stage.builtin.height");

        private readonly ChannelHandle<float> _output;
        private readonly WorldPlanarSampleLayout _layout;
        private readonly WorldFractalNoiseSettings _noise;
        private readonly float _minHeight;
        private readonly float _maxHeight;

        public WorldGenerationStageId Id => StableId;

        public WorldHeightStage(
            ChannelHandle<float> output,
            WorldPlanarSampleLayout layout,
            WorldFractalNoiseSettings noise,
            float minHeight,
            float maxHeight)
        {
            WorldNoiseFieldStageUtility.ValidateRange(minHeight, maxHeight);
            _output = output;
            _layout = layout;
            _noise = noise;
            _minHeight = minHeight;
            _maxHeight = maxHeight;
        }

        public void Describe(WorldGenerationStageDescriptorBuilder builder)
        {
            builder.SetSeedScope(WorldGenerationSeedScope.World);
            builder.Produce(_output);
        }

        public WorldGenerationStageResult Execute(in WorldGenerationContext context) =>
            WorldNoiseFieldStageUtility.Fill(
                in context,
                _output,
                in _layout,
                in _noise,
                _minHeight,
                _maxHeight);
    }
}
