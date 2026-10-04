namespace StellarFramework.Res.CodeUpdate.HybridCLR
{
    /// <summary>
    /// HybridCLR code-update runner state.
    /// Resource package/version/download work is intentionally outside this state machine.
    /// </summary>
    public enum HybridCLRUpdateState
    {
        None,
        LoadingManifest,
        LoadingBytes,
        LoadingMetadata,
        LoadingAssembly,
        EnteredHotUpdate,
        Failed
    }

    /// <summary>
    /// Result of one HybridCLR code-update startup run.
    /// </summary>
    public struct HybridCLRUpdateResult
    {
        public bool Success;
        public HybridCLRUpdateState State;
        public string Error;
        public string LoadedAssemblyFullName;
        public string[] LoadedAotMetadataKeys;
        public HotUpdateManifest Manifest;
        public string ManifestSource;
    }

}
