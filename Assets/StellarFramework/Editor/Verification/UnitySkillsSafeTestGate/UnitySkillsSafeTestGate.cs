using System;
using System.Globalization;
using UnityEditor;
using UnitySkills;

namespace StellarFramework.Editor.Verification
{
    /// <summary>
    /// StellarFramework-side safety gate for UnitySkills Test Runner launches.
    /// </summary>
    /// <remarks>
    /// EditMode Test Runner locks assembly reloads while a run is active. Starting a run while
    /// an asset refresh or script compilation is pending can deadlock tests that rely on the
    /// Editor PlayerLoop. This gate deliberately separates refresh, idle observation, settle,
    /// and test launch into distinct calls so a domain reload can safely happen between them.
    /// </remarks>
    public static class UnitySkillsSafeTestGate
    {
        private const string SessionPrefix = "StellarFramework.UnitySkillsSafeTestGate.";
        private const string PhaseRefreshRequested = "refresh";
        private const string PhaseIdleObserved = "idle";
        private const double IdleSettleSeconds = 0.75d;
        private const double GateExpirySeconds = 60d;

        [UnitySkill(
            "stellar_test_run_safe",
            "Safely run Unity tests after AssetDatabase refresh, compilation/update idle checks, and a short idle settle window. Retry this same skill when retryRequired=true; once stable it delegates to UnitySkills test_run and returns the normal test jobId.",
            Category = SkillCategory.Test,
            Operation = SkillOperation.Execute,
            Tags = new[] { "stellar", "test", "safe", "gate", "compile", "refresh", "editmode", "playmode" },
            Outputs = new[] { "status", "retryRequired", "retryAfterSeconds", "jobId", "testMode", "filter" },
            SupportsDryRun = false,
            MayTriggerReload = true,
            MayEnterPlayMode = true,
            RiskLevel = "medium")]
        public static object Run(string testMode = "EditMode", string filter = null, bool refreshAssets = true)
        {
            if (!TryNormalizeTestMode(testMode, out string normalizedMode))
            {
                return new
                {
                    success = false,
                    status = "invalid_test_mode",
                    retryRequired = false,
                    error = $"Unsupported testMode '{testMode}'. Use EditMode or PlayMode."
                };
            }

            string sessionKey = BuildSessionKey(normalizedMode, filter);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            GateState state = default;

            if (refreshAssets && !TryReadState(sessionKey, now, out state))
            {
                WriteState(sessionKey, PhaseRefreshRequested, now);
                AssetDatabase.Refresh();
                return Waiting(
                    normalizedMode,
                    filter,
                    "asset_refresh_requested",
                    "AssetDatabase.Refresh was requested. Retry after Unity finishes any import/compile/domain-reload work.",
                    2);
            }

            if (!refreshAssets && !TryReadState(sessionKey, now, out state))
            {
                state = new GateState(PhaseRefreshRequested, now);
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return Waiting(
                    normalizedMode,
                    filter,
                    "waiting_editor_idle",
                    BuildEditorBusyReason(),
                    2);
            }

            if (string.Equals(normalizedMode, "EditMode", StringComparison.Ordinal) &&
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return Waiting(
                    normalizedMode,
                    filter,
                    "waiting_edit_mode",
                    "EditMode tests will not start while Unity is playing or changing Play Mode state.",
                    2);
            }

            if (!string.Equals(state.Phase, PhaseIdleObserved, StringComparison.Ordinal))
            {
                WriteState(sessionKey, PhaseIdleObserved, now);
                return Waiting(
                    normalizedMode,
                    filter,
                    "editor_idle_observed",
                    "Unity is idle. One short settle interval is required to close the refresh/compile race before Test Runner starts.",
                    1);
            }

            double idleSeconds = (now - state.Timestamp).TotalSeconds;
            if (idleSeconds < IdleSettleSeconds)
            {
                return Waiting(
                    normalizedMode,
                    filter,
                    "settling_editor_idle",
                    $"Unity has been idle for {idleSeconds:F2}s; waiting for the {IdleSettleSeconds:F2}s settle window.",
                    1);
            }

