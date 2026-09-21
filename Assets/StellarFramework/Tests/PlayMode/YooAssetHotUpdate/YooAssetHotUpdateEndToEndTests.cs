using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.HybridCLR;
using StellarFramework.Res;
using StellarFrameworkVerification.Editor;
using UnityEngine;
using UnityEngine.TestTools;
using YooAsset;

namespace StellarFramework.Tests.PlayMode
{
    public sealed class YooAssetHotUpdateEndToEndTests
    {
        private const int TimeoutMs = 120000;
        private string _cacheRoot;
        private RangeHttpServer _server;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return CleanupPackageAsync().ToCoroutine();
            YooAssetResKitInstaller.Uninstall();
            _server?.Dispose();
            _server = null;
            DeleteDirectorySafe(_cacheRoot);
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        [Timeout(TimeoutMs)]
        public IEnumerator InterruptedBundleResumesWithRangeThenHybridClrEntryRuns()
        {
            yield return RunInterruptedBundleResumesWithRangeThenHybridClrEntryRuns().ToCoroutine();
        }

        private async UniTask RunInterruptedBundleResumesWithRangeThenHybridClrEntryRuns()
        {
            string packageDirectory = YooAssetHotUpdateVerificationBuilder.GetPackageOutputDirectory();
            Assert.That(
                Directory.Exists(packageDirectory),
                Is.True,
                "Verification package is missing. Run StellarFramework/Verification/Build YooAsset HotUpdate Package before this PlayMode gate.");

            _cacheRoot = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Temp",
                "StellarHotUpdateVerification",
                "Cache").Replace('\\', '/');
            DeleteDirectorySafe(_cacheRoot);

            string largeBundle = Directory.GetFiles(packageDirectory, "*.bundle", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.Length)
                .First()
                .Name;
            long largeBundleLength = new FileInfo(Path.Combine(packageDirectory, largeBundle)).Length;
            Assert.That(largeBundleLength, Is.GreaterThan(1024L * 1024L));

            _server = new RangeHttpServer(packageDirectory, largeBundle, 256 * 1024);
            _server.Start();

            YooAssetContentUpdateOptions firstOptions = CreateOptions(_server.BaseUrl);
            firstOptions.FailedTryAgain = 0;
            YooAssetContentUpdateResult first;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                first = await YooAssetContentUpdater.UpdateHostPackageAsync(firstOptions);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }

            Assert.That(first.Success, Is.False, "The first download must fail after the server intentionally resets the large bundle request.");
            Assert.That(first.ErrorCode, Is.EqualTo(YooAssetContentUpdateErrorCode.DownloadFailed));
            Assert.That(first.FailureStage, Is.EqualTo(YooAssetContentUpdateStage.Downloading));
            Assert.That(_server.InterruptionTriggered, Is.True, "The verification server did not interrupt the selected large bundle.");
            Assert.That(_server.InterruptedBytes, Is.GreaterThan(0));

            await CleanupPackageAsync();
            _server.AllowCompleteResponses();

            YooAssetContentUpdateResult second = await YooAssetContentUpdater.UpdateHostPackageAsync(CreateOptions(_server.BaseUrl));
            Assert.That(second.Success, Is.True, second.Error);
            Assert.That(second.PackageVersion, Is.EqualTo(YooAssetHotUpdateVerificationBuilder.PackageVersion));

            long[] rangeStarts = _server.RangeStartsFor(largeBundle);
            Assert.That(rangeStarts, Is.Not.Empty, "Second download never issued an HTTP Range request; true breakpoint resume was not exercised.");
            Assert.That(rangeStarts.Any(value => value > 0), Is.True, "Range request did not resume from a positive cached byte offset.");
            Assert.That(rangeStarts.Max(), Is.LessThanOrEqualTo(_server.InterruptedBytes));

            using (ResScope scope = StellarFramework.Res.ResKit.CreateCustomScope(
                       YooAssetResKitInstaller.LoaderKey,
                       "YooAssetHotUpdateE2E"))
            {
                TextAsset manifestAsset = await scope.Loader.LoadAsync<TextAsset>(
                    "Assets/GameHotUpdate/Manifest/HotUpdateManifest.json",
                    CancellationToken.None);
                TextAsset hotUpdateAsset = await scope.Loader.LoadAsync<TextAsset>(
                    "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes",
                    CancellationToken.None);
                Assert.That(manifestAsset, Is.Not.Null);
                Assert.That(hotUpdateAsset, Is.Not.Null);

                HotUpdateManifest manifest = HotUpdateManifest.FromJson(manifestAsset.text);
                Assert.That(manifest, Is.Not.Null);
                Assert.That(
                    ComputeSha256(hotUpdateAsset.bytes),
                    Is.EqualTo(HotUpdateManifest.NormalizeSha256(manifest.hotUpdateAssemblySha256)));
            }

