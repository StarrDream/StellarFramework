namespace StellarFramework.WorldGenKit
{
    public enum WorldGenerationDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct WorldGenerationDiagnostic
    {
        public WorldGenerationDiagnosticSeverity Severity { get; }
        public WorldGenerationDiagnosticId Code { get; }
        public WorldGenerationStageId StageId { get; }
        public WorldDataChannelId ChannelId { get; }
        public string Message { get; }

        public WorldGenerationDiagnostic(
            WorldGenerationDiagnosticSeverity severity,
            WorldGenerationDiagnosticId code,
            WorldGenerationStageId stageId,
            WorldDataChannelId channelId,
            string message)
        {
            Severity = severity;
            Code = code;
            StageId = stageId;
            ChannelId = channelId;
            Message = message;
        }
    }
}
