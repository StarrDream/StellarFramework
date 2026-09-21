namespace StellarFramework.WorldGenKit
{
    public interface IWorldGenerationStage
    {
        WorldGenerationStageId Id { get; }
        void Describe(WorldGenerationStageDescriptorBuilder builder);
        WorldGenerationStageResult Execute(in WorldGenerationContext context);
    }
}
