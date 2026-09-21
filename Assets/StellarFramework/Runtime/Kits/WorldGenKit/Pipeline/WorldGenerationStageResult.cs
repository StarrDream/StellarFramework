namespace StellarFramework.WorldGenKit
{
    public enum WorldGenerationStageStatus
    {
        Succeeded = 0,
        Failed = 1,
        Cancelled = 2
    }

    public readonly struct WorldGenerationStageResult
    {
        public WorldGenerationStageStatus Status { get; }
        public WorldGenerationDiagnosticId Code { get; }
        public string Message { get; }
        public bool Success => Status == WorldGenerationStageStatus.Succeeded;

        private WorldGenerationStageResult(
            WorldGenerationStageStatus status,
            WorldGenerationDiagnosticId code,
            string message)
        {
            Status = status;
            Code = code;
            Message = message;
        }

        public static WorldGenerationStageResult Succeeded() =>
            new WorldGenerationStageResult(WorldGenerationStageStatus.Succeeded, default(WorldGenerationDiagnosticId), null);

        public static WorldGenerationStageResult Failed(WorldGenerationDiagnosticId code, string message = null) =>
            new WorldGenerationStageResult(WorldGenerationStageStatus.Failed, code, message);

        public static WorldGenerationStageResult Cancelled(WorldGenerationDiagnosticId code, string message = null) =>
            new WorldGenerationStageResult(WorldGenerationStageStatus.Cancelled, code, message);
    }
}
