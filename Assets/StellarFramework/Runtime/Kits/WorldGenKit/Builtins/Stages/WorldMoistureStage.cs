namespace StellarFramework.WorldGenKit.Builtins
{
    public sealed class WorldMoistureStage : IWorldGenerationStage
    {
        public static readonly WorldGenerationStageId StableId = WorldGenerationStageId.From("stage.builtin.moisture");

        private readonly ChannelHandle<float> _output;
        private readonly WorldPlanarSampleLayout _layout;
        private readonly WorldFractalNoiseSettings _noise;

        public WorldGenerationStageId Id => StableId;

        public WorldMoistureStage(
            ChannelHandle<float> output,
            WorldPlanarSampleLayout layout,
            WorldFractalNoiseSettings noise)
        {
            _output = output;
            _layout = layout;
            _noise = noise;
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
                0f,
                1f);
    }
}
