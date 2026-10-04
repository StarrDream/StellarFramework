using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>
    /// 创建、加载、列举并验证与 Base App 绑定的 metadata 快照。
    /// 正式数据只写入 BuildArtifacts/HotUpdate/BaseReleases，不读取临时 HybridCLRData 回退路径。
    /// </summary>
    public sealed class HotUpdateBaseReleaseRepository
    {
        public const string DefaultRelativeRoot = "BuildArtifacts/HotUpdate/BaseReleases";
        private const string RecordFileName = "base-release.json";
        private const string MetadataDirectoryName = "AotMetadata";
        private const int CurrentSchemaVersion = 1;
        private static readonly Regex SafeVersionRegex = new Regex(
            @"\A[A-Za-z0-9][A-Za-z0-9.+-]{0,63}\z",
            RegexOptions.Compiled);

        private readonly string _repositoryRoot;

        /// <summary>创建仓库；未传路径时使用项目根下的正式 BaseRelease 目录。</summary>
        public HotUpdateBaseReleaseRepository(string repositoryRoot = null)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot))
            {
                DirectoryInfo projectRoot = Directory.GetParent(UnityEngine.Application.dataPath);
                if (projectRoot == null)
                {
                    throw new DirectoryNotFoundException("Unity project root could not be resolved from Application.dataPath.");
                }

                repositoryRoot = Path.Combine(
                    projectRoot.FullName,
                    DefaultRelativeRoot.Replace('/', Path.DirectorySeparatorChar));
            }

            _repositoryRoot = Path.GetFullPath(repositoryRoot);
        }

        /// <summary>仓库的规范化绝对路径。</summary>
        public string RepositoryRoot => _repositoryRoot;

        /// <summary>从本次 Base App 构建产物创建不可覆盖的 BaseRelease 快照。</summary>
        public HotUpdateBaseRelease Create(HotUpdateBaseReleaseCreateRequest request)
        {
            ValidateCreateRequest(request);
            string platformDirectory = GetPlatformDirectory(request.Platform);
            Directory.CreateDirectory(platformDirectory);

            string releaseDirectory = GetReleaseDirectory(request.Platform, request.BaseAppVersion);
            if (Directory.Exists(releaseDirectory) || File.Exists(releaseDirectory))
            {
                throw new IOException(
                    $"BaseRelease '{request.Platform}/{request.BaseAppVersion}' already exists and is immutable.");
            }

            string stagingDirectory = Path.Combine(
                platformDirectory,
                ".staging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);
            try
            {
                string metadataDirectory = Path.Combine(stagingDirectory, MetadataDirectoryName);
                Directory.CreateDirectory(metadataDirectory);

                string[] sourcePaths = request.AotMetadataSourcePaths
                    .Select(sourcePath => Path.GetFullPath(sourcePath))
                    .OrderBy(sourcePath => Path.GetFileName(sourcePath), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var metadataNames = new string[sourcePaths.Length];
                var metadataHashes = new string[sourcePaths.Length];
                for (int index = 0; index < sourcePaths.Length; index++)
                {
                    string sourcePath = sourcePaths[index];
                    string fileName = Path.GetFileName(sourcePath);
                    if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"AOT metadata source '{sourcePath}' must be a .dll file.");
                    }

                    if (index > 0 && string.Equals(
                            fileName,
                            Path.GetFileName(sourcePaths[index - 1]),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"AOT metadata source file name '{fileName}' is duplicated.");
                    }

                    if (!File.Exists(sourcePath))
                    {
                        throw new FileNotFoundException(
                            $"AOT metadata source file was not found: '{sourcePath}'.",
                            sourcePath);
                    }

                    string destinationPath = Path.Combine(metadataDirectory, fileName);
                    File.Copy(sourcePath, destinationPath, false);
                    metadataNames[index] = fileName;
                    metadataHashes[index] = ComputeSha256(destinationPath);
                }

                var release = new HotUpdateBaseRelease
                {
                    SchemaVersion = CurrentSchemaVersion,
                    BaseAppVersion = request.BaseAppVersion.Trim(),
                    Platform = request.Platform,
                    Architecture = request.Architecture.Trim(),
                    UnityVersion = request.UnityVersion.Trim(),
                    HybridCLRVersion = request.HybridCLRVersion.Trim(),
                    YooAssetVersion = request.YooAssetVersion.Trim(),
                    ScriptingBackend = request.ScriptingBackend,
                    GitCommit = request.GitCommit.Trim(),
                    CreatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    AotMetadata = metadataNames,
                    AotHashes = metadataHashes
                };

                File.WriteAllText(
                    Path.Combine(stagingDirectory, RecordFileName),
                    UnityEngine.JsonUtility.ToJson(release, true),
                    new UTF8Encoding(false));
                Directory.Move(stagingDirectory, releaseDirectory);
                return release;
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, true);
                }
            }
        }

        /// <summary>按平台和 Base App 版本加载唯一记录；缺失或损坏时抛出明确异常。</summary>
        public HotUpdateBaseRelease Load(BuildTarget platform, string baseAppVersion)
        {
            string releaseDirectory = GetReleaseDirectory(platform, baseAppVersion);
            string recordPath = Path.Combine(releaseDirectory, RecordFileName);
            if (!File.Exists(recordPath))
            {
                throw new FileNotFoundException(
                    $"BaseRelease '{platform}/{baseAppVersion}' was not found. Select a recorded BaseRelease before building a Hot Patch.",
                    recordPath);
            }

            string json = File.ReadAllText(recordPath, Encoding.UTF8);
            HotUpdateBaseRelease release = UnityEngine.JsonUtility.FromJson<HotUpdateBaseRelease>(json);
            if (release == null)
            {
                throw new InvalidDataException($"BaseRelease record '{recordPath}' could not be parsed.");
            }

            if (release.Platform != platform ||
                !string.Equals(release.BaseAppVersion, baseAppVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"BaseRelease record '{recordPath}' does not match its platform/version directory.");
            }

            return release;
        }

        /// <summary>列出平台下所有正式记录，按创建时间从新到旧排序。</summary>
        public IReadOnlyList<HotUpdateBaseRelease> List(BuildTarget platform)
        {
            string platformDirectory = GetPlatformDirectory(platform);
            if (!Directory.Exists(platformDirectory))
            {
                return Array.Empty<HotUpdateBaseRelease>();
            }

            var releases = new List<HotUpdateBaseRelease>();
            foreach (string directory in Directory.GetDirectories(platformDirectory))
            {
                string directoryName = Path.GetFileName(directory);
                if (directoryName.StartsWith(".staging-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                releases.Add(Load(platform, directoryName));
            }

            return releases
                .OrderByDescending(release => ParseCreatedAt(release.CreatedAt))
                .ToArray();
        }

        /// <summary>
        /// 校验已选 BaseRelease 与当前目标环境相同，并重新读取磁盘记录及其 metadata SHA256；
        /// 不把调用方可变对象或临时 HybridCLRData 视为权威来源。
        /// </summary>
        public HotUpdateBaseReleaseValidationResult Validate(
            HotUpdateBaseRelease selectedRelease,
            HotUpdateBaseReleaseRequirements requirements)
        {
            if (selectedRelease == null)
            {
                throw new ArgumentNullException(nameof(selectedRelease));
            }

            if (requirements == null)
            {
                throw new ArgumentNullException(nameof(requirements));
            }

            HotUpdateBaseRelease storedRelease = Load(
                selectedRelease.Platform,
                selectedRelease.BaseAppVersion);
            var issues = new List<HotUpdateBaseReleaseValidationIssue>();

            if (storedRelease.SchemaVersion != CurrentSchemaVersion)
            {
                AddIssue(issues, HotUpdateBaseReleaseValidationIssueCode.UnsupportedSchemaVersion,
                    $"BaseRelease schema {storedRelease.SchemaVersion} is not supported (expected {CurrentSchemaVersion}).");
            }

            AddRequired(issues, storedRelease.BaseAppVersion, nameof(storedRelease.BaseAppVersion));
            AddRequired(issues, storedRelease.Architecture, nameof(storedRelease.Architecture));
            AddRequired(issues, storedRelease.UnityVersion, nameof(storedRelease.UnityVersion));
            AddRequired(issues, storedRelease.HybridCLRVersion, nameof(storedRelease.HybridCLRVersion));
            AddRequired(issues, storedRelease.YooAssetVersion, nameof(storedRelease.YooAssetVersion));
            AddRequired(issues, storedRelease.GitCommit, nameof(storedRelease.GitCommit));
            AddRequired(issues, storedRelease.CreatedAt, nameof(storedRelease.CreatedAt));

            Compare(issues, requirements.BaseAppVersion, storedRelease.BaseAppVersion,
                HotUpdateBaseReleaseValidationIssueCode.BaseAppVersionMismatch, "Base App version");
            Compare(issues, requirements.Platform, storedRelease.Platform,
                HotUpdateBaseReleaseValidationIssueCode.PlatformMismatch, "Build target");
            Compare(issues, requirements.Architecture, storedRelease.Architecture,
                HotUpdateBaseReleaseValidationIssueCode.ArchitectureMismatch, "Architecture");
            Compare(issues, requirements.UnityVersion, storedRelease.UnityVersion,
                HotUpdateBaseReleaseValidationIssueCode.UnityVersionMismatch, "Unity version");
            Compare(issues, requirements.HybridCLRVersion, storedRelease.HybridCLRVersion,
                HotUpdateBaseReleaseValidationIssueCode.HybridCLRVersionMismatch, "HybridCLR version");
            Compare(issues, requirements.YooAssetVersion, storedRelease.YooAssetVersion,
                HotUpdateBaseReleaseValidationIssueCode.YooAssetVersionMismatch, "YooAsset version");
            Compare(issues, requirements.ScriptingBackend, storedRelease.ScriptingBackend,
                HotUpdateBaseReleaseValidationIssueCode.ScriptingBackendMismatch, "Scripting backend");

            string[] metadataNames = storedRelease.AotMetadata ?? Array.Empty<string>();
            string[] metadataHashes = storedRelease.AotHashes ?? Array.Empty<string>();
            if (metadataNames.Length == 0 || metadataNames.Length != metadataHashes.Length)
            {
                AddIssue(issues, HotUpdateBaseReleaseValidationIssueCode.MetadataListInvalid,
                    "AOT metadata file and SHA256 lists must be non-empty and have equal lengths.");
            }
            else
            {
                string metadataDirectory = Path.Combine(
                    GetReleaseDirectory(storedRelease.Platform, storedRelease.BaseAppVersion),
                    MetadataDirectoryName);
                for (int index = 0; index < metadataNames.Length; index++)
                {
                    string fileName = metadataNames[index];
                    if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) ||
                        !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        AddIssue(issues, HotUpdateBaseReleaseValidationIssueCode.MetadataListInvalid,
                            $"AOT metadata entry '{fileName}' must be a DLL file name without a directory path.");
                        continue;
                    }

                    string metadataPath = Path.Combine(metadataDirectory, fileName);
                    if (!File.Exists(metadataPath))
                    {
                        AddIssue(issues, HotUpdateBaseReleaseValidationIssueCode.MetadataFileMissing,
                            $"AOT metadata file '{fileName}' is missing from the BaseRelease.");
                        continue;
                    }

                    string actualHash = ComputeSha256(metadataPath);
                    if (!string.Equals(actualHash, metadataHashes[index], StringComparison.OrdinalIgnoreCase))
                    {
                        AddIssue(issues, HotUpdateBaseReleaseValidationIssueCode.MetadataHashMismatch,
                            $"AOT metadata SHA256 mismatch for '{fileName}'.");
                    }
                }
            }

            return new HotUpdateBaseReleaseValidationResult(storedRelease, issues.ToArray());
        }

        /// <summary>加载并校验显式选择的 BaseRelease；缺失或不兼容时失败，不回退到临时 metadata。</summary>
        public HotUpdateBaseRelease LoadAndValidate(
            BuildTarget platform,
            string baseAppVersion,
            HotUpdateBaseReleaseRequirements requirements)
        {
            HotUpdateBaseRelease selected = Load(platform, baseAppVersion);
            HotUpdateBaseReleaseValidationResult validation = Validate(selected, requirements);
            if (!validation.IsValid)
            {
                string details = string.Join(
                    Environment.NewLine,
                    validation.Issues.Select(issue => $"[{issue.Code}] {issue.Message}"));
                throw new InvalidDataException(
                    $"BaseRelease '{platform}/{baseAppVersion}' is not compatible with this Hot Patch:{Environment.NewLine}{details}");
            }

            return validation.Release;
        }

        /// <summary>返回校验后的 BaseRelease metadata 绝对路径，供 HybridCLR Build Adapter 复制使用。</summary>
        public IReadOnlyList<string> GetAotMetadataPaths(
            HotUpdateBaseRelease selectedRelease,
            HotUpdateBaseReleaseRequirements requirements)
        {
            HotUpdateBaseReleaseValidationResult validation = Validate(selectedRelease, requirements);
            if (!validation.IsValid)
            {
                string details = string.Join(
                    Environment.NewLine,
                    validation.Issues.Select(issue => $"[{issue.Code}] {issue.Message}"));
                throw new InvalidDataException(
                    $"Selected BaseRelease cannot provide AOT metadata:{Environment.NewLine}{details}");
            }

            string metadataDirectory = Path.Combine(
                GetReleaseDirectory(validation.Release.Platform, validation.Release.BaseAppVersion),
                MetadataDirectoryName);
            return validation.Release.AotMetadata
                .Select(fileName => Path.Combine(metadataDirectory, fileName))
                .ToArray();
        }

        private void ValidateCreateRequest(HotUpdateBaseReleaseCreateRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ValidateVersion(request.BaseAppVersion);
            if (request.Platform == BuildTarget.NoTarget || !Enum.IsDefined(typeof(BuildTarget), request.Platform))
            {
                throw new ArgumentOutOfRangeException(nameof(request.Platform), "A concrete Unity BuildTarget is required.");
            }

            Require(request.Architecture, nameof(request.Architecture));
            Require(request.UnityVersion, nameof(request.UnityVersion));
            Require(request.HybridCLRVersion, nameof(request.HybridCLRVersion));
            Require(request.YooAssetVersion, nameof(request.YooAssetVersion));
            Require(request.GitCommit, nameof(request.GitCommit));
            if (request.AotMetadataSourcePaths == null || request.AotMetadataSourcePaths.Length == 0)
            {
                throw new ArgumentException("At least one AOT metadata DLL from this Base App build is required.",
                    nameof(request.AotMetadataSourcePaths));
            }

            foreach (string sourcePath in request.AotMetadataSourcePaths)
            {
                Require(sourcePath, nameof(request.AotMetadataSourcePaths));
                if (!File.Exists(Path.GetFullPath(sourcePath)))
                {
                    throw new FileNotFoundException(
                        $"AOT metadata source file was not found: '{sourcePath}'.",
                        sourcePath);
                }
            }
        }

        private string GetPlatformDirectory(BuildTarget platform)
        {
            if (platform == BuildTarget.NoTarget || !Enum.IsDefined(typeof(BuildTarget), platform))
            {
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "A concrete Unity BuildTarget is required.");
            }

            return Path.Combine(_repositoryRoot, platform.ToString());
        }

        private string GetReleaseDirectory(BuildTarget platform, string baseAppVersion)
        {
            ValidateVersion(baseAppVersion);
            return Path.Combine(GetPlatformDirectory(platform), baseAppVersion);
        }

        private static void ValidateVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version) ||
                !string.Equals(version, version.Trim(), StringComparison.Ordinal) ||
                !SafeVersionRegex.IsMatch(version))
            {
                throw new ArgumentException(
                    "Base App version must be a path-safe identifier containing letters, digits, '.', '+' or '-'.",
                    nameof(version));
            }
        }

        private static void Require(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"'{fieldName}' is required.", fieldName);
            }
        }

        private static DateTime ParseCreatedAt(string value)
        {
            if (!DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime createdAt))
            {
                throw new InvalidDataException($"BaseRelease CreatedAt value '{value}' is not valid ISO-8601.");
            }

            return createdAt;
        }

        private static string ComputeSha256(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static void Compare<T>(
            List<HotUpdateBaseReleaseValidationIssue> issues,
            T expected,
            T actual,
            HotUpdateBaseReleaseValidationIssueCode code,
            string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                AddIssue(issues, code, $"{label} mismatch: selected BaseRelease has '{actual}', target requires '{expected}'.");
            }
        }

        private static void Compare(
            List<HotUpdateBaseReleaseValidationIssue> issues,
            string expected,
            string actual,
            HotUpdateBaseReleaseValidationIssueCode code,
            string label)
        {
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(issues, code, $"{label} mismatch: selected BaseRelease has '{actual}', target requires '{expected}'.");
            }
        }

        private static void AddRequired(
            List<HotUpdateBaseReleaseValidationIssue> issues,
            string value,
            string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                AddIssue(issues, HotUpdateBaseReleaseValidationIssueCode.MissingField,
                    $"BaseRelease field '{label}' is required.");
            }
        }

        private static void AddIssue(
            List<HotUpdateBaseReleaseValidationIssue> issues,
            HotUpdateBaseReleaseValidationIssueCode code,
            string message)
        {
            issues.Add(new HotUpdateBaseReleaseValidationIssue(code, message));
        }
    }
}
