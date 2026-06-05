using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;
using UnityEngine.Networking;

namespace StellarFramework.HotUpdate
{
    [Serializable]
    public sealed class HotUpdateManifest
    {
        public int version = 1;
        public string buildTarget;
        public string hotUpdateAssemblyKey;
        public string hotUpdateAssemblySha256;
        public string hotUpdateEntryClass;
        public string hotUpdateEntryMethod;
        public List<string> aotMetadataKeys = new List<string>();

        public static HotUpdateManifest FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                HotUpdateManifest manifest = JsonUtility.FromJson<HotUpdateManifest>(
                    json.TrimStart('\uFEFF'));
                if (manifest != null && manifest.aotMetadataKeys == null)
                {
                    manifest.aotMetadataKeys = new List<string>();
                }

                return manifest;
            }
            catch (Exception ex)
            {
                LogKit.LogError($"[HotUpdateManifest] JSON parse failed: {ex.Message}");
                return null;
            }
        }

        public static HotUpdateManifest FromRuntimeSettings(HotUpdateSettings settings)
        {
            if (settings == null)
            {
                return null;
            }

            return new HotUpdateManifest
            {
                version = 1,
                buildTarget = HotUpdateManifestRuntimePaths.RuntimeBuildTargetName,
                hotUpdateAssemblyKey = settings.HotUpdateAssemblyKey,
                hotUpdateAssemblySha256 = settings.HotUpdateAssemblySha256,
                hotUpdateEntryClass = settings.HotUpdateEntryClass,
                hotUpdateEntryMethod = settings.HotUpdateEntryMethod,
                aotMetadataKeys = ResKitRuntimeSettings.ToDistinctStringList(settings.AotMetadataKeys)
            };
        }

        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public List<object> BuildDownloadKeys()
        {
            List<string> keys = ResKitRuntimeSettings.ToDistinctStringList(aotMetadataKeys);
            if (!string.IsNullOrWhiteSpace(hotUpdateAssemblyKey) &&
                !ContainsOrdinal(keys, hotUpdateAssemblyKey.Trim()))
            {
                keys.Add(hotUpdateAssemblyKey.Trim());
            }

            return ResKitRuntimeSettings.ToObjectKeyList(keys);
        }

        public HotUpdateManifestValidationReport Validate()
        {
            return Validate(false);
        }

        public HotUpdateManifestValidationReport Validate(bool strictAssemblyIntegrity)
        {
            HotUpdateManifestValidationReport report = new HotUpdateManifestValidationReport();

            if (string.IsNullOrWhiteSpace(hotUpdateAssemblyKey))
            {
                report.AddError("hotUpdateAssemblyKey is empty.");
            }
            else if (!hotUpdateAssemblyKey.Trim().EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("hotUpdateAssemblyKey should usually point to a .dll.bytes TextAsset address.");
            }

            string normalizedSha256 = string.IsNullOrWhiteSpace(hotUpdateAssemblySha256)
                ? string.Empty
                : hotUpdateAssemblySha256.Trim().Replace("-", string.Empty);

            if (strictAssemblyIntegrity && string.IsNullOrWhiteSpace(normalizedSha256))
            {
                report.AddError(
                    "Production hot update requires hotUpdateAssemblySha256. Re-export dll.bytes and regenerate HotUpdateManifest.json.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedSha256) &&
                normalizedSha256.Length != 64)
            {
                report.AddError("hotUpdateAssemblySha256 must be a 64-character SHA256 hex string when provided.");
            }

            if (string.IsNullOrWhiteSpace(hotUpdateEntryClass))
            {
                report.AddError("hotUpdateEntryClass is empty.");
            }

            if (string.IsNullOrWhiteSpace(hotUpdateEntryMethod))
            {
                report.AddError("hotUpdateEntryMethod is empty.");
            }

            if (ResKitRuntimeSettings.ToDistinctStringList(aotMetadataKeys).Count == 0)
            {
                report.AddError("aotMetadataKeys are empty.");
            }

            return report;
        }

        private static bool ContainsOrdinal(IReadOnlyList<string> list, string value)
        {
            if (list == null || value == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class HotUpdateManifestValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsValid => Errors.Count == 0;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Errors.Add(message);
            }
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Warnings.Add(message);
            }
        }
    }

    public sealed class HotUpdateManifestLoadResult
    {
        public bool Success;
        public HotUpdateManifest Manifest;
        public string Source;
        public string Error;
        public List<string> Errors = new List<string>();

        public static HotUpdateManifestLoadResult Ok(HotUpdateManifest manifest, string source)
        {
            return new HotUpdateManifestLoadResult
            {
                Success = true,
                Manifest = manifest,
                Source = source
            };
        }

        public static HotUpdateManifestLoadResult Fail(string source, string error,
            List<string> errors = null)
        {
            HotUpdateManifestLoadResult result = new HotUpdateManifestLoadResult
            {
                Success = false,
                Source = source,
                Error = error
            };

            if (errors != null)
            {
                result.Errors.AddRange(errors);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                result.Errors.Add($"{source}: {error}");
            }

            return result;
        }
    }

    public interface IHotUpdateManifestSource
    {
        string Description { get; }

        UniTask<HotUpdateManifestLoadResult> LoadAsync(CancellationToken cancellationToken);
    }

    public static class HotUpdateManifestSourceChain
    {
        public static async UniTask<HotUpdateManifestLoadResult> LoadAsync(
            IEnumerable<IHotUpdateManifestSource> sources,
            CancellationToken cancellationToken)
        {
            List<string> errors = new List<string>();
            if (sources == null)
            {
                return HotUpdateManifestLoadResult.Fail("ManifestSourceChain", "Manifest sources are empty.");
            }

            foreach (IHotUpdateManifestSource source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                HotUpdateManifestLoadResult result;
                try
                {
                    result = await source.LoadAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result = HotUpdateManifestLoadResult.Fail(source.Description, ex.Message);
                }

                if (result != null && result.Success && result.Manifest != null)
                {
                    result.Errors = errors;
                    return result;
                }

                if (result != null && result.Errors != null && result.Errors.Count > 0)
                {
                    errors.AddRange(result.Errors);
                }
            }

            return HotUpdateManifestLoadResult.Fail(
                "ManifestSourceChain",
                "No hot update manifest source succeeded.",
                errors);
        }

        public static List<IHotUpdateManifestSource> BuildDefaultSources(HotUpdateSettings settings)
        {
            return BuildDefaultSources(settings, HotUpdateRuntimePolicy.IsStrictProductionRuntime);
        }

        public static List<IHotUpdateManifestSource> BuildDefaultSources(HotUpdateSettings settings, bool strictProduction)
        {
            List<IHotUpdateManifestSource> sources = new List<IHotUpdateManifestSource>();
            if (settings == null)
            {
                settings = HotUpdateSettings.LoadOrCreateDefault();
            }

            string explicitPath = settings != null ? settings.HotUpdateManifestPathOrUrl : null;
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                sources.Add(CreateExplicitSource(
                    explicitPath.Trim(),
                    settings != null ? settings.HotUpdateManifestHttpTimeoutSeconds : 30));

                if (strictProduction)
                {
                    return sources;
                }
            }

            if (settings == null || settings.HotUpdateManifestFallbackToStreamingAssets)
            {
                sources.Add(new StreamingAssetsHotUpdateManifestSource());
            }

            if (!strictProduction && (settings == null || settings.HotUpdateManifestFallbackToResources))
            {
                sources.Add(new ResourcesHotUpdateManifestSource(settings));
            }

            return sources;
        }

        private static IHotUpdateManifestSource CreateExplicitSource(string pathOrUrl, int timeoutSeconds)
        {
            if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpHotUpdateManifestSource(pathOrUrl, timeoutSeconds);
            }

            return new FileUriHotUpdateManifestSource(pathOrUrl);
        }
    }

    public sealed class StreamingAssetsHotUpdateManifestSource : IHotUpdateManifestSource
    {
        public const string FileName = "HotUpdateManifest.json";

        public string Description => "StreamingAssets";

        public async UniTask<HotUpdateManifestLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            string path = HotUpdateManifestRuntimePaths.GetStreamingAssetsManifestPath();
            HotUpdateManifestLoadResult result =
                await HotUpdateManifestFileLoader.LoadFromPathOrUriAsync(path, Description, cancellationToken);
            if (result != null && result.Success)
            {
                return result;
            }

            string legacyPath = HotUpdateManifestRuntimePaths.GetLegacyStreamingAssetsManifestPath();
            if (string.Equals(path, legacyPath, StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            return await HotUpdateManifestFileLoader.LoadFromPathOrUriAsync(
                legacyPath,
                Description,
                cancellationToken);
        }
    }

    public sealed class FileUriHotUpdateManifestSource : IHotUpdateManifestSource
    {
        private readonly string _pathOrUri;

        public FileUriHotUpdateManifestSource(string pathOrUri)
        {
            _pathOrUri = pathOrUri;
        }

        public string Description => "File";

        public UniTask<HotUpdateManifestLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            return HotUpdateManifestFileLoader.LoadFromPathOrUriAsync(_pathOrUri, Description, cancellationToken);
        }
    }

    public sealed class HttpHotUpdateManifestSource : IHotUpdateManifestSource
    {
        private readonly int _timeoutSeconds;
        private readonly string _url;

        public HttpHotUpdateManifestSource(string url, int timeoutSeconds = 30)
        {
            _url = url;
            _timeoutSeconds = Mathf.Max(1, timeoutSeconds);
        }

        public string Description => "Http";

        public async UniTask<HotUpdateManifestLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_url))
            {
                return HotUpdateManifestLoadResult.Fail(Description, "Manifest URL is empty.");
            }

            HttpResponse response = await HttpKit.GetAsync(_url, timeout: _timeoutSeconds)
                .AttachExternalCancellation(cancellationToken);
            if (response == null || !response.isSuccess)
            {
                return HotUpdateManifestLoadResult.Fail(
                    Description,
                    response != null ? response.error : "HttpKit returned null response.");
            }

            return HotUpdateManifestFileLoader.ParseJson(response.responseText, $"{Description}:{_url}");
        }
    }

    public sealed class ResourcesHotUpdateManifestSource : IHotUpdateManifestSource
    {
        private readonly HotUpdateSettings _settings;

        public ResourcesHotUpdateManifestSource(HotUpdateSettings settings = null)
        {
            _settings = settings;
        }

        public string Description => "Resources";

        public UniTask<HotUpdateManifestLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            HotUpdateSettings settings = _settings ?? HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateManifest manifest = HotUpdateManifest.FromRuntimeSettings(settings);
            return UniTask.FromResult(manifest != null
                ? HotUpdateManifestLoadResult.Ok(manifest, Description)
                : HotUpdateManifestLoadResult.Fail(Description, "HotUpdateSettings was not found."));
        }
    }

    public static class HotUpdateManifestRuntimePaths
    {
        public static string RuntimeBuildTargetName
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                        return "StandaloneWindows64";
                    case RuntimePlatform.OSXPlayer:
                    case RuntimePlatform.OSXEditor:
                        return "StandaloneOSX";
                    case RuntimePlatform.LinuxPlayer:
                    case RuntimePlatform.LinuxEditor:
                        return "StandaloneLinux64";
                    case RuntimePlatform.Android:
                        return "Android";
                    case RuntimePlatform.IPhonePlayer:
                        return "iOS";
                    case RuntimePlatform.WebGLPlayer:
                        return "WebGL";
                    default:
                        return Application.platform.ToString();
                }
            }
        }

        public static string GetStreamingAssetsManifestPath()
        {
            return CombineUrlOrPath(
                Application.streamingAssetsPath,
                "aa",
                StreamingAssetsHotUpdateManifestSource.FileName);
        }

        public static string GetLegacyStreamingAssetsManifestPath()
        {
            return CombineUrlOrPath(
                Application.streamingAssetsPath,
                "aa",
                RuntimeBuildTargetName,
                StreamingAssetsHotUpdateManifestSource.FileName);
        }

        private static string CombineUrlOrPath(params string[] parts)
        {
            string result = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(result))
                {
                    result = part.TrimEnd('/', '\\');
                }
                else
                {
                    result = result.TrimEnd('/', '\\') + "/" + part.Trim('/', '\\');
                }
            }

            return result;
        }
    }

    internal static class HotUpdateManifestFileLoader
    {
        public static async UniTask<HotUpdateManifestLoadResult> LoadFromPathOrUriAsync(
            string pathOrUri,
            string description,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(pathOrUri))
            {
                return HotUpdateManifestLoadResult.Fail(description, "Manifest path is empty.");
            }

            string normalized = pathOrUri.Trim();
            if (IsWebLikeUri(normalized))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(normalized))
                {
                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        return HotUpdateManifestLoadResult.Fail(
                            description,
                            $"Load failed. Path={normalized}, Error={request.error}");
                    }

                    return ParseJson(request.downloadHandler.text, $"{description}:{normalized}");
                }
            }

            string filePath = NormalizeLocalPath(normalized);
            if (!File.Exists(filePath))
            {
                return HotUpdateManifestLoadResult.Fail(description, "Manifest file does not exist: " + filePath);
            }

            string json = File.ReadAllText(filePath);
            return ParseJson(json, $"{description}:{filePath}");
        }

        public static HotUpdateManifestLoadResult ParseJson(string json, string source)
        {
            HotUpdateManifest manifest = HotUpdateManifest.FromJson(json);
            if (manifest == null)
            {
                return HotUpdateManifestLoadResult.Fail(source, "Manifest JSON is invalid.");
            }

            HotUpdateManifestValidationReport validation = manifest.Validate();
            if (!validation.IsValid)
            {
                return HotUpdateManifestLoadResult.Fail(
                    source,
                    "Manifest validation failed: " + string.Join(" | ", validation.Errors));
            }

            return HotUpdateManifestLoadResult.Ok(manifest, source);
        }

        private static bool IsWebLikeUri(string pathOrUri)
        {
            return pathOrUri.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                   pathOrUri.StartsWith("jar:", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeLocalPath(string pathOrUri)
        {
            if (!pathOrUri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return pathOrUri;
            }

            Uri uri;
            if (Uri.TryCreate(pathOrUri, UriKind.Absolute, out uri) && uri.IsFile)
            {
                return uri.LocalPath;
            }

            return pathOrUri.Substring("file://".Length);
        }
    }
}
