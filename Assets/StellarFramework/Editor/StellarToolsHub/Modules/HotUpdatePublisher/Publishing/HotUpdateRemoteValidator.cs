using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>一个流式 HTTP GET 的验证结果；大对象不会被整体缓存在内存中。</summary>
    public sealed class HotUpdateRemoteHttpResponse
    {
        public int StatusCode { get; set; }
        public long ContentLength { get; set; } = -1;
        public long BytesRead { get; set; }
        public long? ContentRangeStart { get; set; }
        public long? ContentRangeTotal { get; set; }
        public string AcceptRanges { get; set; } = string.Empty;
        public string BodyText { get; set; } = string.Empty;
    }

    /// <summary>可替换的 HTTP GET 边界，测试不需要外网/CDN。</summary>
    public interface IHotUpdateRemoteHttpClient
    {
        Task<HotUpdateRemoteHttpResponse> GetAsync(
            Uri uri,
            long? rangeStart,
            int maxCapturedBodyBytes,
            CancellationToken cancellationToken);
    }

    /// <summary>BCL HTTP 实现，流式读取响应并限制只捕获小型 Version/Manifest 正文。</summary>
    public sealed class HotUpdateRemoteHttpClient : IHotUpdateRemoteHttpClient
    {
        private const int BufferSize = 81920;
        private readonly int _timeoutMilliseconds;

        public HotUpdateRemoteHttpClient(int timeoutMilliseconds = 30000)
        {
            if (timeoutMilliseconds < 1000 || timeoutMilliseconds > 900000)
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "Timeout must be between 1000 and 900000 milliseconds.");
            _timeoutMilliseconds = timeoutMilliseconds;
        }

        public async Task<HotUpdateRemoteHttpResponse> GetAsync(
            Uri uri,
            long? rangeStart,
            int maxCapturedBodyBytes,
            CancellationToken cancellationToken)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (maxCapturedBodyBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxCapturedBodyBytes));
            cancellationToken.ThrowIfCancellationRequested();
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.Timeout = _timeoutMilliseconds;
            request.ReadWriteTimeout = _timeoutMilliseconds;
            if (rangeStart.HasValue) request.AddRange(rangeStart.Value);

            using (var registration = cancellationToken.Register(request.Abort))
            {
                HttpWebResponse response;
                try
                {
                    response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
                }
                catch (WebException exception) when (exception.Response is HttpWebResponse httpResponse)
                {
                    response = httpResponse;
                }
                catch (WebException exception) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Remote verification request was cancelled.", exception, cancellationToken);
                }

                using (response)
                using (Stream stream = response.GetResponseStream())
                {
                    var result = new HotUpdateRemoteHttpResponse
                    {
                        StatusCode = (int)response.StatusCode,
                        ContentLength = response.ContentLength,
                        AcceptRanges = response.Headers[HttpResponseHeader.AcceptRanges] ?? string.Empty
                    };
                    ParseContentRange(response.Headers[HttpResponseHeader.ContentRange], result);
                    if (stream == null) return result;

                    byte[] buffer = new byte[BufferSize];
                    MemoryStream capturedBody = maxCapturedBodyBytes > 0 ? new MemoryStream() : null;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        result.BytesRead += read;
                        if (capturedBody != null)
                        {
                            if (result.BytesRead > maxCapturedBodyBytes)
                                throw new IOException($"Remote verification response exceeded the {maxCapturedBodyBytes}-byte capture limit.");
                            capturedBody.Write(buffer, 0, read);
                        }
                    }

                    if (capturedBody != null)
                    {
                        using (capturedBody)
                            result.BodyText = Encoding.UTF8.GetString(capturedBody.ToArray());
                    }
                    return result;
                }
            }
        }

        private static void ParseContentRange(string value, HotUpdateRemoteHttpResponse response)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("bytes ", StringComparison.OrdinalIgnoreCase)) return;
            int dashIndex = value.IndexOf('-', 6);
            int slashIndex = value.IndexOf('/', dashIndex + 1);
            if (dashIndex < 0 || slashIndex < 0) return;
            if (long.TryParse(value.Substring(6, dashIndex - 6), NumberStyles.None, CultureInfo.InvariantCulture, out long start))
                response.ContentRangeStart = start;
            if (long.TryParse(value.Substring(slashIndex + 1), NumberStyles.None, CultureInfo.InvariantCulture, out long total))
                response.ContentRangeTotal = total;
        }
    }

    /// <summary>验证当前版本指针、候选 Manifest/Bundle、HTTP GET、长度、Range 与配置的回退 Host。</summary>
    public sealed class HotUpdateRemoteValidator : IHotUpdatePrePublishRemoteVerifier, IHotUpdateHistoricalReleaseRemoteVerifier
    {
        public const long RequiredRangeStart = 262144;
        private const int MaxVersionBytes = 4096;
        private const int MaxManifestBytes = 4 * 1024 * 1024;

        private readonly HotUpdateEnvironmentProfile _profile;
        private readonly IHotUpdateRemoteHttpClient _httpClient;
        private readonly Func<int, int> _selectBundleIndex;

        public HotUpdateRemoteValidator(
            HotUpdateEnvironmentProfile profile,
            IHotUpdateRemoteHttpClient httpClient = null,
            Func<int, int> selectBundleIndex = null)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            HotUpdateEnvironmentProfileValidationResult validation = profile.Validate();
            if (!validation.IsValid)
                throw new ArgumentException("Environment profile is invalid: " + string.Join(Environment.NewLine, validation.Errors), nameof(profile));
            if (string.IsNullOrWhiteSpace(profile.MainHostServer))
                throw new ArgumentException("MainHostServer must point to the YooAsset remote file directory.", nameof(profile));
            _httpClient = httpClient ?? new HotUpdateRemoteHttpClient();
            _selectBundleIndex = selectBundleIndex ?? SelectRandomBundleIndex;
        }

        public async Task VerifyAsync(
            HotUpdatePublishContext context,
            IReadOnlyList<HotUpdatePublishFile> immutableFiles,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (immutableFiles == null) throw new ArgumentNullException(nameof(immutableFiles));
            if (context.YooAssetBuildOutput == null || context.VersionPublishRequest == null)
                throw new InvalidOperationException("Remote verification requires prepared YooAsset output and a version pointer request.");
            if (immutableFiles.Count == 0) throw new InvalidOperationException("Remote verification requires immutable package files.");

            HotUpdatePublishFile manifestFile = FindManifestFile(context, immutableFiles);
            List<HotUpdatePublishFile> bundles = FindLargeBundleFiles(immutableFiles);
            if (bundles.Count == 0)
                throw new InvalidOperationException($"Remote Range verification requires a bundle larger than {RequiredRangeStart} bytes.");
            int selectedIndex = _selectBundleIndex(bundles.Count);
            if (selectedIndex < 0 || selectedIndex >= bundles.Count)
                throw new InvalidOperationException("Bundle selector returned an out-of-range index.");
            HotUpdatePublishFile bundleFile = bundles[selectedIndex];

            var hosts = new List<Uri>(2) { NormalizeHost(_profile.MainHostServer) };
            if (!string.IsNullOrWhiteSpace(_profile.FallbackHostServer))
            {
                Uri fallback = NormalizeHost(_profile.FallbackHostServer);
                if (!string.Equals(hosts[0].AbsoluteUri, fallback.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
                    hosts.Add(fallback);
            }

            for (int index = 0; index < hosts.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Uri host = hosts[index];
                await VerifyVersionPointerAsync(
                    host,
                    context.VersionPublishRequest.PointerRelativePath,
                    context.VersionPublishRequest.ExpectedCurrentPackageVersion,
                    cancellationToken);
                await VerifyFileAsync(host, manifestFile, captureBodyBytes: MaxManifestBytes, cancellationToken);
                await VerifyFileAsync(host, bundleFile, captureBodyBytes: 0, cancellationToken);
                await VerifyRangeAsync(host, bundleFile, cancellationToken);
            }
        }

        public async Task VerifyRollbackAsync(HotUpdateReleaseRecord release, CancellationToken cancellationToken)
        {
            if (release == null) throw new ArgumentNullException(nameof(release));
            if (release.Files == null || release.Files.Length == 0 || release.ManifestFiles == null || release.ManifestFiles.Length < 4)
                throw new InvalidOperationException("Rollback verification requires historical Manifest names and file integrity evidence.");

            string manifestPath = release.ManifestFiles[1];
            IHotUpdateRemoteFile manifest = FindHistoricalFile(release.Files, manifestPath);
            IHotUpdateRemoteFile bundle = FindHistoricalRangeBundle(release.Files);
            var hosts = new List<Uri>(2) { NormalizeHost(_profile.MainHostServer) };
            if (!string.IsNullOrWhiteSpace(_profile.FallbackHostServer))
            {
                Uri fallback = NormalizeHost(_profile.FallbackHostServer);
                if (!string.Equals(hosts[0].AbsoluteUri, fallback.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
                    hosts.Add(fallback);
            }

            for (int index = 0; index < hosts.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Uri host = hosts[index];
                await VerifyVersionPointerAsync(host, release.ManifestFiles[3], release.PackageVersion, cancellationToken);
                await VerifyFileAsync(host, manifest, MaxManifestBytes, cancellationToken);
                await VerifyFileAsync(host, bundle, 0, cancellationToken);
                await VerifyRangeAsync(host, bundle, cancellationToken);
            }
        }

        private async Task VerifyVersionPointerAsync(
            Uri host,
            string pointerRelativePath,
            string expectedCurrentVersion,
            CancellationToken cancellationToken)
        {
            Uri uri = Combine(host, pointerRelativePath);
            HotUpdateRemoteHttpResponse response = await _httpClient.GetAsync(uri, null, MaxVersionBytes, cancellationToken);
            if (response == null) throw new IOException($"Remote version pointer '{pointerRelativePath}' returned no HTTP response from '{host}'.");
            if (response.StatusCode == 404 && string.IsNullOrEmpty(expectedCurrentVersion)) return;
            EnsureStatus(response, 200, pointerRelativePath, host);
            if (response.ContentLength < 0 || response.ContentLength != response.BytesRead || response.BytesRead > MaxVersionBytes)
                throw new IOException($"Remote version pointer '{pointerRelativePath}' has an invalid Content-Length at '{host}'.");
            string actualVersion = (response.BodyText ?? string.Empty).Trim();
            if (!string.Equals(actualVersion, expectedCurrentVersion ?? string.Empty, StringComparison.Ordinal))
                throw new IOException($"Remote version pointer at '{host}' changed before publish. Expected='{expectedCurrentVersion}', actual='{actualVersion}'.");
        }

        private async Task VerifyFileAsync(
            Uri host,
            IHotUpdateRemoteFile file,
            int captureBodyBytes,
            CancellationToken cancellationToken)
        {
            HotUpdateRemoteHttpResponse response = await _httpClient.GetAsync(
                Combine(host, file.RelativePath), null, captureBodyBytes, cancellationToken);
            EnsureStatus(response, 200, file.RelativePath, host);
            if (response.ContentLength != file.Length || response.BytesRead != file.Length)
                throw new IOException($"Remote file '{file.RelativePath}' length mismatch at '{host}'. Expected={file.Length}, header={response.ContentLength}, read={response.BytesRead}.");
        }

        private async Task VerifyRangeAsync(Uri host, IHotUpdateRemoteFile file, CancellationToken cancellationToken)
        {
            HotUpdateRemoteHttpResponse response = await _httpClient.GetAsync(
                Combine(host, file.RelativePath), RequiredRangeStart, 0, cancellationToken);
            EnsureStatus(response, 206, file.RelativePath + " (Range)", host);
            long expectedContentLength = file.Length - RequiredRangeStart;
            if (response.ContentRangeStart != RequiredRangeStart || response.ContentRangeTotal != file.Length ||
                response.ContentLength != expectedContentLength || response.BytesRead != expectedContentLength)
                throw new IOException($"Remote Range response for '{file.RelativePath}' is invalid at '{host}'. Expected start={RequiredRangeStart}, total={file.Length}, content length={expectedContentLength}.");
        }

        private static HotUpdatePublishFile FindManifestFile(
            HotUpdatePublishContext context,
            IReadOnlyList<HotUpdatePublishFile> files)
        {
            string[] manifestNames = context.YooAssetBuildOutput.ManifestFiles;
            if (manifestNames == null || manifestNames.Length < 2)
                throw new InvalidOperationException("YooAsset output must identify the JSON Manifest file.");
            string manifestName = manifestNames[1];
            for (int index = 0; index < files.Count; index++)
                if (string.Equals(files[index].RelativePath, manifestName, StringComparison.OrdinalIgnoreCase)) return files[index];
            throw new FileNotFoundException($"JSON Manifest '{manifestName}' is not present in the immutable publish set.");
        }

        private static List<HotUpdatePublishFile> FindLargeBundleFiles(IReadOnlyList<HotUpdatePublishFile> files)
        {
            var bundles = new List<HotUpdatePublishFile>();
            for (int index = 0; index < files.Count; index++)
            {
                HotUpdatePublishFile file = files[index];
                if (file.Length > RequiredRangeStart && file.RelativePath.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                    bundles.Add(file);
            }
            return bundles;
        }

        private static IHotUpdateRemoteFile FindHistoricalFile(HotUpdateReleaseFileRecord[] files, string relativePath)
        {
            for (int index = 0; index < files.Length; index++)
                if (string.Equals(files[index].RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)) return files[index];
            throw new FileNotFoundException($"Historical JSON Manifest '{relativePath}' is missing from the release record.");
        }

        private static IHotUpdateRemoteFile FindHistoricalRangeBundle(HotUpdateReleaseFileRecord[] files)
        {
            for (int index = 0; index < files.Length; index++)
                if (files[index].Length > RequiredRangeStart && files[index].RelativePath.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                    return files[index];
            throw new InvalidOperationException($"Historical release has no bundle larger than {RequiredRangeStart} bytes for Range verification.");
        }

        private static void EnsureStatus(HotUpdateRemoteHttpResponse response, int expectedStatus, string filePath, Uri host)
        {
            if (response == null) throw new IOException($"Remote request for '{filePath}' returned no HTTP response from '{host}'.");
            if (response.StatusCode != expectedStatus)
                throw new IOException($"Remote request for '{filePath}' at '{host}' returned HTTP {response.StatusCode}; expected {expectedStatus}.");
        }

        private static Uri NormalizeHost(string host)
        {
            if (!Uri.TryCreate(host, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Remote host must be an absolute HTTP(S) directory URL without credentials, query or fragment.", nameof(host));
            string normalized = uri.AbsoluteUri.TrimEnd('/') + "/";
            return new Uri(normalized, UriKind.Absolute);
        }

        private static Uri Combine(Uri host, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath.StartsWith("/", StringComparison.Ordinal) || relativePath.Contains("\\"))
                throw new ArgumentException("Remote package file path must be relative and slash-normalized.", nameof(relativePath));
            string[] segments = relativePath.Split('/');
            var escapedPath = new StringBuilder();
            for (int index = 0; index < segments.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(segments[index]) || segments[index] == "." || segments[index] == "..")
                    throw new ArgumentException("Remote package file path contains an unsafe segment.", nameof(relativePath));
                if (index > 0) escapedPath.Append('/');
                escapedPath.Append(Uri.EscapeDataString(segments[index]));
            }
            return new Uri(host.AbsoluteUri + escapedPath, UriKind.Absolute);
        }

        private static int SelectRandomBundleIndex(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            return new Random().Next(count);
        }
    }
}
