using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;
using StellarFramework.Res;
using StellarFramework.Res.AB;
using StellarFramework.UI;
using UnityEngine;

namespace StellarFramework.Examples
{
    public enum FrameworkValidationStatus
    {
        Passed,
        Warning,
        Failed
    }

    public readonly struct FrameworkValidationEntry
    {
        public FrameworkValidationEntry(string name, FrameworkValidationStatus status, string message)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name.Trim();
            Status = status;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            Timestamp = DateTime.Now;
        }

        public string Name { get; }
        public FrameworkValidationStatus Status { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {FormatStatus(Status)}: {Name} - {Message}";
        }

        public static string FormatStatus(FrameworkValidationStatus status)
        {
            switch (status)
            {
                case FrameworkValidationStatus.Passed:
                    return "通过";
                case FrameworkValidationStatus.Warning:
                    return "警告";
                case FrameworkValidationStatus.Failed:
                    return "失败";
                default:
                    return status.ToString();
            }
        }
    }

    public sealed class FrameworkValidationReport
    {
        private readonly List<FrameworkValidationEntry> _entries = new List<FrameworkValidationEntry>();

        public IReadOnlyList<FrameworkValidationEntry> Entries => _entries;
        public bool HasFailures => Count(FrameworkValidationStatus.Failed) > 0;

        public void Add(string name, FrameworkValidationStatus status, string message)
        {
            _entries.Add(new FrameworkValidationEntry(name, status, message));
        }

        public int Count(FrameworkValidationStatus status)
        {
            int count = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Status == status)
                {
                    count++;
                }
            }

            return count;
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public string ToSummaryString()
        {
            StringBuilder builder = new StringBuilder(512);
            builder.Append("框架验收报告 ");
            builder.Append("通过=").Append(Count(FrameworkValidationStatus.Passed));
            builder.Append(", 警告=").Append(Count(FrameworkValidationStatus.Warning));
            builder.Append(", 失败=").Append(Count(FrameworkValidationStatus.Failed));
            builder.AppendLine();

            if (_entries.Count == 0)
            {
                builder.AppendLine("还没有验收记录。");
                return builder.ToString();
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                builder.AppendLine(_entries[i].ToString());
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// 框架集中验收入口。
    ///
    /// 场景: Scenes/FrameworkValidation_Playable.unity
    /// 操作: 先 Validate settings，再按需点击 ResKit、UIKit、HotUpdateKit 按钮。
    /// 前置: 运行样例构建器；AB/AA/HybridCLR 的真实链路仍需各自构建产物。
    /// 通过标准: 主链路无 Failed；真机前至少跑 Resources、UIKit Snapshot 和 UIKit Stress。
    /// </summary>
    public sealed class FrameworkValidationRunner : MonoBehaviour
    {
        [Header("ResKit Paths")]
        [SerializeField] private string resourcesPath = "ResKitTest/TestCube_Res";
        [SerializeField] private string assetBundlePath =
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle/TestCapsule_AB.prefab";
#if UNITY_ADDRESSABLES
        [SerializeField] private string addressablePath =
            "Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables/TestSphere_AA.prefab";
#endif
        [SerializeField] private string rawTextPath =
            "StellarFramework/Samples/KitSamples/Example_ResKit/TestText.txt";

        [Header("Options")]
        [SerializeField] private int uiStressIterations = 100;
        [SerializeField] private Vector3 resourcesSpawnPosition = new Vector3(-3f, 0f, 0f);
        [SerializeField] private Vector3 assetBundleSpawnPosition = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 addressableSpawnPosition = new Vector3(3f, 0f, 0f);

        private readonly FrameworkValidationReport _report = new FrameworkValidationReport();
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

        private ResourceLoader _resourcesLoader;
        private AssetBundleLoader _assetBundleLoader;
        private RawTextLoader _rawTextLoader;
#if UNITY_ADDRESSABLES
        private IResLoader _addressableLoader;
#endif
        private CancellationTokenSource _destroyCts;
        private Vector2 _scroll;
        private bool _isRunning;
        private string _lastAction = "等待操作。";

        private void Awake()
        {
            _destroyCts = new CancellationTokenSource();
            AllocateLoaders();
            Add(FrameworkValidationStatus.Passed, "FrameworkValidation", "验收入口已初始化。");
        }

        private void OnDestroy()
        {
            _destroyCts?.Cancel();
            _destroyCts?.Dispose();
            ClearSpawnedObjects();
            RecycleLoaders();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20f, 20f, 520f, Screen.height - 40f), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("框架集中验收包", TitleStyle());
            GUILayout.Label(_lastAction);

            DrawSection("安全检查");
            Button("检查 ResKitRuntimeSettings", ValidateSettings);
            Button("检查 HotUpdate 配置", ValidateHotUpdateSettings);
            Button("UIKit Snapshot", CaptureUIKitSnapshot);

            DrawSection("ResKit 运行时");
            ButtonAsync("加载 Resources Prefab", LoadResourcesAsync);
            ButtonAsync("初始化 AssetBundle Manager", InitAssetBundleAsync);
            ButtonAsync("加载 AssetBundle Prefab", LoadAssetBundleAsync);
#if UNITY_ADDRESSABLES
            ButtonAsync("AA Catalog Dry Run", CheckAddressablesCatalogAsync);
            ButtonAsync("加载 Addressables Prefab", LoadAddressablesAsync);
#else
            GUILayout.Label("未启用 UNITY_ADDRESSABLES，Addressables 按钮已隐藏。");
#endif
            ButtonAsync("加载 RawText", LoadRawTextAsync);

            DrawSection("UIKit 运行时");
            ButtonAsync("初始化 UIKit", InitUIKitAsync);
            ButtonAsync("打开 UIKit 面板", OpenUIKitPanelAsync);
            ButtonAsync($"UIKit 压力测试 {uiStressIterations} 次", RunUIKitStressAsync);

            DrawSection("维护操作");
            if (GUILayout.Button("清理生成物体", GUILayout.Height(28f)))
            {
                ClearSpawnedObjects();
                Add(FrameworkValidationStatus.Passed, "Cleanup", "已清理验收过程中生成的物体。");
            }

            if (GUILayout.Button("清空报告", GUILayout.Height(28f)))
            {
                _report.Clear();
                _lastAction = "报告已清空。";
            }

            GUILayout.Space(8f);
            GUILayout.TextArea(_report.ToSummaryString(), GUILayout.MinHeight(240f));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void ValidateSettings()
        {
            ResKitRuntimeSettings settings = ResKitRuntimeSettings.LoadOrCreateDefault();
            ResKitRuntimeSettingsValidationReport report = settings.Validate(includeHybridCLR: false);
            AppendSettingsReport("ResKitRuntimeSettings", report);
        }

private void ValidateHotUpdateSettings()
        {
            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateSettingsValidationReport report = settings.Validate();
            AppendHotUpdateSettingsReport("HotUpdate Settings", report);

            if (report.IsValid)
            {
                Add(FrameworkValidationStatus.Warning, "HotUpdateKit",
                    "配置结构有效。真实 HybridCLR 验证仍需要 dll.bytes 与 AOT metadata 资源。");
            }
        }

        private async UniTask LoadResourcesAsync()
        {
            GameObject prefab = await _resourcesLoader.LoadAsync<GameObject>(resourcesPath, _destroyCts.Token);
            if (prefab == null)
            {
                Add(FrameworkValidationStatus.Failed, "Resources", $"加载失败。Path={resourcesPath}");
                return;
            }

            Spawn(prefab, resourcesSpawnPosition, "Resources_Instance");
            Add(FrameworkValidationStatus.Passed, "Resources", $"已加载并实例化 {resourcesPath}");
        }

        private async UniTask InitAssetBundleAsync()
        {
            bool success = await AssetBundleManager.Instance.InitAsync(_destroyCts.Token);
            if (success)
            {
                Add(FrameworkValidationStatus.Passed, "AssetBundle",
                    $"管理器初始化完成。State={AssetBundleManager.Instance.State}");
                return;
            }

            Add(FrameworkValidationStatus.Failed, "AssetBundle",
                AssetBundleManager.Instance.LastError ?? "AssetBundleManager.InitAsync returned false.");
        }

        private async UniTask LoadAssetBundleAsync()
        {
            GameObject prefab = await _assetBundleLoader.LoadAsync<GameObject>(assetBundlePath, _destroyCts.Token);
#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetBundlePath);
                if (prefab != null)
                {
                    Spawn(prefab, assetBundleSpawnPosition, "AssetBundle_EditorFallback_Instance");
                    Add(FrameworkValidationStatus.Warning, "AssetBundle",
                        "运行时 AB 产物未命中，已在 Editor 下回退加载源 Prefab。真机验证前请先构建 AB 产物。");
                    return;
                }
            }
#endif
            if (prefab == null)
            {
                Add(FrameworkValidationStatus.Failed, "AssetBundle",
                    $"加载失败。Path={assetBundlePath}, LastError={AssetBundleManager.Instance.LastError ?? "None"}");
                return;
            }

            Spawn(prefab, assetBundleSpawnPosition, "AssetBundle_Instance");
            Add(FrameworkValidationStatus.Passed, "AssetBundle", $"已加载并实例化 {assetBundlePath}");
        }

#if UNITY_ADDRESSABLES
        private async UniTask CheckAddressablesCatalogAsync()
        {
            HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
            HotUpdateCheckResult result = await HotUpdateKit.CheckResourceUpdatesAsync(
                settings.BuildAddressablesDefaultUpdateKeys(),
                settings.AddressablesUpdateCatalogsOnCheck,
                _destroyCts.Token);

            if (result.Success)
            {
            Add(result.HasUpdate ? FrameworkValidationStatus.Warning : FrameworkValidationStatus.Passed,
                "Addressables Catalog",
                $"Address={addressablePath}, HasUpdate={result.HasUpdate}, Size={FormatBytes(result.TotalDownloadSize)}, Keys={result.Keys?.Count ?? 0}, Elapsed={result.ElapsedMilliseconds}ms");
                return;
            }

            Add(FrameworkValidationStatus.Failed, "Addressables Catalog",
                $"Status={result.Status}, Error={result.Error ?? "None"}");
        }

        private async UniTask LoadAddressablesAsync()
        {
            GameObject prefab = await _addressableLoader.LoadAsync<GameObject>(addressablePath, _destroyCts.Token);
            if (prefab == null)
            {
                Add(FrameworkValidationStatus.Failed, "Addressables",
                    $"加载失败。Address={addressablePath}。请确认 entry address 为 Assets/...，并且 Addressables 已构建或已启用模拟模式。");
                return;
            }

            Spawn(prefab, addressableSpawnPosition, "Addressables_Instance");
            Add(FrameworkValidationStatus.Passed, "Addressables", $"已加载并实例化 {addressablePath}");
        }
#endif

        private async UniTask LoadRawTextAsync()
        {
            TextAsset textAsset = await _rawTextLoader.LoadAsync<TextAsset>(rawTextPath, _destroyCts.Token);
            if (textAsset == null)
            {
                Add(FrameworkValidationStatus.Failed, "RawText", $"加载失败。Path={rawTextPath}");
                return;
            }

            string preview = textAsset.text;
            if (preview.Length > 120)
            {
                preview = preview.Substring(0, 120) + "...";
            }

            Add(FrameworkValidationStatus.Passed, "RawText", $"已读取 {rawTextPath}: {preview}");
        }

        private async UniTask InitUIKitAsync()
        {
            await UIKit.Instance.InitAsync();
            UIKitRuntimeSnapshot snapshot = UIKit.TakeSnapshot();
            Add(snapshot.IsInitialized ? FrameworkValidationStatus.Passed : FrameworkValidationStatus.Failed,
                "UIKit Init",
                snapshot.ToMultilineString());
        }

        private async UniTask OpenUIKitPanelAsync()
        {
            await UIKit.Instance.InitAsync();
            ExamplePanel panel = await UIKit.OpenAsync<ExamplePanel>(new ExamplePanelData
            {
                TitleMessage = "框架验收",
                RewardCount = _report.Entries.Count + 1
            });

            Add(panel != null ? FrameworkValidationStatus.Passed : FrameworkValidationStatus.Failed,
                "UIKit Open",
                panel != null ? "已通过 UIKit.OpenAsync 打开 ExamplePanel。" : "ExamplePanel 打开失败。");
        }

        private async UniTask RunUIKitStressAsync()
        {
            await UIKit.Instance.InitAsync();
            UIKitRuntimeSnapshot snapshot = await UIKit.StressOpenCloseAsync<ExamplePanel>(
                Mathf.Max(0, uiStressIterations),
                new ExamplePanelData
                {
                    TitleMessage = "框架验收压力测试",
                    RewardCount = uiStressIterations
                },
                yieldEvery: 5,
                cancellationToken: _destroyCts.Token);

            FrameworkValidationStatus status = snapshot.LoadingPanelCount == 0
                ? FrameworkValidationStatus.Passed
                : FrameworkValidationStatus.Warning;
            Add(status, "UIKit Stress", snapshot.ToMultilineString());
        }

        private void CaptureUIKitSnapshot()
        {
            UIKitRuntimeSnapshot snapshot = UIKit.TakeSnapshot();
            FrameworkValidationStatus status = snapshot.IsInitialized
                ? FrameworkValidationStatus.Passed
                : FrameworkValidationStatus.Warning;
            Add(status, "UIKit Snapshot", snapshot.ToMultilineString());
        }

        private void AppendSettingsReport(string name, ResKitRuntimeSettingsValidationReport report)
        {
            if (report == null)
            {
                Add(FrameworkValidationStatus.Failed, name, "校验报告为空。");
                return;
            }

            for (int i = 0; i < report.Errors.Count; i++)
            {
                Add(FrameworkValidationStatus.Failed, name, report.Errors[i]);
            }

            for (int i = 0; i < report.Warnings.Count; i++)
            {
                Add(FrameworkValidationStatus.Warning, name, report.Warnings[i]);
            }

            if (report.Errors.Count == 0 && report.Warnings.Count == 0)
            {
                Add(FrameworkValidationStatus.Passed, name, "未发现错误或警告。");
            }
        }

        private void AppendHotUpdateSettingsReport(string name, HotUpdateSettingsValidationReport report)
        {
            if (report == null)
            {
                Add(FrameworkValidationStatus.Failed, name, "校验报告为空。");
                return;
            }

            for (int i = 0; i < report.Errors.Count; i++)
            {
                Add(FrameworkValidationStatus.Failed, name, report.Errors[i]);
            }

            for (int i = 0; i < report.Warnings.Count; i++)
            {
                Add(FrameworkValidationStatus.Warning, name, report.Warnings[i]);
            }

            if (report.Errors.Count == 0 && report.Warnings.Count == 0)
            {
                Add(FrameworkValidationStatus.Passed, name, "未发现错误或警告。");
            }
        }

        private void AllocateLoaders()
        {
            _resourcesLoader = ResKit.Allocate<ResourceLoader>();
            _resourcesLoader.SetOwnerName("FrameworkValidation.Resources");

            _assetBundleLoader = ResKit.Allocate<AssetBundleLoader>();
            _assetBundleLoader.SetOwnerName("FrameworkValidation.AssetBundle");

            _rawTextLoader = ResKit.Allocate<RawTextLoader>();
            _rawTextLoader.SetOwnerName("FrameworkValidation.RawText");

#if UNITY_ADDRESSABLES
            _addressableLoader = ResKit.Allocate(ResLoaderRequest.Custom(
                "Addressables",
                "FrameworkValidation.Addressables"));
#endif
        }

        private void RecycleLoaders()
        {
            if (_resourcesLoader != null)
            {
                ResKit.Recycle(_resourcesLoader);
            }

            if (_assetBundleLoader != null)
            {
                ResKit.Recycle(_assetBundleLoader);
            }

            if (_rawTextLoader != null)
            {
                ResKit.Recycle(_rawTextLoader);
            }

#if UNITY_ADDRESSABLES
            if (_addressableLoader != null)
            {
                ResKit.Recycle(_addressableLoader);
            }
#endif
            _resourcesLoader = null;
            _assetBundleLoader = null;
            _rawTextLoader = null;
#if UNITY_ADDRESSABLES
            _addressableLoader = null;
#endif
        }

        private void Button(string label, Action action)
        {
            GUI.enabled = !_isRunning;
            if (GUILayout.Button(label, GUILayout.Height(30f)))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Add(FrameworkValidationStatus.Failed, label, ex.Message);
                }
            }

            GUI.enabled = true;
        }

        private void ButtonAsync(string label, Func<UniTask> action)
        {
            GUI.enabled = !_isRunning;
            if (GUILayout.Button(label, GUILayout.Height(30f)))
            {
                RunAsync(label, action).Forget();
            }

            GUI.enabled = true;
        }

        private async UniTaskVoid RunAsync(string label, Func<UniTask> action)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _lastAction = $"正在执行：{label}";
            try
            {
                await action.Invoke();
            }
            catch (OperationCanceledException)
            {
                Add(FrameworkValidationStatus.Warning, label, "操作已取消。");
            }
            catch (Exception ex)
            {
                Add(FrameworkValidationStatus.Failed, label, ex.Message);
            }
            finally
            {
                _lastAction = $"执行完成：{label}";
                _isRunning = false;

                // 关键：每个动作完成后，若有失败项必须显式告警，
                // 防止验证"假通过"（此前失败只显示在报告文本里，容易被遗漏）。
                if (_report.HasFailures)
                {
                    Debug.LogError(
                        $"[FrameworkValidation] 验收失败：共 {_report.Count(FrameworkValidationStatus.Failed)} 项失败。Action={label}，请处理后再验收。");
                }
            }
        }

        private void Add(FrameworkValidationStatus status, string name, string message)
        {
            _report.Add(name, status, message);
            string displayStatus = FrameworkValidationEntry.FormatStatus(status);
            _lastAction = $"{displayStatus}: {name}";
            Debug.Log($"[FrameworkValidation] {displayStatus}: {name} - {message}");
        }

        private void DrawSection(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, SectionStyle());
        }

        private void Spawn(GameObject prefab, Vector3 position, string instanceName)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            instance.name = instanceName;
            _spawnedObjects.Add(instance);
        }

        private void ClearSpawnedObjects()
        {
            for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
            {
                GameObject instance = _spawnedObjects[i];
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            _spawnedObjects.Clear();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024f:F1} KB";
            }

            return $"{bytes / 1048576f:F2} MB";
        }

        private static GUIStyle TitleStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
        }

        private static GUIStyle SectionStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
        }
    }
}
