using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.Samples.SaveKit
{
    using CoreSaveKit = global::StellarFramework.SaveKit;

    /// <summary>
    /// SaveKit 的可运行样例。重点是 Section/DTO 边界、异步结果、恢复顺序和版本迁移，
    /// 不把 TimeKit、ResKit、Addressables、HybridCLR 或 Newtonsoft.Json 带进示例。
    /// </summary>
    public sealed class SaveKitExample : MonoBehaviour
    {
        private const string MainSlotId = "sf-sample-savekit";
        private const string LegacySlotId = "sf-sample-savekit-migration";

        [Header("Sample Runtime State")]
        [SerializeField] private int _level = 1;
        [SerializeField] private long _money = 100L;
        [SerializeField] private long _worldTick;
        [SerializeField] private int _weatherSeed = 12345;
        [SerializeField] private int _legacyCoins = 500;

        private SaveKitSampleWorldSection _worldSection;
        private SaveKitSamplePlayerSection _playerSection;
        private SaveKitSampleLegacyPlayerSection _legacyPlayerSection;
        private CancellationTokenSource _lifetime;
        private Vector2 _scroll;
        private bool _operationBusy;
        private bool _mainSlotExists;
        private bool _legacySlotExists;
        private long _mainRevision;
        private long _legacyRevision;
        private DateTime _mainUpdatedUtc;
        private DateTime _legacyUpdatedUtc;
        private bool _currentKitReady;
        private bool _usingLegacyKit;
        private string _operationName = string.Empty;
        private string _lastResult = "尚未执行存档操作。";
        private string _setupError = string.Empty;
        private string _migrationText = "未检查迁移链。";
        private string _restoreOrder = "尚未加载。";
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;

        public int Level
        {
            get => _level;
            set => _level = Mathf.Max(1, value);
        }

        public long Money
        {
            get => _money;
            set => _money = Math.Max(0L, value);
        }

        public long WorldTick
        {
            get => _worldTick;
            set => _worldTick = Math.Max(0L, value);
        }

        public int WeatherSeed
        {
            get => _weatherSeed;
            set => _weatherSeed = value;
        }

        public int LegacyCoins
        {
            get => _legacyCoins;
            set => _legacyCoins = Math.Max(0, value);
        }

        public string RestoreOrder => _restoreOrder;

        private CancellationToken Token => _lifetime == null ? default(CancellationToken) : _lifetime.Token;

        private void Awake()
        {
            _lifetime = new CancellationTokenSource();
            InitializeCurrentKit();
        }

        private void Start()
        {
            RefreshSlotsAsync().Forget();
        }

        private void OnDestroy()
        {
            if (_lifetime == null)
            {
                return;
            }

            _lifetime.Cancel();
            _lifetime.Dispose();
            _lifetime = null;
        }

        /// <summary>保留一个简单入口，便于把样例脚本当作模板复制到业务项目。</summary>
        public UniTask<SaveResult> SaveSampleAsync()
        {
            EnsureCurrentKit();
            return CoreSaveKit.SaveAsync(MainSlotId, Token);
        }

        /// <summary>保留一个简单入口，便于自动化冒烟测试和业务侧快速上手。</summary>
        public UniTask<SaveResult> LoadSampleAsync()
        {
            EnsureCurrentKit();
            return CoreSaveKit.LoadAsync(MainSlotId, Token);
        }

        internal void RecordRestore(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
            {
                return;
            }

            _restoreOrder = string.IsNullOrEmpty(_restoreOrder) || _restoreOrder == "尚未加载。"
                ? sectionId
                : _restoreOrder + " -> " + sectionId;
        }

        private void InitializeCurrentKit()
        {
            try
            {
                CoreSaveKit.Initialize(builder => builder.SetApplicationVersion("sample"));
                _worldSection = new SaveKitSampleWorldSection(this);
                _playerSection = new SaveKitSamplePlayerSection(this);
                _legacyPlayerSection = null;
                CoreSaveKit.Register(_worldSection);
                CoreSaveKit.Register(_playerSection);
                bool migrationRegistered = CoreSaveKit.RegisterMigration(
                    SaveKitSampleIds.Player,
                    new SaveKitSamplePlayerV1ToV2Migration());

                _currentKitReady = true;
                _usingLegacyKit = false;
                _setupError = string.Empty;
                _restoreOrder = "尚未加载。";

                IReadOnlyList<ISaveMigration> chain;
                string chainError;
                if (CoreSaveKit.TryBuildMigrationChain(
                        SaveKitSampleIds.Player, 1, 2, out chain, out chainError))
                {
                    _migrationText = string.Format(
                        "V1 -> V2 可用（{0} 步，RegisterMigration={1}）。",
                        chain.Count, migrationRegistered ? "成功" : "失败");
                }
                else
                {
                    _migrationText = "V1 -> V2 不可用：" + chainError;
                }
            }
            catch (Exception exception)
            {
                _currentKitReady = false;
                _setupError = exception.GetType().Name + ": " + exception.Message;
            }
        }

        private void ConfigureLegacyKit()
        {
            CoreSaveKit.Initialize(builder => builder.SetApplicationVersion("sample-v1"));
            _worldSection = new SaveKitSampleWorldSection(this);
            _legacyPlayerSection = new SaveKitSampleLegacyPlayerSection(this);
            _playerSection = null;
            CoreSaveKit.Register(_worldSection);
            CoreSaveKit.Register(_legacyPlayerSection);
            _currentKitReady = true;
            _usingLegacyKit = true;
            _setupError = string.Empty;
            _restoreOrder = "尚未加载。";
            _migrationText = "当前为 V1 生成配置；生成后会自动切回 V2 配置。";
        }

        private void EnsureCurrentKit()
        {
            if (!_currentKitReady || _usingLegacyKit)
            {
                InitializeCurrentKit();
            }
        }

        private async UniTaskVoid RefreshSlotsAsync()
        {
            if (!_currentKitReady)
            {
                return;
            }

            try
            {
                IReadOnlyList<SaveSlotInfo> slots = await CoreSaveKit.GetSlotsAsync(Token);
                _mainSlotExists = false;
                _legacySlotExists = false;
                _mainRevision = 0L;
                _legacyRevision = 0L;
                _mainUpdatedUtc = default(DateTime);
                _legacyUpdatedUtc = default(DateTime);
                if (slots != null)
                {
                    for (int i = 0; i < slots.Count; i++)
                    {
                        SaveSlotInfo slot = slots[i];
                        if (slot == null || slot.Metadata == null)
                        {
                            continue;
                        }

                        string id = slot.Metadata.SlotId.ToString();
                        if (id == MainSlotId)
                        {
                            _mainSlotExists = true;
                            _mainRevision = slot.Metadata.Revision;
                            _mainUpdatedUtc = slot.Metadata.UpdatedUtc;
                        }
                        if (id == LegacySlotId)
                        {
                            _legacySlotExists = true;
                            _legacyRevision = slot.Metadata.Revision;
                            _legacyUpdatedUtc = slot.Metadata.UpdatedUtc;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 场景退出时取消是正常路径。
            }
            catch (Exception exception)
            {
                _lastResult = "读取 Slot 列表失败：" + exception.Message;
            }
        }

        private void StartOperation(string name, Func<UniTask<SaveResult>> operation)
        {
            if (_operationBusy || operation == null)
            {
                return;
            }

            RunOperationAsync(name, operation).Forget();
        }

        private async UniTaskVoid RunOperationAsync(string name, Func<UniTask<SaveResult>> operation)
        {
            _operationBusy = true;
            _operationName = name;
            _lastResult = name + "…";
            try
            {
                SaveResult result = await operation();
                _lastResult = DescribeResult(result);
            }
            catch (OperationCanceledException)
            {
                _lastResult = name + " 已取消。";
            }
            catch (Exception exception)
            {
                _lastResult = name + " 异常：" + exception.GetType().Name + ": " + exception.Message;
            }
            finally
            {
                _operationBusy = false;
                _operationName = string.Empty;
                if (_lifetime != null && !_lifetime.IsCancellationRequested)
                {
                    RefreshSlotsAsync().Forget();
                }
            }
        }

        private async UniTask<SaveResult> SaveCurrentAsync()
        {
            EnsureCurrentKit();
            return await CoreSaveKit.SaveAsync(MainSlotId, Token);
        }

        private async UniTask<SaveResult> LoadCurrentAsync()
        {
            EnsureCurrentKit();
            _restoreOrder = string.Empty;
            return await CoreSaveKit.LoadAsync(MainSlotId, Token);
        }

        private async UniTask<SaveResult> DeleteCurrentAsync()
        {
            EnsureCurrentKit();
            return await CoreSaveKit.DeleteAsync(MainSlotId, Token);
        }

        private async UniTask<SaveResult> CreateLegacySaveAsync()
        {
            ConfigureLegacyKit();
            SaveResult result = await CoreSaveKit.SaveAsync(LegacySlotId, Token);
            if (_lifetime == null || _lifetime.IsCancellationRequested)
            {
                return result;
            }

            InitializeCurrentKit();
            return result;
        }

        private async UniTask<SaveResult> LoadLegacyAsCurrentAsync()
        {
            EnsureCurrentKit();
            _restoreOrder = string.Empty;
            return await CoreSaveKit.LoadAsync(LegacySlotId, Token);
        }

        private async UniTask<SaveResult> DeleteLegacyAsync()
        {
            EnsureCurrentKit();
            return await CoreSaveKit.DeleteAsync(LegacySlotId, Token);
        }

        private static string DescribeResult(SaveResult result)
        {
            if (result == null)
            {
                return "SaveKit 返回空结果。";
            }

            string message = string.Format(
                "Status={0}\nSuccess={1}\nSlot={2}\nRevision={3}\nUsedBackup={4}\nErrorCode={5}",
                result.Status, result.IsSuccess, result.SlotId, result.Revision,
                result.UsedBackup, result.ErrorCode);
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                message += "\nErrorMessage=" + result.ErrorMessage;
            }

            if (result.Diagnostics != null)
            {
                message += string.Format(
                    "\nDiagnostics: Sections={0}, Migrations={1}, Total={2:0.##}ms",
                    result.Diagnostics.SectionCount, result.Diagnostics.MigrationCount,
                    result.Diagnostics.TotalDurationMs);
            }

            return message;
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(16f, 16f, 760f, 900f), GUI.skin.box);
            GUILayout.Label("SaveKit Sample", _titleStyle);
            GUILayout.Label(
                "DTO Section / Save-Load-Delete / RestoreAfter / V1→V2 migration",
                _bodyStyle);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(744f), GUILayout.Height(840f));
            DrawSection("CURRENT KIT", string.Format(
                "Registered Sections: sample.world (V1), sample.player (V2)\n" +
                "ApplicationVersion: sample\n" +
                "Main Slot: {0} ({1}), Revision={2}, UpdatedUtc={3}\n" +
                "Legacy Slot: {4} ({5}), Revision={6}, UpdatedUtc={7}",
                MainSlotId, _mainSlotExists ? "exists" : "not found", _mainRevision,
                FormatUtc(_mainUpdatedUtc), LegacySlotId,
                _legacySlotExists ? "exists" : "not found", _legacyRevision,
                FormatUtc(_legacyUpdatedUtc)));

            if (!string.IsNullOrEmpty(_setupError))
            {
                GUILayout.Label("Setup Error: " + _setupError, _bodyStyle);
            }

            DrawSection("RUNTIME DTO STATE", string.Format(
                "Level: {0}\nMoney: {1}\nWorldTick: {2}\nWeatherSeed: {3}\nLegacy Coins: {4}",
                Level, Money, WorldTick, WeatherSeed, LegacyCoins));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = !_operationBusy;
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Level +1", GUILayout.Height(28f))) Level++;
                if (GUILayout.Button("Money +100", GUILayout.Height(28f))) Money += 100L;
                if (GUILayout.Button("WorldTick +10000", GUILayout.Height(28f))) WorldTick += 10000L;
                if (GUILayout.Button("New Weather Seed", GUILayout.Height(28f))) WeatherSeed = UnityEngine.Random.Range(1, 999999);
            }

            DrawSection("SLOT OPERATIONS", "SaveKit 负责容器、校验、临时文件、Backup 和 Revision；Section 只负责 DTO Capture/Restore。");
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save", GUILayout.Height(32f))) StartOperation("Save current", SaveCurrentAsync);
                if (GUILayout.Button("Load", GUILayout.Height(32f))) StartOperation("Load current", LoadCurrentAsync);
                if (GUILayout.Button("Delete", GUILayout.Height(32f))) StartOperation("Delete current", DeleteCurrentAsync);
            }
            GUILayout.Label("Last Result:\n" + _lastResult, _bodyStyle);
            GUILayout.Label("Restore order: " + _restoreOrder, _bodyStyle);

            DrawSection("V1 → V2 MIGRATION", _migrationText);
            GUILayout.Label(
                "Create Legacy V1 Save 会临时注册 V1 DTO（Coins），保存后自动切回 V2；" +
                "Load Legacy As V2 会执行真实迁移（Coins → long Money，Level=1）。",
                _bodyStyle);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Legacy V1 Save", GUILayout.Height(32f)))
                {
                    StartOperation("Create legacy V1", CreateLegacySaveAsync);
                }
                if (GUILayout.Button("Load Legacy As V2", GUILayout.Height(32f)))
                {
                    StartOperation("Load legacy as V2", LoadLegacyAsCurrentAsync);
                }
                if (GUILayout.Button("Delete Legacy", GUILayout.Height(32f)))
                {
                    StartOperation("Delete legacy", DeleteLegacyAsync);
                }
            }

            DrawSection("PRODUCTION BOUNDARY",
                "本样例只引用 SaveKit.Core 与 UniTask。没有 TimeKit、ResKit、Addressables、HybridCLR、" +
                "ToolsHub 或 Newtonsoft.Json；导出 samples.savekit 时依赖边界保持一致。\n" +
                "Section 的输入输出均为 [Serializable] DTO，UnityEngine.Object 不进入存档数据。\n" +
                (_operationBusy ? "正在执行：" + _operationName : "就绪"));
            GUI.enabled = previousEnabled;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSection(string title, string body)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, _sectionStyle);
            GUILayout.Label(body ?? string.Empty, _bodyStyle);
        }

        private static string FormatUtc(DateTime value)
        {
            return value == default(DateTime) ? "--" : value.ToString("u");
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true
            };
        }
    }
}
