using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>Git provenance captured once at publish preflight.</summary>
    public sealed class HotUpdateGitSnapshot
    {
        public HotUpdateGitSnapshot(string branch, string commit, bool isDirty)
        {
            string normalizedBranch = string.IsNullOrWhiteSpace(branch) ? string.Empty : branch.Trim();
            Branch = string.IsNullOrEmpty(normalizedBranch) || string.Equals(normalizedBranch, "HEAD", StringComparison.Ordinal)
                ? "(detached)"
                : normalizedBranch;
            Commit = string.IsNullOrWhiteSpace(commit) ? throw new ArgumentException("Git commit is required.", nameof(commit)) : commit.Trim();
            IsDirty = isDirty;
        }

        public string Branch { get; }
        public string Commit { get; }
        public bool IsDirty { get; }
    }

    /// <summary>Replaceable boundary for recording repository provenance in tests and CI.</summary>
    public interface IHotUpdateGitSnapshotProvider
    {
        HotUpdateGitSnapshot ReadSnapshot();
    }

    /// <summary>Reads branch, commit and dirty status from Git without invoking a shell.</summary>
    public sealed class GitHotUpdateSnapshotProvider : IHotUpdateGitSnapshotProvider
    {
        private const int GitTimeoutMilliseconds = 15000;
        private readonly string _projectRoot;

        public GitHotUpdateSnapshotProvider(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Project root is required.", nameof(projectRoot));
            _projectRoot = Path.GetFullPath(projectRoot);
            if (!Directory.Exists(Path.Combine(_projectRoot, ".git")) && !File.Exists(Path.Combine(_projectRoot, ".git")))
                throw new DirectoryNotFoundException($"Git metadata was not found under '{_projectRoot}'.");
        }

        public HotUpdateGitSnapshot ReadSnapshot()
        {
            string branch = RunGit("rev-parse --abbrev-ref HEAD");
            string commit = RunGit("rev-parse HEAD");
            string status = RunGit("status --porcelain=v1 -z --untracked-files=all");
            return new HotUpdateGitSnapshot(branch, commit, !string.IsNullOrEmpty(status));
        }

        private string RunGit(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                if (!process.Start()) throw new InvalidOperationException($"Git command '{arguments}' could not be started.");
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(GitTimeoutMilliseconds))
                {
                    process.Kill();
                    throw new TimeoutException($"Git command '{arguments}' did not finish within {GitTimeoutMilliseconds} ms.");
                }
                if (!Task.WaitAll(new Task[] { outputTask, errorTask }, GitTimeoutMilliseconds))
                    throw new TimeoutException($"Git command '{arguments}' output could not be read before the timeout.");
                string output = outputTask.GetAwaiter().GetResult();
                string error = errorTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Git command '{arguments}' failed with exit code {process.ExitCode}: {error.Trim()}");
                return output.TrimEnd('\r', '\n');
            }
        }
    }

    /// <summary>Preflight provenance policy: Production must be clean; other environments retain dirty state and warn.</summary>
    public sealed class HotUpdateGitPreflightStageHandler : IHotUpdatePublishStageHandler
    {
        private readonly IHotUpdateGitSnapshotProvider _snapshotProvider;

        public HotUpdateGitPreflightStageHandler(IHotUpdateGitSnapshotProvider snapshotProvider)
        {
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        }

        public HotUpdatePublishStage Stage => HotUpdatePublishStage.Preflight;

        public Task<HotUpdatePublishStepResult> ExecuteAsync(HotUpdatePublishContext context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            HotUpdateGitSnapshot snapshot = _snapshotProvider.ReadSnapshot();
            if (snapshot == null) throw new InvalidOperationException("Git snapshot provider returned no repository state.");

            context.GitBranch = snapshot.Branch;
            context.GitCommit = snapshot.Commit;
            context.GitDirty = snapshot.IsDirty;

            bool isProduction = string.Equals(context.Environment, "Production", StringComparison.OrdinalIgnoreCase);
            bool isDevelopment = string.Equals(context.Environment, "Development", StringComparison.OrdinalIgnoreCase);
            bool isStaging = string.Equals(context.Environment, "Staging", StringComparison.OrdinalIgnoreCase);
            if (!isProduction && !isDevelopment && !isStaging)
                return Task.FromResult(HotUpdatePublishStepResult.Failed(
                    HotUpdatePublishErrorCode.StageFailed,
                    $"Unsupported publishing environment '{context.Environment}'. Expected Development, Staging or Production."));

            if (snapshot.IsDirty && isProduction)
                return Task.FromResult(HotUpdatePublishStepResult.Failed(
                    HotUpdatePublishErrorCode.StageFailed,
                    "Production publishing is blocked because the Git working tree contains staged, unstaged or untracked changes."));

            if (snapshot.IsDirty)
                return Task.FromResult(HotUpdatePublishStepResult.Succeeded(
                    "Git working tree is dirty; the state was captured in release provenance."));

            return Task.FromResult(HotUpdatePublishStepResult.Succeeded());
        }
    }
}
