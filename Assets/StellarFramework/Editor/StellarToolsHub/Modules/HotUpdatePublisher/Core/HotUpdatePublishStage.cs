namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Publisher pipeline 的执行阶段和终态。</summary>
    public enum HotUpdatePublishStage
    {
        None = 0,
        Preflight = 1,
        ClassifyChanges = 2,
        CompileHotUpdate = 3,
        ExportHybridCLRAssets = 4,
        BuildYooAsset = 5,
        ValidateArtifacts = 6,
        RunReleaseGate = 7,
        PrepareUpload = 8,
        UploadFiles = 9,
        VerifyRemote = 10,
        PublishVersion = 11,
        Finalize = 12,
        Completed = 13,
        Failed = 14,
        Cancelled = 15
    }

    /// <summary>结构化失败代码，调用方不得通过错误文本推断结果。</summary>
    public enum HotUpdatePublishErrorCode
    {
        None = 0,
        MissingStageHandler = 1,
        StageFailed = 2,
        StageException = 3,
        InvalidStageResult = 4,
        Cancelled = 5,
        ArtifactValidationFailed = 6,
        ReleaseGatePolicyRejected = 7,
        ReleaseGateFailed = 8,
        FullReleaseGateUnavailable = 9
    }
}
