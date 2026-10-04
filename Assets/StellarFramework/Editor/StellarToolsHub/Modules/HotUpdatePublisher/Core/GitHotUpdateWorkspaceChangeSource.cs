using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>通过 Git porcelain status 读取暂存、未暂存和未跟踪改动。</summary>
    public sealed class GitHotUpdateWorkspaceChangeSource : IHotUpdateWorkspaceChangeSource
    {
        private const int GitTimeoutMilliseconds = 15000;
        private readonly string _projectRoot;

        /// <summary>创建工作区扫描器。</summary>
        public GitHotUpdateWorkspaceChangeSource(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            }

            _projectRoot = Path.GetFullPath(projectRoot);
            if (!Directory.Exists(Path.Combine(_projectRoot, ".git")) &&
                !File.Exists(Path.Combine(_projectRoot, ".git")))
            {
                throw new DirectoryNotFoundException($"Git metadata was not found under '{_projectRoot}'.");
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<HotUpdateWorkspaceChange> ReadChanges()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "status --porcelain=v1 -z --untracked-files=all",
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
                if (!process.Start())
                {
                    throw new InvalidOperationException("Git status process could not be started.");
                }

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(GitTimeoutMilliseconds))
                {
                    process.Kill();
                    throw new TimeoutException(
                        $"Git status did not finish within {GitTimeoutMilliseconds} ms.");
                }

                if (!Task.WaitAll(new Task[] { outputTask, errorTask }, GitTimeoutMilliseconds))
                {
                    throw new TimeoutException("Git status output could not be read before the timeout.");
                }

                string output = outputTask.GetAwaiter().GetResult();
                string error = errorTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Git status failed with exit code {process.ExitCode}: {error.Trim()}");
                }

                return ParsePorcelainStatus(output);
            }
        }

        /// <summary>解析 porcelain v1 的 NUL 分隔输出，包括含空格路径与重命名双路径。</summary>
        public static IReadOnlyList<HotUpdateWorkspaceChange> ParsePorcelainStatus(string output)
        {
            var changes = new List<HotUpdateWorkspaceChange>();
            if (string.IsNullOrEmpty(output))
            {
                return changes;
            }

            string[] entries = output.Split('\0');
            for (int index = 0; index < entries.Length; index++)
            {
                string entry = entries[index];
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                if (entry.Length < 4 || entry[2] != ' ')
                {
                    throw new FormatException($"Invalid Git porcelain status entry: '{entry}'.");
                }

                string status = entry.Substring(0, 2);
                string path = entry.Substring(3);
                bool renamed = status.IndexOf('R') >= 0;
                bool copied = status.IndexOf('C') >= 0;
                if (renamed || copied)
                {
                    if (index + 1 >= entries.Length || string.IsNullOrEmpty(entries[index + 1]))
                    {
                        throw new FormatException($"Git rename/copy entry is missing its source path: '{entry}'.");
                    }

                    string sourcePath = entries[++index];
                    changes.Add(new HotUpdateWorkspaceChange(
                        path,
                        status,
                        renamed ? HotUpdateChangeKind.Renamed : HotUpdateChangeKind.Copied));
                    changes.Add(new HotUpdateWorkspaceChange(
                        sourcePath,
                        "D ",
                        HotUpdateChangeKind.Deleted));
                    continue;
                }

                changes.Add(new HotUpdateWorkspaceChange(path, status, GetChangeKind(status)));
            }

            return changes;
        }

        private static HotUpdateChangeKind GetChangeKind(string status)
        {
            if (status == "??" || status.IndexOf('A') >= 0)
            {
                return HotUpdateChangeKind.Added;
            }

            if (status.IndexOf('D') >= 0)
            {
                return HotUpdateChangeKind.Deleted;
            }

            if (status.IndexOf('T') >= 0)
            {
                return HotUpdateChangeKind.TypeChanged;
            }

            if (status.IndexOf('M') >= 0 || status.IndexOf('U') >= 0)
            {
                return HotUpdateChangeKind.Modified;
            }

            return HotUpdateChangeKind.Unknown;
        }
    }
}