            LogAssert.Expect(LogType.Log, "<color=red>Hello HybridCLR , 热更成功 ;</color>");
            HybridCLRUpdateResult codeUpdate = await HybridCLRKit.RunAsync(HotUpdateSettings.LoadOrCreateDefault());
            Assert.That(codeUpdate.Success, Is.True, codeUpdate.Error);
            Assert.That(codeUpdate.State, Is.EqualTo(HybridCLRUpdateState.EnteredHotUpdate));
            Assert.That(codeUpdate.Manifest, Is.Not.Null);
            Assert.That(codeUpdate.ManifestSource, Does.StartWith("ResKit:YooAsset:"));
            Assert.That(codeUpdate.LoadedAssemblyFullName, Does.Contain("HotUpdate"));
        }

        private YooAssetContentUpdateOptions CreateOptions(string host)
        {
            return new YooAssetContentUpdateOptions
            {
                PackageName = YooAssetHotUpdateVerificationBuilder.PackageName,
                MainHostServer = host,
                FallbackHostServer = host,
                CachePackageRoot = _cacheRoot,
                AppendTimeTicks = false,
                DownloadingMaxNumber = 1,
                FailedTryAgain = 1,
                OperationTimeoutSeconds = 20,
                DownloadWatchDogSeconds = 10,
                ResumeDownloadMinimumSize = 1,
                InstallResKitOnSuccess = true
            };
        }

        private static async UniTask CleanupPackageAsync()
        {
            if (!YooAssets.Initialized) return;

            ResourcePackage package = YooAssets.TryGetPackage(YooAssetHotUpdateVerificationBuilder.PackageName);
            if (package == null) return;

            DestroyOperation destroyOperation = package.DestroyAsync();
            await destroyOperation.Task.AsUniTask();
            Assert.That(destroyOperation.Status, Is.EqualTo(EOperationStatus.Succeed), destroyOperation.Error);

            Assert.That(YooAssets.RemovePackage(package), Is.True);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static void DeleteDirectorySafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private sealed class RangeHttpServer : IDisposable
        {
            private readonly string _root;
            private readonly string _interruptFileName;
            private readonly int _interruptAfterBytes;
            private readonly ConcurrentDictionary<string, ConcurrentBag<long>> _rangeStarts =
                new ConcurrentDictionary<string, ConcurrentBag<long>>(StringComparer.OrdinalIgnoreCase);
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private TcpListener _listener;
            private Task _acceptTask;
            private volatile bool _allowCompleteResponses;
            private int _interruptionTriggered;

            public bool InterruptionTriggered => Volatile.Read(ref _interruptionTriggered) != 0;
            public long InterruptedBytes { get; private set; }
            public string BaseUrl { get; private set; }

            public RangeHttpServer(string root, string interruptFileName, int interruptAfterBytes)
            {
                _root = Path.GetFullPath(root);
                _interruptFileName = interruptFileName;
                _interruptAfterBytes = interruptAfterBytes;
            }

            public void Start()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                BaseUrl = $"http://127.0.0.1:{port}";
                _acceptTask = Task.Run(AcceptLoopAsync);
            }

            public void AllowCompleteResponses()
            {
                _allowCompleteResponses = true;
            }

            public long[] RangeStartsFor(string fileName)
            {
                return _rangeStarts.TryGetValue(fileName, out ConcurrentBag<long> values)
                    ? values.ToArray()
                    : Array.Empty<long>();
            }

            public void Dispose()
            {
                _cts.Cancel();
                try { _listener?.Stop(); } catch { }
                try { _acceptTask?.Wait(1000); } catch { }
                _cts.Dispose();
            }

            private async Task AcceptLoopAsync()
            {
                while (!_cts.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync();
                    }
                    catch when (_cts.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }

                    _ = Task.Run(() => HandleClientAsync(client));
                }
            }

            private async Task HandleClientAsync(TcpClient client)
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    string headerText = await ReadHeaderAsync(stream, _cts.Token);
                    if (string.IsNullOrWhiteSpace(headerText)) return;

                    string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string[] requestParts = lines[0].Split(' ');
                    if (requestParts.Length < 2 || !string.Equals(requestParts[0], "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteErrorAsync(stream, 405, "Method Not Allowed");
                        return;
                    }

                    string requestPath = requestParts[1];
                    int queryIndex = requestPath.IndexOf('?');
                    if (queryIndex >= 0) requestPath = requestPath.Substring(0, queryIndex);
                    string fileName = Uri.UnescapeDataString(requestPath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    string fullPath = Path.GetFullPath(Path.Combine(_root, fileName));
                    if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                    {
                        await WriteErrorAsync(stream, 404, "Not Found");
                        return;
                    }

                    long rangeStart = ParseRangeStart(lines);
                    string leafName = Path.GetFileName(fullPath);
                    if (rangeStart > 0)
                    {
                        _rangeStarts.GetOrAdd(leafName, _ => new ConcurrentBag<long>()).Add(rangeStart);
                    }

                    long length = new FileInfo(fullPath).Length;
                    if (rangeStart >= length)
                    {
                        await WriteHeaderAsync(stream,
                            "HTTP/1.1 416 Range Not Satisfiable\r\n" +
                            $"Content-Range: bytes */{length}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        return;
                    }

                    bool shouldInterrupt = !_allowCompleteResponses &&
                                           rangeStart <= 0 &&
                                           string.Equals(leafName, _interruptFileName, StringComparison.OrdinalIgnoreCase) &&
                                           Interlocked.CompareExchange(ref _interruptionTriggered, 1, 0) == 0;
                    long start = Math.Max(0, rangeStart);
                    long remaining = length - start;
                    string status = start > 0 ? "206 Partial Content" : "200 OK";
                    var headers = new StringBuilder();
                    headers.Append("HTTP/1.1 ").Append(status).Append("\r\n");
                    headers.Append("Content-Length: ").Append(remaining.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                    headers.Append("Accept-Ranges: bytes\r\n");
                    if (start > 0)
                    {
                        headers.Append("Content-Range: bytes ")
                            .Append(start).Append('-').Append(length - 1).Append('/').Append(length).Append("\r\n");
                    }
                    headers.Append("Connection: close\r\n\r\n");
                    await WriteHeaderAsync(stream, headers.ToString());

                    using (FileStream file = File.OpenRead(fullPath))
                    {
                        file.Position = start;
                        byte[] buffer = new byte[32 * 1024];
                        long bytesToWrite = shouldInterrupt ? Math.Min(_interruptAfterBytes, remaining) : remaining;
                        long written = 0;
                        while (written < bytesToWrite)
                        {
                            int request = (int)Math.Min(buffer.Length, bytesToWrite - written);
                            int read = await file.ReadAsync(buffer, 0, request);
                            if (read <= 0) break;
                            await stream.WriteAsync(buffer, 0, read);
                            written += read;
                        }

                        await stream.FlushAsync();
                        if (shouldInterrupt)
                        {
                            InterruptedBytes = written;
                            client.Client.LingerState = new LingerOption(true, 0);
                        }
                    }
                }
            }

            private static async Task<string> ReadHeaderAsync(NetworkStream stream, CancellationToken token)
            {
                byte[] one = new byte[1];
                var bytes = new MemoryStream(1024);
                int state = 0;
                while (bytes.Length < 32 * 1024)
                {
                    int read = await stream.ReadAsync(one, 0, 1, token);
                    if (read <= 0) break;
                    byte value = one[0];
                    bytes.WriteByte(value);
                    state = state switch
                    {
                        0 when value == '\r' => 1,
                        1 when value == '\n' => 2,
                        2 when value == '\r' => 3,
                        3 when value == '\n' => 4,
                        _ when value == '\r' => 1,
                        _ => 0
                    };
                    if (state == 4) break;
                }
                return Encoding.ASCII.GetString(bytes.ToArray());
            }

            private static long ParseRangeStart(string[] lines)
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    const string prefix = "Range: bytes=";
                    if (!lines[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string value = lines[i].Substring(prefix.Length);
                    int dash = value.IndexOf('-');
                    if (dash >= 0) value = value.Substring(0, dash);
                    if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long start))
                    {
                        return start;
                    }
                }
                return 0;
            }

            private static Task WriteHeaderAsync(NetworkStream stream, string text)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(text);
                return stream.WriteAsync(bytes, 0, bytes.Length);
            }

            private static async Task WriteErrorAsync(NetworkStream stream, int code, string reason)
            {
                string response = $"HTTP/1.1 {code} {reason}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                await WriteHeaderAsync(stream, response);
            }
        }
    }
}
