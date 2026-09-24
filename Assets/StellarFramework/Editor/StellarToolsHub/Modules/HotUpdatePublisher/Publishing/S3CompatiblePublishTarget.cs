using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>连接 S3-Compatible endpoint 的非秘密选项。</summary>
    public sealed class S3CompatiblePublishTargetOptions
    {
        public Uri ServiceEndpoint { get; set; }
        public string Bucket { get; set; } = string.Empty;
        public string Region { get; set; } = "us-east-1";
        public int RequestTimeoutMilliseconds { get; set; } = 120000;
    }

    /// <summary>只存在内存中的 S3 Access Key 凭证；不要序列化、日志输出或保存在 Profile。</summary>
    public sealed class S3CompatibleCredentials
    {
        public S3CompatibleCredentials(string accessKeyId, string secretAccessKey, string sessionToken = null)
        {
            AccessKeyId = accessKeyId ?? string.Empty;
            SecretAccessKey = secretAccessKey ?? string.Empty;
            SessionToken = sessionToken ?? string.Empty;
        }

        public string AccessKeyId { get; }
        public string SecretAccessKey { get; }
        public string SessionToken { get; }
    }

    /// <summary>无 SDK 依赖的 S3 对象信息。</summary>
    public sealed class S3CompatibleObjectInfo
    {
        public string Key { get; set; } = string.Empty;
        public long Length { get; set; }
        public string ETag { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }

    /// <summary>便于测试和替换传输实现的 S3 对象 API 边界。</summary>
    public interface IS3CompatibleObjectStoreClient
    {
        Task<S3CompatibleObjectInfo> GetInfoAsync(string key, CancellationToken cancellationToken);
        Task<string> GetObjectSha256Async(string key, CancellationToken cancellationToken);
        Task<string> GetTextAsync(string key, CancellationToken cancellationToken);
        Task PutFileAsync(string key, string sourcePath, long length, string sha256, CancellationToken cancellationToken);
        Task PutTextAsync(string key, string text, string expectedETag, bool createOnly, CancellationToken cancellationToken);
    }

    /// <summary>
    /// S3-Compatible publisher built on the existing target contract. Object keys are immutable;
    /// only the version pointer may change, using S3 conditional requests to prevent lost updates.
    /// </summary>
    public sealed class S3CompatiblePublishTarget : IHotUpdatePublishTarget
    {
        private readonly string _rootPrefix;
        private readonly IS3CompatibleObjectStoreClient _client;

        public S3CompatiblePublishTarget(
            HotUpdateEnvironmentProfile profile,
            S3CompatiblePublishTargetOptions options,
            IHotUpdateCredentialProvider credentialProvider,
            IS3CompatibleObjectStoreClient client = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (!string.Equals(profile.PublishTarget, "S3Compatible", StringComparison.Ordinal))
                throw new ArgumentException("Environment profile PublishTarget must be 'S3Compatible'.", nameof(profile));

            HotUpdateEnvironmentProfileValidationResult profileValidation = profile.Validate();
            if (!profileValidation.IsValid)
                throw new ArgumentException("Environment profile is invalid: " + string.Join(Environment.NewLine, profileValidation.Errors), nameof(profile));
            ValidateOptions(options);
            _rootPrefix = profile.RemoteRoot.Trim('/');

            if (client != null)
            {
                _client = client;
                return;
            }
            if (credentialProvider == null) throw new ArgumentNullException(nameof(credentialProvider));
            S3CompatibleCredentials credentials = ResolveCredentials(profile.CredentialProfileName, credentialProvider);
            if (options.ServiceEndpoint.Scheme != Uri.UriSchemeHttps && !options.ServiceEndpoint.IsLoopback)
                throw new ArgumentException("S3 credentials may only be sent to HTTPS endpoints (HTTP loopback is allowed for local development).", nameof(options));
            _client = new S3CompatibleSignedObjectStoreClient(options, credentials);
        }

        public async Task<HotUpdatePublishTargetFileInfo> UploadAsync(
            HotUpdatePublishFile file,
            CancellationToken cancellationToken)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSourceSnapshot(file);
            string key = ResolveObjectKey(file.RelativePath);
            S3CompatibleObjectInfo existing = await _client.GetInfoAsync(key, cancellationToken);
            if (existing != null)
            {
                string existingHash = await _client.GetObjectSha256Async(key, cancellationToken);
                return EnsureImmutableMatch(file, existing, existingHash);
            }

            try
            {
                await _client.PutFileAsync(key, file.SourcePath, file.Length, file.Sha256, cancellationToken);
            }
            catch (S3CompatiblePreconditionFailedException)
            {
                S3CompatibleObjectInfo racedObject = await _client.GetInfoAsync(key, cancellationToken);
                if (racedObject == null) throw;
                string racedHash = await _client.GetObjectSha256Async(key, cancellationToken);
                return EnsureImmutableMatch(file, racedObject, racedHash);
            }

            S3CompatibleObjectInfo uploaded = await _client.GetInfoAsync(key, cancellationToken);
            if (uploaded == null) throw new IOException($"Uploaded S3 object '{key}' is not visible after PUT.");
            string uploadedHash = await _client.GetObjectSha256Async(key, cancellationToken);
            return EnsureImmutableMatch(file, uploaded, uploadedHash);
        }

        public async Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _client.GetInfoAsync(ResolveObjectKey(relativePath), cancellationToken) != null;
        }

        public async Task<HotUpdatePublishTargetFileInfo> GetInfoAsync(
            string relativePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = ResolveObjectKey(relativePath);
            S3CompatibleObjectInfo info = await _client.GetInfoAsync(key, cancellationToken);
            if (info == null) return null;
            string sha256 = string.IsNullOrWhiteSpace(info.Sha256)
                ? await _client.GetObjectSha256Async(key, cancellationToken)
                : info.Sha256;
            return new HotUpdatePublishTargetFileInfo(relativePath, info.Length, sha256);
        }

        public async Task VerifyAsync(IReadOnlyList<HotUpdatePublishFile> files, CancellationToken cancellationToken)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            for (int index = 0; index < files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HotUpdatePublishFile file = files[index];
                if (file == null) throw new ArgumentException($"Publish file at index {index} is null.", nameof(files));
                ValidateSourceSnapshot(file);
                string key = ResolveObjectKey(file.RelativePath);
                S3CompatibleObjectInfo info = await _client.GetInfoAsync(key, cancellationToken);
                if (info == null) throw new FileNotFoundException($"S3 object '{key}' is missing.");
                if (info.Length != file.Length)
                    throw new IOException($"S3 object '{key}' length mismatch. Expected={file.Length}, Actual={info.Length}.");
                string remoteHash = await _client.GetObjectSha256Async(key, cancellationToken);
                if (!string.Equals(remoteHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"S3 object '{key}' SHA256 mismatch. Expected={file.Sha256}, Actual={remoteHash}.");
            }
        }

        public Task PublishVersionAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken)
        {
            return PublishVersionPointerAsync(request, cancellationToken);
        }

        public Task RollbackAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken)
        {
            return PublishVersionPointerAsync(request, cancellationToken);
        }

        private async Task PublishVersionPointerAsync(
            HotUpdateVersionPublishRequest request,
            CancellationToken cancellationToken)
        {
            ValidateVersionRequest(request);
            cancellationToken.ThrowIfCancellationRequested();
            string key = ResolveObjectKey(request.PointerRelativePath);
            S3CompatibleObjectInfo currentInfo = await _client.GetInfoAsync(key, cancellationToken);
            string currentVersion = currentInfo == null
                ? string.Empty
                : (await _client.GetTextAsync(key, cancellationToken)).Trim();
            if (!string.Equals(currentVersion, request.ExpectedCurrentPackageVersion ?? string.Empty, StringComparison.Ordinal))
                throw new IOException($"S3 version pointer changed before publish. Expected='{request.ExpectedCurrentPackageVersion}', actual='{currentVersion}'.");

            await _client.PutTextAsync(
                key,
                request.PackageVersion + "\n",
                currentInfo?.ETag,
                currentInfo == null,
                cancellationToken);
        }

        private string ResolveObjectKey(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            return string.IsNullOrEmpty(_rootPrefix) ? normalized : _rootPrefix + "/" + normalized;
        }

        private static HotUpdatePublishTargetFileInfo EnsureImmutableMatch(
            HotUpdatePublishFile file,
            S3CompatibleObjectInfo existing,
            string actualHash)
        {
            if (existing.Length != file.Length || !string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Immutable S3 key '{file.RelativePath}' already exists with different content; it was not overwritten.");
            return new HotUpdatePublishTargetFileInfo(file.RelativePath, existing.Length, actualHash);
        }

        private static void ValidateSourceSnapshot(HotUpdatePublishFile file)
        {
            if (!File.Exists(file.SourcePath)) throw new FileNotFoundException("Publish source file is missing.", file.SourcePath);
            var info = new FileInfo(file.SourcePath);
            if (info.Length != file.Length || !string.Equals(
                    HotUpdatePublishFile.ComputeSha256(file.SourcePath), file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"S3 source file '{file.RelativePath}' changed after its SHA256 snapshot was created.");
        }

        private void ValidateVersionRequest(HotUpdateVersionPublishRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            NormalizeRelativePath(request.PointerRelativePath);
            if (string.IsNullOrWhiteSpace(request.PackageVersion) || request.PackageVersion.Length > 64)
                throw new ArgumentException("PackageVersion must be a path-safe identifier.", nameof(request));
            for (int index = 0; index < request.PackageVersion.Length; index++)
            {
                char character = request.PackageVersion[index];
                bool valid = (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z') ||
                             (character >= '0' && character <= '9') || (index > 0 && (character == '.' || character == '+' || character == '-'));
                if (!valid) throw new ArgumentException("PackageVersion must be a path-safe identifier.", nameof(request));
            }
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("Object relative path is required.", nameof(relativePath));
            string normalized = relativePath.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains(":"))
                throw new ArgumentException("S3 object paths must be relative.", nameof(relativePath));
            string[] segments = normalized.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(segments[index]) || segments[index] == "." || segments[index] == "..")
                    throw new ArgumentException("S3 object paths cannot contain empty, '.' or '..' segments.", nameof(relativePath));
            }
            return normalized;
        }

        private static void ValidateOptions(S3CompatiblePublishTargetOptions options)
        {
            if (options.ServiceEndpoint == null || !options.ServiceEndpoint.IsAbsoluteUri ||
                (options.ServiceEndpoint.Scheme != Uri.UriSchemeHttps && options.ServiceEndpoint.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(options.ServiceEndpoint.UserInfo) ||
                !string.IsNullOrEmpty(options.ServiceEndpoint.Query) ||
                !string.IsNullOrEmpty(options.ServiceEndpoint.Fragment))
                throw new ArgumentException("S3 service endpoint must be an absolute HTTP(S) URL without user info, query or fragment.", nameof(options));
            if (!IsValidBucket(options.Bucket))
                throw new ArgumentException("S3 bucket name is invalid.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.Region) || options.Region.Contains("/") || options.Region.Contains(" "))
                throw new ArgumentException("S3 region is invalid.", nameof(options));
        }

        private static bool IsValidBucket(string bucket)
        {
            if (string.IsNullOrWhiteSpace(bucket) || bucket.Length < 3 || bucket.Length > 63 ||
                bucket.StartsWith(".", StringComparison.Ordinal) || bucket.EndsWith(".", StringComparison.Ordinal) ||
                bucket.StartsWith("-", StringComparison.Ordinal) || bucket.EndsWith("-", StringComparison.Ordinal) ||
                bucket.Contains("..") || bucket.Contains(".-") || bucket.Contains("-.") ||
                Uri.CheckHostName(bucket) == UriHostNameType.IPv4)
                return false;

            for (int index = 0; index < bucket.Length; index++)
            {
                char character = bucket[index];
                bool valid = (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '.' || character == '-';
                if (!valid) return false;
            }
            return true;
        }

        private static S3CompatibleCredentials ResolveCredentials(
            string credentialProfileName,
            IHotUpdateCredentialProvider credentialProvider)
        {
            if (!credentialProvider.TryGetSecret(credentialProfileName, out string secret) || string.IsNullOrEmpty(secret))
                throw new InvalidOperationException($"S3 credentials for profile '{credentialProfileName}' are not available from the configured environment variable.");

            try
            {
                S3CredentialEnvironmentValue document = JsonUtility.FromJson<S3CredentialEnvironmentValue>(secret);
                if (document == null || string.IsNullOrWhiteSpace(document.accessKeyId) || string.IsNullOrWhiteSpace(document.secretAccessKey))
                    throw new InvalidDataException("S3 credential environment variable must contain accessKeyId and secretAccessKey JSON fields.");
                return new S3CompatibleCredentials(document.accessKeyId, document.secretAccessKey, document.sessionToken);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidDataException)
            {
                throw new InvalidDataException("S3 credential environment variable has an invalid JSON credential document. Secret content is not included in diagnostics.", exception);
            }
            finally
            {
                secret = null;
            }
        }

        [Serializable]
        private sealed class S3CredentialEnvironmentValue
        {
            public string accessKeyId;
            public string secretAccessKey;
            public string sessionToken;
        }
    }
}
