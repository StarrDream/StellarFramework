using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Required verification tier for a Publisher operation.</summary>
    public enum HotUpdateReleaseGateLevel
    {
        Fast = 0,
        Full = 1
    }

    /// <summary>One executed release gate and its machine-readable evidence location.</summary>
    [Serializable]
    public sealed class HotUpdateReleaseGateRun
    {
        public bool Passed;
        public string EvidencePath;
        public string Diagnostic;
        public DateTime StartedAtUtc;
        public DateTime CompletedAtUtc;
    }

    /// <summary>Summary of all gates required and completed for a publish operation.</summary>
    [Serializable]
    public sealed class HotUpdateReleaseGateReport
    {
        public HotUpdateReleaseGateLevel RequiredLevel;
        public HotUpdateReleaseGateRun FastGate;
        public HotUpdateReleaseGateRun FullGate;
        public bool Passed => FastGate != null && FastGate.Passed &&
            (RequiredLevel == HotUpdateReleaseGateLevel.Fast || (FullGate != null && FullGate.Passed));
    }

    /// <summary>Runs the repository's canonical Unity PlayMode HotUpdate release gate.</summary>
    public interface IHotUpdateFastReleaseGateRunner
    {
        Task<HotUpdateReleaseGateRun> RunAsync(HotUpdatePublishContext context, CancellationToken cancellationToken);
    }

    /// <summary>Runs a configured platform release verifier for Full Gate (Android or Windows IL2CPP).</summary>
    public interface IHotUpdateFullReleaseGateRunner
    {
        Task<HotUpdateReleaseGateRun> RunAsync(HotUpdatePublishContext context, CancellationToken cancellationToken);
    }

    /// <summary>Chooses a gate level and enforces the Red-change boundary.</summary>
    public static class HotUpdateReleaseGatePolicy
    {
        public static HotUpdateReleaseGateLevel Resolve(HotUpdatePublishContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            HotUpdateChangeClassificationResult classification = context.ChangeClassification;
            if (classification == null)
                throw new InvalidOperationException("Release Gate requires completed change classification.");

            if (!context.IsBaseAppRelease && !classification.CanHotPatch)
                throw new InvalidOperationException("RED changes or dependency boundary violations block an ordinary Hot Patch.");

            return context.IsBaseAppRelease || context.IsMajorHotPatch || classification.RequiresFullGate
                ? HotUpdateReleaseGateLevel.Full
                : HotUpdateReleaseGateLevel.Fast;
        }
    }

    /// <summary>Runs Fast Gate for every publish, then Full Gate when policy requires it.</summary>
    public sealed class HotUpdateReleaseGateStageHandler : IHotUpdatePublishStageHandler
    {
        private readonly IHotUpdateFastReleaseGateRunner _fastRunner;
        private readonly IHotUpdateFullReleaseGateRunner _fullRunner;

        public HotUpdateReleaseGateStageHandler(
            IHotUpdateFastReleaseGateRunner fastRunner,
            IHotUpdateFullReleaseGateRunner fullRunner = null)
        {
            _fastRunner = fastRunner ?? throw new ArgumentNullException(nameof(fastRunner));
            _fullRunner = fullRunner;
        }

        public HotUpdatePublishStage Stage => HotUpdatePublishStage.RunReleaseGate;

        public async Task<HotUpdatePublishStepResult> ExecuteAsync(
            HotUpdatePublishContext context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            HotUpdateReleaseGateLevel required;
            try
            {
                required = HotUpdateReleaseGatePolicy.Resolve(context);
            }
            catch (InvalidOperationException exception)
            {
                return HotUpdatePublishStepResult.Failed(
                    HotUpdatePublishErrorCode.ReleaseGatePolicyRejected, exception.Message);
            }

            var report = new HotUpdateReleaseGateReport { RequiredLevel = required };
            context.ReleaseGateReport = report;
            report.FastGate = await _fastRunner.RunAsync(context, cancellationToken);
            if (report.FastGate == null || !report.FastGate.Passed)
                return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.ReleaseGateFailed,
                    report.FastGate?.Diagnostic ?? "Fast Gate returned no passing machine evidence.");

            if (required == HotUpdateReleaseGateLevel.Full)
            {
                if (_fullRunner == null)
                    return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.FullReleaseGateUnavailable,
                        "Full Gate is required but no Android or Windows IL2CPP verifier is configured.");

                report.FullGate = await _fullRunner.RunAsync(context, cancellationToken);
                if (report.FullGate == null || !report.FullGate.Passed)
                    return HotUpdatePublishStepResult.Failed(HotUpdatePublishErrorCode.ReleaseGateFailed,
                        report.FullGate?.Diagnostic ?? "Full Gate returned no passing machine evidence.");
            }

            return HotUpdatePublishStepResult.Succeeded();
        }
    }

    /// <summary>Executes the existing PowerShell Fast Gate and accepts success only from its JSON evidence.</summary>
    public sealed class PowerShellHotUpdateFastReleaseGateRunner : IHotUpdateFastReleaseGateRunner
    {
        private readonly string _projectRoot;
        private readonly string _unitySkillsUrl;
        private readonly int _timeoutMinutes;
        private readonly IHotUpdateGateProcessRunner _processRunner;

        public PowerShellHotUpdateFastReleaseGateRunner(
            string projectRoot,
            string unitySkillsUrl,
            IHotUpdateGateProcessRunner processRunner,
            int timeoutMinutes = 20)
        {
            _projectRoot = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)));
            _unitySkillsUrl = unitySkillsUrl ?? throw new ArgumentNullException(nameof(unitySkillsUrl));
            if (timeoutMinutes < 1 || timeoutMinutes > 30) throw new ArgumentOutOfRangeException(nameof(timeoutMinutes));
            _timeoutMinutes = timeoutMinutes;
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        }

        public async Task<HotUpdateReleaseGateRun> RunAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            string safeId = string.IsNullOrWhiteSpace(context.ReleaseId) ? "unassigned" : Sanitize(context.ReleaseId);
            string evidencePath = Path.Combine(_projectRoot, "BuildArtifacts", "HotUpdate", "ReleaseGates", safeId + "-fast-gate.json");
            string scriptPath = Path.Combine(_projectRoot, "Tools", "Verification", "Invoke-HotUpdatePlayModeReleaseGate.ps1");
            if (!File.Exists(scriptPath)) throw new FileNotFoundException("Canonical Fast Gate script was not found.", scriptPath);

            DateTime started = DateTime.UtcNow;
            string args = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(scriptPath) +
                " -UnitySkillsUrl " + Quote(_unitySkillsUrl) +
                " -TimeoutMinutes " + _timeoutMinutes.ToString(CultureInfo.InvariantCulture) +
                " -EvidencePath " + Quote(evidencePath);
            HotUpdateGateProcessResult process = await _processRunner.RunAsync("powershell.exe", args, _projectRoot,
                TimeSpan.FromMinutes(_timeoutMinutes + 2), cancellationToken);

            var run = new HotUpdateReleaseGateRun
            {
                Passed = false,
                EvidencePath = evidencePath,
                Diagnostic = process?.Diagnostic ?? "Fast Gate process returned no result.",
                StartedAtUtc = started,
                CompletedAtUtc = DateTime.UtcNow
            };
            if (process == null || process.ExitCode != 0 || !File.Exists(evidencePath)) return run;

            HotUpdateFastGateEvidence evidence = JsonUtility.FromJson<HotUpdateFastGateEvidence>(File.ReadAllText(evidencePath));
            run.Passed = evidence != null && string.Equals(evidence.status, "PASS", StringComparison.Ordinal) &&
                evidence.test != null && evidence.test.totalTests == 1 && evidence.test.passedTests == 1 &&
                evidence.test.failedTests == 0 && evidence.test.skippedTests == 0 && evidence.test.inconclusiveTests == 0;
            run.Diagnostic = run.Passed ? string.Empty : "Fast Gate process completed without exact 1/1 PASS evidence.";
            return run;
        }

        private static string Sanitize(string value)
        {
            char[] chars = value.ToCharArray();
            for (int index = 0; index < chars.Length; index++)
                if (!char.IsLetterOrDigit(chars[index]) && chars[index] != '-' && chars[index] != '_') chars[index] = '_';
            return new string(chars);
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        [Serializable]
        private sealed class HotUpdateFastGateEvidence
        {
            public string status;
            public HotUpdateFastGateTestEvidence test;
        }

        [Serializable]
        private sealed class HotUpdateFastGateTestEvidence
        {
            public int totalTests;
            public int passedTests;
            public int failedTests;
            public int skippedTests;
            public int inconclusiveTests;
        }
    }

    /// <summary>Uses the existing Android Release Verification pipeline for the Android Full Gate.</summary>
    public sealed class PowerShellHotUpdateAndroidFullReleaseGateRunner : IHotUpdateFullReleaseGateRunner
    {
        private static readonly Regex ArtifactsPathPattern = new Regex(
            @"Artifacts:\s*(?<path>.+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly string _projectRoot;
        private readonly string _unitySkillsUrl;
        private readonly int _buildTimeoutMinutes;
        private readonly IHotUpdateGateProcessRunner _processRunner;

        public PowerShellHotUpdateAndroidFullReleaseGateRunner(
            string projectRoot,
            string unitySkillsUrl,
            IHotUpdateGateProcessRunner processRunner,
            int buildTimeoutMinutes = 45)
        {
            _projectRoot = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)));
            _unitySkillsUrl = unitySkillsUrl ?? throw new ArgumentNullException(nameof(unitySkillsUrl));
            if (buildTimeoutMinutes < 1 || buildTimeoutMinutes > 180)
                throw new ArgumentOutOfRangeException(nameof(buildTimeoutMinutes));
            _buildTimeoutMinutes = buildTimeoutMinutes;
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        }

        public async Task<HotUpdateReleaseGateRun> RunAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Platform != BuildTarget.Android)
                throw new InvalidOperationException($"Android Full Gate cannot validate target '{context.Platform}'.");

            string script = Path.Combine(_projectRoot, "Tools", "AndroidVerification", "Invoke-StellarAndroidReleaseVerification.ps1");
            if (!File.Exists(script)) throw new FileNotFoundException("Existing Android Release Verification script was not found.", script);
            string arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                " -UnitySkillsUrl " + Quote(_unitySkillsUrl) +
                " -BuildTimeoutMinutes " + _buildTimeoutMinutes.ToString(CultureInfo.InvariantCulture) +
                " -HotUpdate";

            DateTime started = DateTime.UtcNow;
            HotUpdateGateProcessResult process = await _processRunner.RunAsync("powershell.exe", arguments, _projectRoot,
                TimeSpan.FromMinutes(_buildTimeoutMinutes + 10), cancellationToken);
            var run = new HotUpdateReleaseGateRun
            {
                Passed = false,
                StartedAtUtc = started,
                CompletedAtUtc = DateTime.UtcNow,
                Diagnostic = process?.Diagnostic ?? "Android Full Gate process returned no result."
            };
            if (process == null || process.ExitCode != 0) return run;

            Match match = ArtifactsPathPattern.Match(process.Diagnostic ?? string.Empty);
            if (!match.Success)
            {
                run.Diagnostic = "Android verifier exited successfully but did not identify its machine-evidence directory.";
                return run;
            }

            string evidencePath = Path.Combine(match.Groups["path"].Value.Trim(), "pipeline-result.json");
            run.EvidencePath = evidencePath;
            if (!File.Exists(evidencePath))
            {
                run.Diagnostic = "Android verifier exited successfully but pipeline-result.json is missing.";
                return run;
            }

            AndroidFullGateEvidence evidence = JsonUtility.FromJson<AndroidFullGateEvidence>(File.ReadAllText(evidencePath));
            run.Passed = evidence != null && evidence.status == "PASS" && evidence.profile == "HotUpdate" &&
                evidence.productVerificationStatus == "PASS" && evidence.cleanupStatus == "PASS" &&
                IsAndroidRuntimePass(evidence.hotUpdateRuntime?.coldStart) &&
                IsAndroidRuntimePass(evidence.hotUpdateRuntime?.restart);
            run.Diagnostic = run.Passed ? string.Empty : "Android Full Gate evidence did not prove both cold-start and restart HotUpdate runtime passes.";
            return run;
        }

        private static bool IsAndroidRuntimePass(AndroidRuntimeEvidence runtime)
        {
            return runtime != null && runtime.status == "PASS" && runtime.platform == "Android" &&
                runtime.manifestBuildTarget == "Android" && runtime.contentUpdateSucceeded &&
                runtime.resKitManifestLoaded && runtime.resKitAssemblyLoaded && runtime.assemblySha256Verified &&
                runtime.aotMetadataLoadSucceeded && runtime.assemblyLoadSucceeded && runtime.entryPointInvoked &&
                runtime.entryMarkerObserved && !string.IsNullOrWhiteSpace(runtime.loadedAssemblyFullName) &&
                runtime.loadedAssemblyFullName.IndexOf("HotUpdate", StringComparison.Ordinal) >= 0 &&
                string.Equals(runtime.expectedAssemblySha256, runtime.actualAssemblySha256, StringComparison.OrdinalIgnoreCase);
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";

        [Serializable]
        private sealed class AndroidFullGateEvidence
        {
            public string status;
            public string profile;
            public string productVerificationStatus;
            public string cleanupStatus;
            public AndroidRuntimePair hotUpdateRuntime;
        }

        [Serializable]
        private sealed class AndroidRuntimePair
        {
            public AndroidRuntimeEvidence coldStart;
            public AndroidRuntimeEvidence restart;
        }

        [Serializable]
        private sealed class AndroidRuntimeEvidence
        {
            public string status;
            public string platform;
            public string manifestBuildTarget;
            public bool contentUpdateSucceeded;
            public bool resKitManifestLoaded;
            public bool resKitAssemblyLoaded;
            public bool assemblySha256Verified;
            public bool aotMetadataLoadSucceeded;
            public bool assemblyLoadSucceeded;
            public bool entryPointInvoked;
            public bool entryMarkerObserved;
            public string loadedAssemblyFullName;
            public string expectedAssemblySha256;
            public string actualAssemblySha256;
        }
    }

    /// <summary>Process boundary so gate policy and evidence checks can be tested without launching tools.</summary>
    public interface IHotUpdateGateProcessRunner
    {
        Task<HotUpdateGateProcessResult> RunAsync(string executable, string arguments, string workingDirectory,
            TimeSpan timeout, CancellationToken cancellationToken);
    }

    public sealed class HotUpdateGateProcessResult
    {
        public int ExitCode { get; set; }
        public string Diagnostic { get; set; }
    }

    /// <summary>Runs a child process asynchronously so Unity's Editor thread remains responsive.</summary>
    public sealed class SystemHotUpdateGateProcessRunner : IHotUpdateGateProcessRunner
    {
        public async Task<HotUpdateGateProcessResult> RunAsync(string executable, string arguments,
            string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(executable)) throw new ArgumentException("Executable is required.", nameof(executable));
            if (string.IsNullOrWhiteSpace(workingDirectory)) throw new ArgumentException("Working directory is required.", nameof(workingDirectory));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

            var output = new StringBuilder();
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments ?? string.Empty,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                process.OutputDataReceived += (_, eventArgs) => AppendLine(output, eventArgs.Data);
                process.ErrorDataReceived += (_, eventArgs) => AppendLine(output, eventArgs.Data);
                if (!process.Start()) throw new InvalidOperationException($"Could not start release gate process '{executable}'.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                DateTime deadline = DateTime.UtcNow + timeout;
                while (!process.HasExited)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        TryKill(process);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    if (DateTime.UtcNow >= deadline)
                    {
                        TryKill(process);
                        throw new TimeoutException($"Release gate process exceeded timeout {timeout}.");
                    }
                    await Task.Delay(100).ConfigureAwait(false);
                }

                process.WaitForExit();
                return new HotUpdateGateProcessResult
                {
                    ExitCode = process.ExitCode,
                    Diagnostic = output.ToString()
                };
            }
        }

        private static void AppendLine(StringBuilder output, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            lock (output)
            {
                // Bound diagnostic memory; machine evidence remains the authoritative result.
                const int maximumCharacters = 32768;
                output.AppendLine(value);
                if (output.Length > maximumCharacters)
                    output.Remove(0, output.Length - maximumCharacters);
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (InvalidOperationException)
            {
                // The process exited between HasExited and Kill; preserve the original timeout/cancellation.
            }
        }
    }
}
