using System;
using System.Collections.Generic;
using System.IO;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>支持的发布环境。</summary>
    public enum HotUpdateEnvironmentKind
    {
        Development = 0,
        Staging = 1,
        Production = 2
    }

    /// <summary>
    /// 非秘密的发布环境配置。凭证只保存 profile name；Secret 必须由凭证 Provider 在运行时读取。
    /// </summary>
    [Serializable]
    public sealed class HotUpdateEnvironmentProfile
    {
        public string EnvironmentId;
        public string MainHostServer;
        public string FallbackHostServer;
        public string RemoteRoot;
        public string PublishTarget;
        /// <summary>Absolute local publish root or mounted-folder root used by LocalFolder.</summary>
        public string LocalFolderRoot;
        public string CredentialProfileName;
        /// <summary>Non-secret S3-compatible service endpoint used only when PublishTarget is S3Compatible.</summary>
        public string S3ServiceEndpoint;
        /// <summary>Non-secret bucket name used only when PublishTarget is S3Compatible.</summary>
        public string S3Bucket;
        /// <summary>Non-secret S3-compatible signing region.</summary>
        public string S3Region;

        /// <summary>创建标准环境模板。Development 默认使用项目内的本地热更目录。</summary>
        public static HotUpdateEnvironmentProfile CreateDefault(HotUpdateEnvironmentKind environment)
        {
            if (!Enum.IsDefined(typeof(HotUpdateEnvironmentKind), environment))
                throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unknown HotUpdate environment.");

            string remoteRoot = "hotupdate/" + environment;
            string localFolderRoot = environment == HotUpdateEnvironmentKind.Development
                ? GetDefaultLocalFolderRoot()
                : string.Empty;

            return new HotUpdateEnvironmentProfile
            {
                EnvironmentId = environment.ToString(),
                MainHostServer = environment == HotUpdateEnvironmentKind.Development
                    ? CreateLocalFileHost(localFolderRoot, remoteRoot)
                    : string.Empty,
                FallbackHostServer = string.Empty,
                RemoteRoot = remoteRoot,
                PublishTarget = "LocalFolder",
                LocalFolderRoot = localFolderRoot,
                CredentialProfileName = string.Empty,
                S3ServiceEndpoint = string.Empty,
                S3Bucket = string.Empty,
                S3Region = "us-east-1"
            };
        }

        /// <summary>检查非秘密 Profile 字段是否安全且可由当前 Publisher 使用。</summary>
        public HotUpdateEnvironmentProfileValidationResult Validate()
        {
            var errors = new List<string>();
            if (!IsSupportedEnvironmentId(EnvironmentId))
                errors.Add("发布环境必须选择“开发 / 预发布 / 正式环境”之一。");
            bool allowLocalFileHost =
                string.Equals(EnvironmentId, nameof(HotUpdateEnvironmentKind.Development), StringComparison.Ordinal) &&
                string.Equals(PublishTarget, "LocalFolder", StringComparison.Ordinal);
            ValidateHost(MainHostServer, "MainHostServer", true, allowLocalFileHost, errors);
            ValidateHost(FallbackHostServer, "FallbackHostServer", false, allowLocalFileHost, errors);
            if (string.Equals(EnvironmentId, nameof(HotUpdateEnvironmentKind.Production), StringComparison.Ordinal) &&
                Uri.TryCreate(MainHostServer, UriKind.Absolute, out Uri productionHost) &&
                productionHost.Scheme != Uri.UriSchemeHttps)
                errors.Add("正式环境的主下载地址必须使用 HTTPS。");
            if (string.Equals(EnvironmentId, nameof(HotUpdateEnvironmentKind.Production), StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(FallbackHostServer) &&
                Uri.TryCreate(FallbackHostServer, UriKind.Absolute, out Uri productionFallback) &&
                productionFallback.Scheme != Uri.UriSchemeHttps)
                errors.Add("正式环境的备用下载地址必须使用 HTTPS。");
            ValidateRemoteRoot(RemoteRoot, errors);
            if (string.IsNullOrWhiteSpace(PublishTarget) || !IsIdentifier(PublishTarget))
                errors.Add("发布方式必须填写有效标识。");
            if (!string.IsNullOrWhiteSpace(CredentialProfileName) && !IsIdentifier(CredentialProfileName))
                errors.Add("凭证配置名称只能包含英文字母、数字、下划线或连字符。");
            return new HotUpdateEnvironmentProfileValidationResult(errors.ToArray());
        }

        /// <summary>Returns the ignored project-local package output root used by a new Development profile.</summary>
        public static string GetDefaultLocalFolderRoot()
        {
            return Path.GetFullPath(Path.Combine("BuildArtifacts", "HotUpdate", "Local"));
        }

        /// <summary>Creates the file URI that points at the published package directory.</summary>
        public static string CreateLocalFileHost(string localFolderRoot, string remoteRoot)
        {
            if (string.IsNullOrWhiteSpace(localFolderRoot)) throw new ArgumentException("Local folder root is required.", nameof(localFolderRoot));
            if (string.IsNullOrWhiteSpace(remoteRoot)) throw new ArgumentException("Remote root is required.", nameof(remoteRoot));

            string packageDirectory = Path.GetFullPath(Path.Combine(
                localFolderRoot,
                remoteRoot.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
            if (!packageDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                packageDirectory += Path.DirectorySeparatorChar;
            return new Uri(packageDirectory, UriKind.Absolute).AbsoluteUri;
        }

        private static void ValidateHost(string value, string fieldName, bool required, bool allowLocalFileHost, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required) errors.Add(fieldName + " is required.");
                return;
            }

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri) ||
                !IsSafeHostUri(uri, allowLocalFileHost))
            {
                errors.Add(allowLocalFileHost
                    ? fieldName + " must be an HTTP(S) base URL or a local file:/// directory URI, without user info, query or fragment."
                    : fieldName + " must be an absolute HTTP(S) base URL without user info, query or fragment.");
            }
        }

        private static bool IsSafeHostUri(Uri uri, bool allowLocalFileHost)
        {
            if (uri == null || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                return false;

            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                return !string.IsNullOrWhiteSpace(uri.Host);

            // Development can read a package directly from a local disk folder. UNC paths are
            // intentionally excluded so this option cannot silently become a network share.
            return allowLocalFileHost && uri.IsFile && !uri.IsUnc && string.IsNullOrEmpty(uri.Host);
        }

        private static void ValidateRemoteRoot(string value, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add("服务器目录不能为空。");
                return;
            }

            string normalized = value.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.Contains(":") ||
                normalized.Contains("%") ||
                normalized.IndexOfAny(new[] { '?', '#' }) >= 0)
            {
                errors.Add("服务器目录必须是相对路径，不能包含 URL 协议、查询参数或片段。");
                return;
            }

            string[] segments = normalized.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index] == ".." || segments[index] == "." || string.IsNullOrWhiteSpace(segments[index]))
                {
                    errors.Add("服务器目录不能包含空目录、“.” 或“..”路径段。");
                    return;
                }
            }
        }

        private static bool IsSupportedEnvironmentId(string value)
        {
            return string.Equals(value, nameof(HotUpdateEnvironmentKind.Development), StringComparison.Ordinal) ||
                   string.Equals(value, nameof(HotUpdateEnvironmentKind.Staging), StringComparison.Ordinal) ||
                   string.Equals(value, nameof(HotUpdateEnvironmentKind.Production), StringComparison.Ordinal);
        }

        private static bool IsIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool asciiLetter = (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z');
                bool asciiDigit = character >= '0' && character <= '9';
                if (!asciiLetter && !asciiDigit && character != '_' && character != '-') return false;
            }
            return true;
        }
    }

    /// <summary>环境配置校验结果。</summary>
    public sealed class HotUpdateEnvironmentProfileValidationResult
    {
        public HotUpdateEnvironmentProfileValidationResult(string[] errors)
        {
            Errors = errors ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }

    /// <summary>Unity JsonUtility 可持久化的非秘密环境配置集合。</summary>
    [Serializable]
    public sealed class HotUpdateEnvironmentProfileDocument
    {
        public HotUpdateEnvironmentProfile[] Profiles = Array.Empty<HotUpdateEnvironmentProfile>();
    }
}
