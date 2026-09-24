using System;
using System.Collections.Generic;

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
        /// <summary>Absolute mounted-folder root used only when PublishTarget is LocalFolder.</summary>
        public string LocalFolderRoot;
        public string CredentialProfileName;

        /// <summary>创建一个空配置的标准环境模板，不假定任何服务地址或凭证。</summary>
        public static HotUpdateEnvironmentProfile CreateDefault(HotUpdateEnvironmentKind environment)
        {
            if (!Enum.IsDefined(typeof(HotUpdateEnvironmentKind), environment))
                throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unknown HotUpdate environment.");

            return new HotUpdateEnvironmentProfile
            {
                EnvironmentId = environment.ToString(),
                MainHostServer = string.Empty,
                FallbackHostServer = string.Empty,
                RemoteRoot = "hotupdate/" + environment,
                PublishTarget = "LocalFolder",
                LocalFolderRoot = string.Empty,
                CredentialProfileName = string.Empty
            };
        }

        /// <summary>检查非秘密 Profile 字段是否安全且可由当前 Publisher 使用。</summary>
        public HotUpdateEnvironmentProfileValidationResult Validate()
        {
            var errors = new List<string>();
            if (!IsSupportedEnvironmentId(EnvironmentId))
                errors.Add("EnvironmentId must be Development, Staging or Production.");
            ValidateHost(MainHostServer, "MainHostServer", required: true, errors);
            ValidateHost(FallbackHostServer, "FallbackHostServer", required: false, errors);
            if (string.Equals(EnvironmentId, nameof(HotUpdateEnvironmentKind.Production), StringComparison.Ordinal) &&
                Uri.TryCreate(MainHostServer, UriKind.Absolute, out Uri productionHost) &&
                productionHost.Scheme != Uri.UriSchemeHttps)
                errors.Add("Production MainHostServer must use HTTPS.");
            if (string.Equals(EnvironmentId, nameof(HotUpdateEnvironmentKind.Production), StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(FallbackHostServer) &&
                Uri.TryCreate(FallbackHostServer, UriKind.Absolute, out Uri productionFallback) &&
                productionFallback.Scheme != Uri.UriSchemeHttps)
                errors.Add("Production FallbackHostServer must use HTTPS.");
            ValidateRemoteRoot(RemoteRoot, errors);
            if (string.IsNullOrWhiteSpace(PublishTarget) || !IsIdentifier(PublishTarget))
                errors.Add("PublishTarget must be a non-empty identifier containing letters, digits, '_' or '-'.");
            if (!string.IsNullOrWhiteSpace(CredentialProfileName) && !IsIdentifier(CredentialProfileName))
                errors.Add("CredentialProfileName must contain only letters, digits, '_' or '-'.");
            return new HotUpdateEnvironmentProfileValidationResult(errors.ToArray());
        }

        private static void ValidateHost(string value, string fieldName, bool required, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required) errors.Add(fieldName + " is required.");
                return;
            }

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                errors.Add(fieldName + " must be an absolute HTTP(S) base URL without user info, query or fragment.");
            }
        }

        private static void ValidateRemoteRoot(string value, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add("RemoteRoot is required.");
                return;
            }

            string normalized = value.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.Contains(":") ||
                normalized.Contains("%") ||
                normalized.IndexOfAny(new[] { '?', '#' }) >= 0)
            {
                errors.Add("RemoteRoot must be a relative path without URI scheme, query or fragment.");
                return;
            }

            string[] segments = normalized.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index] == ".." || segments[index] == "." || string.IsNullOrWhiteSpace(segments[index]))
                {
                    errors.Add("RemoteRoot cannot contain empty, '.' or '..' path segments.");
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
