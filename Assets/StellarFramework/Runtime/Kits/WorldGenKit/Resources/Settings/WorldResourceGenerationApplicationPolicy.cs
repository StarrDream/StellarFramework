namespace StellarFramework.WorldGenKit.Resources
{
    public enum WorldResourceGenerationApplicationPolicy
    {
        NewChunksOnly = 0,
        UnvisitedChunks = 1,
        NonModifiedChunks = 2,
        ExplicitRegion = 3,
        FullRegenerate = 4
    }
}
