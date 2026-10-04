using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>HTTP 412 / S3 conditional-write conflict.</summary>
    public sealed class S3CompatiblePreconditionFailedException : IOException
    {
        public S3CompatiblePreconditionFailedException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    /// <summary>
    /// Minimal path-style S3 REST client using AWS Signature Version 4 and BCL HttpWebRequest.
    /// This keeps the framework package free of AWS SDK dependencies.
    /// </summary>
    public sealed class S3CompatibleSignedObjectStoreClient : IS3CompatibleObjectStoreClient
    {
        private const int CopyBufferSize = 81920;
        private readonly Uri _endpoint;
        private readonly string _bucket;
        private readonly string _region;
        private readonly int _requestTimeoutMilliseconds;
        private readonly S3CompatibleCredentials _credentials;

        public S3CompatibleSignedObjectStoreClient(
            S3CompatiblePublishTargetOptions options,
            S3CompatibleCredentials credentials)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (credentials == null) throw new ArgumentNullException(nameof(credentials));
            if (options.ServiceEndpoint == null || !options.ServiceEndpoint.IsAbsoluteUri ||
                (options.ServiceEndpoint.Scheme != Uri.UriSchemeHttps && options.ServiceEndpoint.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(options.ServiceEndpoint.UserInfo) ||
                !string.IsNullOrEmpty(options.ServiceEndpoint.Query) ||
                !string.IsNullOrEmpty(options.ServiceEndpoint.Fragment))
                throw new ArgumentException("S3 service endpoint must be an absolute HTTP(S) URL without user info, query or fragment.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.Bucket) || string.IsNullOrWhiteSpace(options.Region))
                throw new ArgumentException("S3 bucket and region are required.", nameof(options));
            if (options.RequestTimeoutMilliseconds < 1000 || options.RequestTimeoutMilliseconds > 900000)
                throw new ArgumentOutOfRangeException(nameof(options), "RequestTimeoutMilliseconds must be between 1000 and 900000.");
            if (string.IsNullOrWhiteSpace(credentials.AccessKeyId) || string.IsNullOrWhiteSpace(credentials.SecretAccessKey))
                throw new ArgumentException("S3 access key id and secret access key are required.", nameof(credentials));
            _endpoint = options.ServiceEndpoint;
            _bucket = options.Bucket;
            _region = options.Region;
            _requestTimeoutMilliseconds = options.RequestTimeoutMilliseconds;
            _credentials = credentials;
        }

        public async Task<S3CompatibleObjectInfo> GetInfoAsync(string key, CancellationToken cancellationToken)
        {
            HttpWebRequest request = CreateRequest("HEAD", key, EmptyPayloadSha256(), null, null);
            using (HttpWebResponse response = await GetResponseAsync(request, cancellationToken, allowNotFound: true))
            {
                if (response == null) return null;
                return new S3CompatibleObjectInfo
                {
                    Key = key,
                    Length = response.ContentLength,
                    ETag = response.Headers[HttpResponseHeader.ETag] ?? string.Empty,
                    Sha256 = response.Headers["x-amz-meta-stellar-sha256"] ?? string.Empty
                };
            }
        }

        public async Task<string> GetObjectSha256Async(string key, CancellationToken cancellationToken)
        {
            HttpWebRequest request = CreateRequest("GET", key, EmptyPayloadSha256(), null, null);
            using (HttpWebResponse response = await GetResponseAsync(request, cancellationToken, allowNotFound: false))
            using (Stream stream = response.GetResponseStream())
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] buffer = new byte[CopyBufferSize];
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    sha256.TransformBlock(buffer, 0, read, buffer, 0);
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToLowerHex(sha256.Hash);
            }
        }

        public async Task<string> GetTextAsync(string key, CancellationToken cancellationToken)
        {
            HttpWebRequest request = CreateRequest("GET", key, EmptyPayloadSha256(), null, null);
            using (HttpWebResponse response = await GetResponseAsync(request, cancellationToken, allowNotFound: false))
            using (Stream stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, CopyBufferSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await reader.ReadToEndAsync();
            }
        }

        public async Task PutFileAsync(
            string key,
            string sourcePath,
            long length,
            string sha256,
            CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["content-type"] = "application/octet-stream",
                ["x-amz-meta-stellar-sha256"] = sha256,
                ["if-none-match"] = "*"
            };
            HttpWebRequest request = CreateRequest("PUT", key, sha256, headers, length);
            using (var registration = cancellationToken.Register(request.Abort))
            using (FileStream input = File.OpenRead(sourcePath))
            using (Stream output = await request.GetRequestStreamAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await input.CopyToAsync(output, CopyBufferSize, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            using (await GetResponseAsync(request, cancellationToken, allowNotFound: false)) { }
        }

        public async Task PutTextAsync(
            string key,
            string text,
            string expectedETag,
            bool createOnly,
            CancellationToken cancellationToken)
        {
            byte[] body = Encoding.UTF8.GetBytes(text ?? string.Empty);
            string payloadHash = ComputeSha256(body);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["content-type"] = "text/plain; charset=utf-8"
            };
            if (createOnly) headers["if-none-match"] = "*";
            else
            {
                if (string.IsNullOrWhiteSpace(expectedETag))
                    throw new ArgumentException("An ETag is required for conditional pointer replacement.", nameof(expectedETag));
                headers["if-match"] = expectedETag;
            }
            HttpWebRequest request = CreateRequest("PUT", key, payloadHash, headers, body.Length);
            using (var registration = cancellationToken.Register(request.Abort))
            using (Stream output = await request.GetRequestStreamAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await output.WriteAsync(body, 0, body.Length, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            using (await GetResponseAsync(request, cancellationToken, allowNotFound: false)) { }
        }

        private HttpWebRequest CreateRequest(
            string method,
            string key,
            string payloadSha256,
            IReadOnlyDictionary<string, string> operationHeaders,
            long? contentLength)
        {
            Uri uri = BuildObjectUri(key);
            var headers = new SortedDictionary<string, string>(StringComparer.Ordinal);
            headers["host"] = uri.IsDefaultPort ? uri.Host : uri.Authority;
            headers["x-amz-content-sha256"] = payloadSha256;
            headers["x-amz-date"] = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(_credentials.SessionToken))
                headers["x-amz-security-token"] = _credentials.SessionToken;
            if (operationHeaders != null)
            {
                foreach (KeyValuePair<string, string> pair in operationHeaders)
                    headers[pair.Key.ToLowerInvariant()] = NormalizeHeaderValue(pair.Value);
            }

            string authorization = S3V4Signer.CreateAuthorizationHeader(
                method, uri, headers, payloadSha256, _credentials, _region);
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = method;
            request.Timeout = _requestTimeoutMilliseconds;
            request.ReadWriteTimeout = _requestTimeoutMilliseconds;
            request.Headers["x-amz-content-sha256"] = headers["x-amz-content-sha256"];
            request.Headers["x-amz-date"] = headers["x-amz-date"];
            if (headers.TryGetValue("x-amz-security-token", out string token))
                request.Headers["x-amz-security-token"] = token;
            if (headers.TryGetValue("x-amz-meta-stellar-sha256", out string digest))
                request.Headers["x-amz-meta-stellar-sha256"] = digest;
            if (headers.TryGetValue("if-match", out string ifMatch)) request.Headers[HttpRequestHeader.IfMatch] = ifMatch;
            if (headers.TryGetValue("if-none-match", out string ifNoneMatch)) request.Headers[HttpRequestHeader.IfNoneMatch] = ifNoneMatch;
            if (headers.TryGetValue("content-type", out string contentType)) request.ContentType = contentType;
            if (contentLength.HasValue) request.ContentLength = contentLength.Value;
            request.Headers[HttpRequestHeader.Authorization] = authorization;
            return request;
        }

        private Uri BuildObjectUri(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("S3 object key is required.", nameof(key));
            string[] segments = key.Split('/');
            var escaped = new StringBuilder();
            escaped.Append(Uri.EscapeDataString(_bucket));
            for (int index = 0; index < segments.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(segments[index]) || segments[index] == "." || segments[index] == "..")
                    throw new ArgumentException("S3 object key contains an unsafe path segment.", nameof(key));
                escaped.Append('/').Append(Uri.EscapeDataString(segments[index]));
            }

            string endpointPath = _endpoint.AbsolutePath.TrimEnd('/');
            string path = endpointPath + "/" + escaped;
            var builder = new UriBuilder(_endpoint) { Path = path, Query = string.Empty, Fragment = string.Empty };
            return builder.Uri;
        }

        private async Task<HttpWebResponse> GetResponseAsync(
            HttpWebRequest request,
            CancellationToken cancellationToken,
            bool allowNotFound)
        {
            using (var registration = cancellationToken.Register(request.Abort))
            {
                try
                {
                    return (HttpWebResponse)await request.GetResponseAsync();
                }
                catch (WebException exception) when (exception.Response is HttpWebResponse response)
                {
                    using (response)
                    {
                        if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound) return null;
                        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                            throw new S3CompatiblePreconditionFailedException("S3 conditional request failed; object state changed concurrently.", exception);
                        throw new IOException($"S3 {request.Method} request failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).", exception);
                    }
                }
                catch (WebException exception) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("S3 request was cancelled.", exception, cancellationToken);
                }
            }
        }

        private static string NormalizeHeaderValue(string value)
        {
            return (value ?? string.Empty).Trim().Replace('\t', ' ');
        }

        private static string EmptyPayloadSha256()
        {
            return "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create()) return ToLowerHex(sha256.ComputeHash(bytes));
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++) builder.Append(bytes[index].ToString("x2"));
            return builder.ToString();
        }
    }

    /// <summary>AWS Signature Version 4 authorization header generation for S3 requests.</summary>
    public static class S3V4Signer
    {
        public static string CreateAuthorizationHeader(
            string method,
            Uri requestUri,
            IReadOnlyDictionary<string, string> headers,
            string payloadSha256,
            S3CompatibleCredentials credentials,
            string region)
        {
            if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("HTTP method is required.", nameof(method));
            if (requestUri == null) throw new ArgumentNullException(nameof(requestUri));
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (credentials == null) throw new ArgumentNullException(nameof(credentials));

            string timestamp = GetHeader(headers, "x-amz-date");
            if (string.IsNullOrEmpty(timestamp)) timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            if (timestamp.Length < 8) throw new ArgumentException("x-amz-date header is invalid.", nameof(headers));
            string date = timestamp.Substring(0, 8);

            var canonicalHeaders = new StringBuilder();
            var signedNames = new List<string>();
            var sortedHeaders = headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in sortedHeaders)
            {
                string name = pair.Key.ToLowerInvariant();
                if (name != "host" && !name.StartsWith("x-amz-", StringComparison.Ordinal) && name != "if-match" && name != "if-none-match" && name != "content-type")
                    continue;
                canonicalHeaders.Append(name).Append(':').Append(NormalizeHeaderValue(pair.Value)).Append('\n');
                signedNames.Add(name);
            }
            string signedHeaders = string.Join(";", signedNames);
            string canonicalRequest = method.ToUpperInvariant() + "\n" + requestUri.AbsolutePath + "\n" +
                                      CanonicalQuery(requestUri.Query) + "\n" + canonicalHeaders +
                                      signedHeaders + "\n" + payloadSha256;
            string scope = date + "/" + region + "/s3/aws4_request";
            string stringToSign = "AWS4-HMAC-SHA256\n" + timestamp + "\n" + scope + "\n" + HashText(canonicalRequest);
            byte[] signingKey = DeriveSigningKey(credentials.SecretAccessKey, date, region);
            string signature = ToLowerHex(Hmac(signingKey, stringToSign));
            return "AWS4-HMAC-SHA256 Credential=" + credentials.AccessKeyId + "/" + scope +
                   ", SignedHeaders=" + signedHeaders + ", Signature=" + signature;
        }

        private static byte[] DeriveSigningKey(string secret, string date, string region)
        {
            byte[] dateKey = Hmac(Encoding.UTF8.GetBytes("AWS4" + secret), date);
            byte[] regionKey = Hmac(dateKey, region);
            byte[] serviceKey = Hmac(regionKey, "s3");
            return Hmac(serviceKey, "aws4_request");
        }

        private static byte[] Hmac(byte[] key, string value)
        {
            using (var hmac = new HMACSHA256(key)) return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        }

        private static string HashText(string value)
        {
            using (SHA256 sha256 = SHA256.Create()) return ToLowerHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static string CanonicalQuery(string query)
        {
            if (string.IsNullOrEmpty(query) || query == "?") return string.Empty;
            return string.Join("&", query.TrimStart('?').Split('&')
                .Select(part => part.Split(new[] { '=' }, 2))
                .Select(pair => new { Key = Uri.EscapeDataString(Uri.UnescapeDataString(pair[0])), Value = pair.Length > 1 ? Uri.EscapeDataString(Uri.UnescapeDataString(pair[1])) : string.Empty })
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value));
        }

        private static string GetHeader(IReadOnlyDictionary<string, string> headers, string name)
        {
            foreach (KeyValuePair<string, string> pair in headers)
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) return pair.Value;
            return string.Empty;
        }

        private static string NormalizeHeaderValue(string value)
        {
            return string.Join(" ", (value ?? string.Empty).Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++) builder.Append(bytes[index].ToString("x2"));
            return builder.ToString();
        }
    }
}
