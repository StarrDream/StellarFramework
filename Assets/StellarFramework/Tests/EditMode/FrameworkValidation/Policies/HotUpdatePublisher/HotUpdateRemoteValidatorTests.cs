using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateRemoteValidatorTests
    {
        private string _root;
        private HotUpdatePublishContext _context;
        private HotUpdateEnvironmentProfile _profile;
        private FakeHttpClient _http;
        private IReadOnlyList<HotUpdatePublishFile> _files;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "StellarRemoteValidatorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            string manifestPath = Path.Combine(_root, "PackageManifest.json");
            string bundlePath = Path.Combine(_root, "content.bundle");
            File.WriteAllText(manifestPath, "{\"manifest\":true}");
            File.WriteAllBytes(bundlePath, CreateBundleBytes(300000));
            File.WriteAllText(Path.Combine(_root, "DefaultPackage.version"), "2.0.0");
            HotUpdatePublishFile manifest = HotUpdatePublishFile.FromFile("PackageManifest.json", manifestPath);
            HotUpdatePublishFile bundle = HotUpdatePublishFile.FromFile("content.bundle", bundlePath);
            _files = new[] { manifest, bundle };
            _profile = new HotUpdateEnvironmentProfile
            {
                EnvironmentId = "Development",
                MainHostServer = "https://cdn.example.test/game/windows/default",
                FallbackHostServer = "https://backup.example.test/game/windows/default",
                RemoteRoot = "game/windows/default",
                PublishTarget = "LocalFolder",
                CredentialProfileName = string.Empty
            };
            _context = new HotUpdatePublishContext
            {
                PackageVersion = "2.0.0",
                ExpectedCurrentPackageVersion = "1.9.0",
                YooAssetBuildOutput = new YooAssetBuildOutput
                {
                    PackageVersion = "2.0.0",
                    OutputDirectory = _root,
                    ManifestFiles = new[] { "PackageManifest.bin", "PackageManifest.json", "PackageManifest.hash", "DefaultPackage.version" }
                }
            };
            HotUpdatePublishStepResult prepared = new HotUpdatePrepareUploadStageHandler()
                .ExecuteAsync(_context, CancellationToken.None).GetAwaiter().GetResult();
            if (!prepared.Success) throw new InvalidOperationException(prepared.Error);
            _http = new FakeHttpClient();
            _http.AddFile("cdn.example.test", "DefaultPackage.version", Encoding.UTF8.GetBytes("1.9.0\n"));
            _http.AddFile("backup.example.test", "DefaultPackage.version", Encoding.UTF8.GetBytes("1.9.0\n"));
            _http.AddFile("cdn.example.test", manifest.RelativePath, File.ReadAllBytes(manifestPath));
            _http.AddFile("backup.example.test", manifest.RelativePath, File.ReadAllBytes(manifestPath));
            _http.AddFile("cdn.example.test", bundle.RelativePath, File.ReadAllBytes(bundlePath));
            _http.AddFile("backup.example.test", bundle.RelativePath, File.ReadAllBytes(bundlePath));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void VerifyAsync_ChecksVersionManifestGetLengthAndRangeOnMainAndFallbackHosts()
        {
            new HotUpdateRemoteValidator(_profile, _http, _ => 0)
                .VerifyAsync(_context, _files, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(_http.Requests, Has.Count.EqualTo(8));
            Assert.That(_http.Requests.FindAll(request => request.RangeStart == HotUpdateRemoteValidator.RequiredRangeStart), Has.Count.EqualTo(2));
            Assert.That(_http.Requests.Exists(request => request.Uri.Host == "backup.example.test"), Is.True);
            Assert.That(_http.Requests.Exists(request => request.Uri.AbsolutePath.EndsWith("PackageManifest.json", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void VerifyAsync_FirstReleaseAcceptsReachableAbsentVersionPointer()
        {
            _context.VersionPublishRequest.ExpectedCurrentPackageVersion = string.Empty;
            _http.SetStatus("cdn.example.test", "DefaultPackage.version", 404);
            _http.SetStatus("backup.example.test", "DefaultPackage.version", 404);

            Assert.DoesNotThrow(() => new HotUpdateRemoteValidator(_profile, _http, _ => 0)
                .VerifyAsync(_context, _files, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void VerifyAsync_RejectsUnexpectedCurrentVersionBeforePublish()
        {
            _http.AddFile("cdn.example.test", "DefaultPackage.version", Encoding.UTF8.GetBytes("1.8.0"));
            IOException error = Assert.Throws<IOException>(() => new HotUpdateRemoteValidator(_profile, _http, _ => 0)
                .VerifyAsync(_context, _files, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(error.Message, Does.Contain("changed before publish"));
        }

        [Test]
        public void VerifyAsync_RejectsIncorrectContentLength()
        {
            _http.ContentLengthAdjustment = 1;
            Assert.Throws<IOException>(() => new HotUpdateRemoteValidator(_profile, _http, _ => 0)
                .VerifyAsync(_context, _files, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void VerifyAsync_RejectsServerThatIgnoresRangeRequest()
        {
            _http.IgnoreRange = true;
            IOException error = Assert.Throws<IOException>(() => new HotUpdateRemoteValidator(_profile, _http, _ => 0)
                .VerifyAsync(_context, _files, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(error.Message, Does.Contain("expected 206"));
        }

        [Test]
        public void VerifyAsync_RejectsFallbackMissingCandidateBundle()
        {
            _http.RemoveFile("backup.example.test", "content.bundle");
            Assert.Throws<IOException>(() => new HotUpdateRemoteValidator(_profile, _http, _ => 0)
                .VerifyAsync(_context, _files, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void VerifyAsync_RequiresBundleLargeEnoughForRequestedRange()
        {
            HotUpdatePublishFile smallBundle = CreateSmallBundle();
            Assert.Throws<InvalidOperationException>(() => new HotUpdateRemoteValidator(_profile, _http, _ => 0)
                .VerifyAsync(_context, new[] { _files[0], smallBundle }, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void VerifyRollbackAsyncChecksHistoricalVersionManifestGetAndRangeOnEveryHost()
        {
            var release = new HotUpdateReleaseRecord
            {
                PackageName = "GameContent",
                PackageVersion = "1.8.0",
                Files = new[]
                {
                    new HotUpdateReleaseFileRecord { RelativePath = _files[0].RelativePath, Length = _files[0].Length, Sha256 = _files[0].Sha256 },
                    new HotUpdateReleaseFileRecord { RelativePath = _files[1].RelativePath, Length = _files[1].Length, Sha256 = _files[1].Sha256 }
                },
                ManifestFiles = new[] { "PackageManifest.bin", "PackageManifest.json", "PackageManifest.hash", "DefaultPackage.version" }
            };
            _http.AddFile("cdn.example.test", "DefaultPackage.version", Encoding.UTF8.GetBytes("1.8.0"));
            _http.AddFile("backup.example.test", "DefaultPackage.version", Encoding.UTF8.GetBytes("1.8.0"));

            new HotUpdateRemoteValidator(_profile, _http)
                .VerifyRollbackAsync(release, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(_http.Requests, Has.Count.EqualTo(8));
            Assert.That(_http.Requests.FindAll(request => request.RangeStart == HotUpdateRemoteValidator.RequiredRangeStart), Has.Count.EqualTo(2));
            Assert.That(_http.Requests.Exists(request => request.Uri.AbsolutePath.EndsWith("DefaultPackage.version", StringComparison.Ordinal)), Is.True);
            Assert.That(_http.Requests.Exists(request => request.Uri.Host == "backup.example.test"), Is.True);
        }

        [UnityTest]
        public IEnumerator HttpClient_PerformsRealLoopbackGetAndRangeRequests()
        {
            byte[] payload = CreateBundleBytes(300000);
            using (var server = new LoopbackRangeServer(payload))
            {
                var client = new HotUpdateRemoteHttpClient();
                Task<HotUpdateRemoteHttpResponse> fullRequest = client.GetAsync(
                    new Uri(server.BaseUrl + "content.bundle"), null, 0, CancellationToken.None);
                yield return new UnityEngine.WaitUntil(() => fullRequest.IsCompleted);
                HotUpdateRemoteHttpResponse full = fullRequest.GetAwaiter().GetResult();

                Task<HotUpdateRemoteHttpResponse> rangeRequest = client.GetAsync(
                    new Uri(server.BaseUrl + "content.bundle"), HotUpdateRemoteValidator.RequiredRangeStart, 0, CancellationToken.None);
                yield return new UnityEngine.WaitUntil(() => rangeRequest.IsCompleted);
                HotUpdateRemoteHttpResponse range = rangeRequest.GetAwaiter().GetResult();

                Assert.That(full.StatusCode, Is.EqualTo(200));
                Assert.That(full.ContentLength, Is.EqualTo(payload.Length));
                Assert.That(full.BytesRead, Is.EqualTo(payload.Length));
                Assert.That(range.StatusCode, Is.EqualTo(206));
                Assert.That(range.ContentRangeStart, Is.EqualTo(HotUpdateRemoteValidator.RequiredRangeStart));
                Assert.That(range.ContentRangeTotal, Is.EqualTo(payload.Length));
                Assert.That(range.BytesRead, Is.EqualTo(payload.Length - HotUpdateRemoteValidator.RequiredRangeStart));
            }
        }

        private HotUpdatePublishFile CreateSmallBundle()
        {
            string path = Path.Combine(_root, "small.bundle");
            File.WriteAllBytes(path, new byte[HotUpdateRemoteValidator.RequiredRangeStart]);
            return HotUpdatePublishFile.FromFile("small.bundle", path);
        }

        private static byte[] CreateBundleBytes(int length)
        {
            var bytes = new byte[length];
            for (int index = 0; index < bytes.Length; index++) bytes[index] = (byte)(index % 239);
            return bytes;
        }

        private sealed class FakeHttpClient : IHotUpdateRemoteHttpClient
        {
            private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _statuses = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly List<Request> Requests = new List<Request>();
            public int ContentLengthAdjustment;
            public bool IgnoreRange;

            public Task<HotUpdateRemoteHttpResponse> GetAsync(Uri uri, long? rangeStart, int maxCapturedBodyBytes, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(new Request(uri, rangeStart));
                string key = GetKey(uri);
                if (_statuses.TryGetValue(key, out int status) && status != 200)
                    return Task.FromResult(new HotUpdateRemoteHttpResponse { StatusCode = status, ContentLength = 0, BytesRead = 0 });
                if (!_files.TryGetValue(key, out byte[] bytes))
                    return Task.FromResult(new HotUpdateRemoteHttpResponse { StatusCode = 404, ContentLength = 0, BytesRead = 0 });

                if (rangeStart.HasValue && !IgnoreRange)
                {
                    long length = bytes.Length - rangeStart.Value;
                    return Task.FromResult(new HotUpdateRemoteHttpResponse
                    {
                        StatusCode = 206,
                        ContentLength = length + ContentLengthAdjustment,
                        BytesRead = length,
                        ContentRangeStart = rangeStart.Value,
                        ContentRangeTotal = bytes.Length,
                        AcceptRanges = "bytes"
                    });
                }

                return Task.FromResult(new HotUpdateRemoteHttpResponse
                {
                    StatusCode = 200,
                    ContentLength = bytes.Length + ContentLengthAdjustment,
                    BytesRead = bytes.Length,
                    BodyText = maxCapturedBodyBytes > 0 ? Encoding.UTF8.GetString(bytes) : string.Empty,
                    AcceptRanges = "bytes"
                });
            }

            public void AddFile(string host, string relativePath, byte[] bytes)
            {
                string key = host + "/game/windows/default/" + relativePath;
                _files[key] = bytes;
                _statuses[key] = 200;
            }

            public void SetStatus(string host, string relativePath, int status) => _statuses[host + "/game/windows/default/" + relativePath] = status;
            public void RemoveFile(string host, string relativePath) => _files.Remove(host + "/game/windows/default/" + relativePath);

            private static string GetKey(Uri uri)
            {
                return uri.Host + "/" + Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            }
        }

        private sealed class Request
        {
            public Request(Uri uri, long? rangeStart) { Uri = uri; RangeStart = rangeStart; }
            public Uri Uri { get; }
            public long? RangeStart { get; }
        }

        private sealed class LoopbackRangeServer : IDisposable
        {
            private readonly byte[] _payload;
            private readonly HttpListener _listener;
            private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
            private readonly Task _acceptLoop;

            public LoopbackRangeServer(byte[] payload)
            {
                _payload = payload;
                int port;
                var probe = new TcpListener(System.Net.IPAddress.Loopback, 0);
                try
                {
                    probe.Start();
                    port = ((IPEndPoint)probe.LocalEndpoint).Port;
                }
                finally
                {
                    probe.Stop();
                }
                BaseUrl = $"http://127.0.0.1:{port}/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(BaseUrl);
                _listener.Start();
                _acceptLoop = Task.Run(AcceptLoopAsync);
            }

            public string BaseUrl { get; }

            public void Dispose()
            {
                _cancellation.Cancel();
                _listener.Close();
                try { _acceptLoop.Wait(1000); }
                catch (AggregateException exception) when (exception.InnerException is HttpListenerException || exception.InnerException is ObjectDisposedException) { }
                _cancellation.Dispose();
            }

            private async Task AcceptLoopAsync()
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try { context = await _listener.GetContextAsync(); }
                    catch (HttpListenerException) when (_cancellation.IsCancellationRequested) { return; }
                    catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested) { return; }
                    await RespondAsync(context);
                }
            }

            private async Task RespondAsync(HttpListenerContext context)
            {
                long start = 0;
                string rangeHeader = context.Request.Headers["Range"];
                if (!string.IsNullOrWhiteSpace(rangeHeader))
                {
                    const string prefix = "bytes=";
                    if (!rangeHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        !long.TryParse(rangeHeader.Substring(prefix.Length).TrimEnd('-'), out start) ||
                        start < 0 || start >= _payload.Length)
                    {
                        context.Response.StatusCode = 416;
                        context.Response.Close();
                        return;
                    }
                    context.Response.StatusCode = 206;
                    context.Response.AddHeader("Content-Range", $"bytes {start}-{_payload.Length - 1}/{_payload.Length}");
                }
                else
                {
                    context.Response.StatusCode = 200;
                }

                context.Response.AddHeader("Accept-Ranges", "bytes");
                context.Response.ContentLength64 = _payload.Length - start;
                await context.Response.OutputStream.WriteAsync(_payload, (int)start, _payload.Length - (int)start);
                context.Response.Close();
            }
        }
    }
}
