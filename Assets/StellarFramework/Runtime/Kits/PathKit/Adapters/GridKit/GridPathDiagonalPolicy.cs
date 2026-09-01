namespace StellarFramework
{
    /// <summary>Controls whether diagonal traversal may pass through a blocked corner.</summary>
    public enum GridPathDiagonalPolicy
    {
        NoCornerCut = 0,
        AllowCornerCut = 1
    }
}
