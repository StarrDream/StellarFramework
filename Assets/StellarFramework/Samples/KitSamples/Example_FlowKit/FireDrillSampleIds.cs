namespace StellarFramework.Samples.FlowKit
{
    /// <summary>消防演练 Sample 的稳定业务契约 ID。业务项目建议采用同样的集中定义方式。</summary>
    public static class FireDrillSampleIds
    {
        public const string BriefingShow = "fire_drill.briefing.show";
        public const string RolesAssign = "fire_drill.roles.assign";
        public const string Player1Prompt = "fire_drill.player1.commander.show";
        public const string Player2Prompt = "fire_drill.player2.extinguisher_a.show";
        public const string Player3Prompt = "fire_drill.player3.extinguisher_b.show";
        public const string Player4Prompt = "fire_drill.player4.evacuator.show";
        public const string AlarmStart = "fire_drill.alarm.start";
        public const string CommanderStart = "fire_drill.commander.start";
        public const string ExtinguisherAStart = "fire_drill.extinguisher_a.start";
        public const string ExtinguisherBStart = "fire_drill.extinguisher_b.start";
        public const string EvacuatorStart = "fire_drill.evacuator.start";
        public const string SafetyCheck = "fire_drill.safety.check";
        public const string SummaryShow = "fire_drill.summary.show";
        public const string CommanderFailure = "fire_drill.commander.failure";
        public const string ExtinguisherAFailure = "fire_drill.extinguisher_a.failure";
        public const string ExtinguisherBFailure = "fire_drill.extinguisher_b.failure";
        public const string EvacuatorFailure = "fire_drill.evacuator.failure";

        public const string Player1Ready = "fire_drill.player1.ready";
        public const string Player2Ready = "fire_drill.player2.ready";
        public const string Player3Ready = "fire_drill.player3.ready";
        public const string Player4Ready = "fire_drill.player4.ready";

        public const string CommanderReported = "fire_drill.commander.reported";
        public const string ExtinguisherACompleted = "fire_drill.extinguisher_a.completed";
        public const string ExtinguisherBCompleted = "fire_drill.extinguisher_b.completed";
        public const string EvacuationCompleted = "fire_drill.evacuation.completed";
        public const string SafetyPassed = "fire_drill.safety.passed";

        public static readonly string[] Operations =
        {
            BriefingShow, RolesAssign, Player1Prompt, Player2Prompt, Player3Prompt, Player4Prompt,
            AlarmStart, CommanderStart, ExtinguisherAStart, ExtinguisherBStart, EvacuatorStart,
            SafetyCheck, SummaryShow, CommanderFailure, ExtinguisherAFailure, ExtinguisherBFailure,
            EvacuatorFailure
        };
        public static readonly string[] ReadySignals =
        {
            Player1Ready, Player2Ready, Player3Ready, Player4Ready
        };

        public static readonly string[] CompletionStates =
        {
            CommanderReported, ExtinguisherACompleted, ExtinguisherBCompleted,
            EvacuationCompleted, SafetyPassed
        };
    }
}
