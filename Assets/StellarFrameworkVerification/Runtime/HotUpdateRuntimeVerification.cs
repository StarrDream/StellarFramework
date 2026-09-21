using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using StellarFramework.HybridCLR;
using StellarFramework.Res;
using UnityEngine;
using YooAsset;

namespace StellarFrameworkVerification.Runtime
{
    [Serializable]
    public sealed class HotUpdateVerificationConfig
    {
        public string packageDirectory;
        public string cacheRoot;
        public string packageName = "StellarHotUpdateVerification";
        public string expectedPackageVersion = "verification-v1";
        public int interruptAfterBytes = 262144;
    }

    [Serializable]
    public sealed class HotUpdateVerificationResult
    {
        public bool success;
        public string error;
        public string packageVersion;
        public string largeBundleFileName;
        public long largeBundleSize;
        public long interruptedBytes;
        public long resumeOffset;
        public string loadedAssemblyFullName;
        public string manifestSource;
    }

    /// <summary>
    /// Verification-only runtime gate for YooAsset Range resume + ResKit + HybridCLR.
    /// </summary>
    public static class HotUpdateRuntimeVerification
    {
        public static async UniTask<HotUpdateVerificationResult> RunAsync(
            HotUpdateVerificationConfig config,
            CancellationToken cancellationToken = default)
        {
            var result = new HotUpdateVerificationResult();
            RangeHttpServer server = null;

            try
            {
                ValidateConfig(config);
                DeleteDirectorySafe(config.cacheRoot);
                await CleanupPackageAsync(config.packageName);
                YooAssetResKitInstaller.Uninstall();

                FileInfo largeBundle = new DirectoryInfo(config.packageDirectory)
                    .GetFiles("*.bundle", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(file => file.Length)
                    .FirstOrDefault();
                if (largeBundle == null || largeBundle.Length <= 1024L * 1024L)
                {
                    throw new InvalidOperationException(
                        "Verification package must contain a bundle larger than 1 MiB.");
                }

                result.largeBundleFileName = largeBundle.Name;
                result.largeBundleSize = largeBundle.Length;

                server = new RangeHttpServer(
                    config.packageDirectory,
                    largeBundle.Name,
                    Math.Max(64 * 1024, config.interruptAfterBytes));
                server.Start();

                YooAssetContentUpdateOptions firstOptions = CreateOptions(config, server.BaseUrl);
                firstOptions.FailedTryAgain = 0;
                YooAssetContentUpdateResult first = await YooAssetContentUpdater.UpdateHostPackageAsync(
                    firstOptions,
                    cancellationToken: cancellationToken);

                if (first.Success)
                {
                    throw new InvalidOperationException(
                        "First content update unexpectedly succeeded; the forced HTTP reset was not observed.");
                }
                if (first.ErrorCode != YooAssetContentUpdateErrorCode.DownloadFailed)
                {
                    throw new InvalidOperationException(
                        $"Forced network interruption was classified incorrectly. ErrorCode={first.ErrorCode}, Error={first.Error}");
                }
                if (!server.InterruptionTriggered || server.InterruptedBytes <= 0)
                {
                    throw new InvalidOperationException(
                        "Verification server did not interrupt the selected large bundle.");
                }

                result.interruptedBytes = server.InterruptedBytes;

                await CleanupPackageAsync(config.packageName);
                server.AllowCompleteResponses();

                YooAssetContentUpdateResult second = await YooAssetContentUpdater.UpdateHostPackageAsync(
                    CreateOptions(config, server.BaseUrl),
                    cancellationToken: cancellationToken);
                if (!second.Success)
                {
                    throw new InvalidOperationException("Resumed content update failed: " + second.Error);
                }

                result.packageVersion = second.PackageVersion;
                if (!string.Equals(second.PackageVersion, config.expectedPackageVersion, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Unexpected package version. Expected={config.expectedPackageVersion}, Actual={second.PackageVersion}");
                }

                long resumeOffset = server.RangeStartsFor(largeBundle.Name)
                    .Where(value => value > 0)
                    .DefaultIfEmpty(0L)
                    .Max();
                if (resumeOffset <= 0)
                {
                    throw new InvalidOperationException(
                        "Second package lifetime never issued a positive HTTP Range offset.");
                }
                if (resumeOffset > server.InterruptedBytes)
                {
                    throw new InvalidOperationException(
                        $"Resume offset exceeds interrupted bytes. Offset={resumeOffset}, Interrupted={server.InterruptedBytes}");
                }
                result.resumeOffset = resumeOffset;

                using (ResScope scope = StellarFramework.Res.ResKit.CreateCustomScope(
                           YooAssetResKitInstaller.LoaderKey,
                           "HotUpdateRuntimeVerification"))
                {
                    TextAsset manifestAsset = await scope.Loader.LoadAsync<TextAsset>(
                        "Assets/GameHotUpdate/Manifest/HotUpdateManifest.json",
                        cancellationToken);
                    TextAsset hotUpdateAsset = await scope.Loader.LoadAsync<TextAsset>(
                        "Assets/GameHotUpdate/Code/HotUpdate.dll.bytes",
                        cancellationToken);
                    if (manifestAsset == null || hotUpdateAsset == null)
                    {
                        throw new InvalidOperationException(
                            "ResKit failed to load manifest or hot-update DLL from YooAsset.");
                    }

                    HotUpdateManifest manifest = HotUpdateManifest.FromJson(manifestAsset.text);
                    if (manifest == null)
                    {
                        throw new InvalidOperationException("Downloaded HotUpdateManifest JSON is invalid.");
                    }

                    string actualSha = ComputeSha256(hotUpdateAsset.bytes);
                    string expectedSha = HotUpdateManifest.NormalizeSha256(manifest.hotUpdateAssemblySha256);
                    if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Downloaded hot-update DLL SHA256 mismatch. Expected={expectedSha}, Actual={actualSha}");
                    }
                }

                HybridCLRUpdateResult codeUpdate = await HybridCLRKit.RunAsync(
                    HotUpdateSettings.LoadOrCreateDefault(),
                    cancellationToken: cancellationToken);
                if (!codeUpdate.Success || codeUpdate.State != HybridCLRUpdateState.EnteredHotUpdate)
                {
                    throw new InvalidOperationException("HybridCLR startup failed: " + codeUpdate.Error);
                }

                result.loadedAssemblyFullName = codeUpdate.LoadedAssemblyFullName;
                result.manifestSource = codeUpdate.ManifestSource;
                if (string.IsNullOrWhiteSpace(result.loadedAssemblyFullName) ||
                    !result.loadedAssemblyFullName.Contains("HotUpdate"))
                {
                    throw new InvalidOperationException(
                        "HybridCLR runner completed without the expected HotUpdate assembly.");
                }
                if (string.IsNullOrWhiteSpace(result.manifestSource) ||
                    !result.manifestSource.StartsWith("ResKit:YooAsset:", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "HybridCLR manifest was not sourced from the YooAsset ResKit backend.");
                }

                result.success = true;
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.success = false;
                result.error = ex.ToString();
                return result;
            }
            finally
            {
                server?.Dispose();
                await CleanupPackageAsync(config?.packageName);
                YooAssetResKitInstaller.Uninstall();
                DeleteDirectorySafe(config?.cacheRoot);
            }
        }

        private static YooAssetContentUpdateOptions CreateOptions(
            HotUpdateVerificationConfig config,
            string host)
        {
            return new YooAssetContentUpdateOptions
            {
                PackageName = config.packageName,
                MainHostServer = host,
                FallbackHostServer = host,
                CachePackageRoot = config.cacheRoot,
                AppendTimeTicks = false,
                DownloadingMaxNumber = 1,
                FailedTryAgain = 1,
                OperationTimeoutSeconds = 20,
                DownloadWatchDogSeconds = 10,
                ResumeDownloadMinimumSize = 1,
                InstallResKitOnSuccess = true
            };
        }

        private static async UniTask CleanupPackageAsync(string packageName)
        {
            if (!YooAssets.Initialized || string.IsNullOrWhiteSpace(packageName)) return;
            ResourcePackage package = YooAssets.TryGetPackage(packageName);
            if (package == null) return;

            DestroyOperation destroyOperation = package.DestroyAsync();
            await destroyOperation.Task.AsUniTask();
            if (destroyOperation.Status != EOperationStatus.Succeed)
                throw new InvalidOperationException("Destroy YooAsset package failed: " + destroyOperation.Error);
            YooAssets.RemovePackage(package);
        }

        private static void ValidateConfig(HotUpdateVerificationConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.packageDirectory) || !Directory.Exists(config.packageDirectory))
                throw new DirectoryNotFoundException("Verification package directory is missing: " + config.packageDirectory);
            if (string.IsNullOrWhiteSpace(config.cacheRoot)) throw new ArgumentException("cacheRoot is empty.");
            if (string.IsNullOrWhiteSpace(config.packageName)) throw new ArgumentException("packageName is empty.");
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
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
            try { Directory.Delete(path, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
                BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
                _acceptTask = Task.Run(AcceptLoopAsync);
            }

            public void AllowCompleteResponses() => _allowCompleteResponses = true;

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
                    try { client = await _listener.AcceptTcpClientAsync(); }
                    catch when (_cts.IsCancellationRequested) { return; }
                    catch (ObjectDisposedException) { return; }
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
                    string relativePath = Uri.UnescapeDataString(requestPath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    string fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
                    if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                    {
                        await WriteErrorAsync(stream, 404, "Not Found");
                        return;
                    }

                    long rangeStart = ParseRangeStart(lines);
                    string leafName = Path.GetFileName(fullPath);
                    if (rangeStart > 0)
                        _rangeStarts.GetOrAdd(leafName, _ => new ConcurrentBag<long>()).Add(rangeStart);

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
                    var headers = new StringBuilder();
                    headers.Append("HTTP/1.1 ").Append(start > 0 ? "206 Partial Content" : "200 OK").Append("\r\n");
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
                    if (state == 0 && value == '\r') state = 1;
                    else if (state == 1 && value == '\n') state = 2;
                    else if (state == 2 && value == '\r') state = 3;
                    else if (state == 3 && value == '\n') state = 4;
                    else state = value == '\r' ? 1 : 0;
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
                    if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long start)) return start;
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
                await WriteHeaderAsync(stream,
                    $"HTTP/1.1 {code} {reason}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            }
        }
    }

    internal static class HotUpdateRuntimeVerificationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void TryRun()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string root = Path.Combine(projectRoot, "Temp", "StellarHotUpdateVerification");
            string configPath = Path.Combine(root, "runtime-config.json");
            if (!File.Exists(configPath)) return;

            string resultPath = Path.Combine(root, "runtime-result.json");
            string json = File.ReadAllText(configPath, Encoding.UTF8);
            File.Delete(configPath);
            HotUpdateVerificationConfig config = JsonUtility.FromJson<HotUpdateVerificationConfig>(json);
            RunAndWriteAsync(config, resultPath).Forget();
        }

        private static async UniTaskVoid RunAndWriteAsync(
            HotUpdateVerificationConfig config,
            string resultPath)
        {
            HotUpdateVerificationResult result = await HotUpdateRuntimeVerification.RunAsync(config);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? string.Empty);
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true), Encoding.UTF8);
            if (result.success)
            {
                Debug.Log(
                    $"[HotUpdateVerification] PASS Range={result.resumeOffset}, Interrupted={result.interruptedBytes}, Assembly={result.loadedAssemblyFullName}");
            }
            else
            {
                Debug.LogError("[HotUpdateVerification] FAIL " + result.error);
            }
        }
    }
}