            // Re-check immediately before launch. This is intentionally after the settle window.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                WriteState(sessionKey, PhaseRefreshRequested, now);
                return Waiting(
                    normalizedMode,
                    filter,
                    "editor_became_busy",
                    BuildEditorBusyReason(),
                    2);
            }

            SessionState.EraseString(sessionKey);
            return TestSkills.TestRun(normalizedMode, filter);
        }

        [UnitySkill(
            "stellar_test_gate_status",
            "Report whether Unity is currently safe to launch a Test Runner job. This does not refresh assets or start tests.",
            Category = SkillCategory.Test,
            Operation = SkillOperation.Query,
            Tags = new[] { "stellar", "test", "gate", "status", "compile", "refresh" },
            Outputs = new[] { "ready", "isCompiling", "isUpdating", "isPlayingOrWillChangePlaymode" },
            ReadOnly = true,
            Mode = SkillMode.SemiAuto)]
        public static object Status(string testMode = "EditMode")
        {
            if (!TryNormalizeTestMode(testMode, out string normalizedMode))
            {
                return new
                {
                    success = false,
                    ready = false,
                    error = $"Unsupported testMode '{testMode}'. Use EditMode or PlayMode."
                };
            }

            bool blockedByPlayMode = string.Equals(normalizedMode, "EditMode", StringComparison.Ordinal) &&
                                     EditorApplication.isPlayingOrWillChangePlaymode;
            bool ready = !EditorApplication.isCompiling && !EditorApplication.isUpdating && !blockedByPlayMode;

            return new
            {
                success = true,
                testMode = normalizedMode,
                ready,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode
            };
        }

        private static object Waiting(
            string testMode,
            string filter,
            string status,
            string message,
            int retryAfterSeconds)
        {
            return new
            {
                success = true,
                status,
                retryRequired = true,
                retryAfterSeconds,
                testMode,
                filter,
                message
            };
        }

        private static bool TryNormalizeTestMode(string testMode, out string normalizedMode)
        {
            if (string.Equals(testMode, "EditMode", StringComparison.OrdinalIgnoreCase))
            {
                normalizedMode = "EditMode";
                return true;
            }

            if (string.Equals(testMode, "PlayMode", StringComparison.OrdinalIgnoreCase))
            {
                normalizedMode = "PlayMode";
                return true;
            }

            normalizedMode = null;
            return false;
        }

        private static string BuildEditorBusyReason()
        {
            if (EditorApplication.isCompiling && EditorApplication.isUpdating)
                return "Unity is compiling scripts and updating assets. Retry after both states become idle.";
            if (EditorApplication.isCompiling)
                return "Unity is compiling scripts. Retry after compilation and any domain reload finish.";
            return "Unity is updating/importing assets. Retry after the AssetDatabase becomes idle.";
        }

        private static string BuildSessionKey(string testMode, string filter)
        {
            unchecked
            {
                int hash = 17;
                string value = testMode + "\n" + (filter ?? string.Empty);
                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];
                return SessionPrefix + hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private static bool TryReadState(string key, DateTimeOffset now, out GateState state)
        {
            string raw = SessionState.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                state = default;
                return false;
            }

            int separator = raw.IndexOf('|');
            if (separator <= 0 || separator >= raw.Length - 1 ||
                !long.TryParse(raw.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
            {
                SessionState.EraseString(key);
                state = default;
                return false;
            }

            DateTimeOffset timestamp;
            try
            {
                timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            }
            catch (ArgumentOutOfRangeException)
            {
                SessionState.EraseString(key);
                state = default;
                return false;
            }

            if ((now - timestamp).TotalSeconds > GateExpirySeconds || timestamp > now)
            {
                SessionState.EraseString(key);
                state = default;
                return false;
            }

            state = new GateState(raw.Substring(0, separator), timestamp);
            return true;
        }

        private static void WriteState(string key, string phase, DateTimeOffset timestamp)
        {
            SessionState.SetString(
                key,
                phase + "|" + timestamp.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture));
        }

        private readonly struct GateState
        {
            public GateState(string phase, DateTimeOffset timestamp)
            {
                Phase = phase;
                Timestamp = timestamp;
            }

            public string Phase { get; }
            public DateTimeOffset Timestamp { get; }
        }
    }
}
