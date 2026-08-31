using System;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// TimeKit 的最小可运行示例：世界时钟、一次性 Timer、周期 Timer、Catch-Up、
    /// TimeKit Pause 以及 Unity Time.timeScale 解耦。
    ///
    /// 场景：Scenes/TimeKit_Playable.unity
    /// 操作：使用面板按钮推进时间，观察 Tick、Calendar、Timer 和诊断状态。
    /// 通过标准：TimeKit 的 Tick 在 Unity timeScale=0 时仍能推进，Pause 后才停止。
    /// </summary>
    public sealed class Example_TimeKit : MonoBehaviour
    {
        private const double DefaultTimeScale = 1d;
        private const double FastTimeScale = 10d;
        private const double VeryFastTimeScale = 60d;

        private TimerHandle _workHandle;
        private TimerHandle _periodicHandle;
        private TimerCatchUpPolicy _catchUpPolicy = TimerCatchUpPolicy.Latest;
        private long _workFinishTick = -1L;
        private int _workCompletedCount;
        private int _periodicCallbackCount;
        private int _lastElapsedCount;
        private string _workStatus = "Idle";
        private string _clockText = string.Empty;
        private string _workText = string.Empty;
        private string _periodicText = string.Empty;
        private string _diagnosticsText = string.Empty;
        private float _nextUiRefresh;
        private bool _hasStarted;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;

        private void Awake()
        {
            // Sample 退出时也会恢复这个值；这里先防止上一个演示场景留下暂停状态。
            Time.timeScale = 1f;
        }

        private void Start()
        {
            InitializeSample();
            _hasStarted = true;
            RefreshUi();
        }

        private void OnEnable()
        {
            if (!_hasStarted)
            {
                return;
            }

            InitializeSample();
            RefreshUi();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextUiRefresh)
            {
                return;
            }

            _nextUiRefresh = Time.unscaledTime + 0.1f;
            RefreshUi();
        }

        private void OnDisable()
        {
            if (!_hasStarted)
            {
                return;
            }

            CleanupTimers();
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            CleanupTimers();
            Time.timeScale = 1f;
        }

        private void InitializeSample()
        {
            CleanupTimers();
            Time.timeScale = 1f;

            // Sample 场景拥有自己的世界时钟起点。Reset 同时让上一次运行留下的
            // TimerHandle 全部失效，Play/Stop/Play 不会积累旧回调。
            TimeKit.Reset(new GameDateTime(1, 1, 1));
            TimeKit.TimeScale = DefaultTimeScale;
            TimeKit.Resume();

            _catchUpPolicy = TimerCatchUpPolicy.Latest;
            _workFinishTick = -1L;
            _workCompletedCount = 0;
            _periodicCallbackCount = 0;
            _lastElapsedCount = 0;
            _workStatus = "Idle";
            CreatePeriodicTimer();
        }

        private void StartWorkshop()
        {
            if (_workHandle.IsValid)
            {
                return;
            }

            GameDuration duration = GameDuration.Hours(2d);
            _workFinishTick = TimeKit.Tick + duration.Ticks;
            _workStatus = "Working";
            _workHandle = TimeKit.ScheduleAfter(duration, CompleteWorkshop);
        }

        private void CancelWorkshop()
        {
            if (_workHandle.Cancel())
            {
                _workStatus = "Cancelled";
                return;
            }

            // Cancel 对无效句柄是安全的，Sample 只更新可理解的状态，不抛异常。
            if (_workStatus == "Working")
            {
                _workStatus = "Cancelled";
            }
        }

        private void CompleteWorkshop()
        {
            _workHandle = TimerHandle.Invalid;
            _workStatus = "Completed";
            _workCompletedCount++;
        }

        private void CreatePeriodicTimer()
        {
            if (_periodicHandle.IsValid)
            {
                _periodicHandle.Cancel();
            }

            _periodicCallbackCount = 0;
            _lastElapsedCount = 0;
            _periodicHandle = TimeKit.ScheduleEvery(GameDuration.Hours(1d), OnPeriodicTick, _catchUpPolicy);
        }

        private void OnPeriodicTick(TimeTriggerContext context)
        {
            _periodicCallbackCount++;
            _lastElapsedCount = context.ElapsedCount;
        }

        private void SetCatchUpPolicy(TimerCatchUpPolicy policy)
        {
            _catchUpPolicy = policy;
            CreatePeriodicTimer();
        }

        private void AdvanceTime(GameDuration duration)
        {
            TimeKit.AddTime(duration);
        }

        private void RefreshUi()
        {
            GameDateTime now = TimeKit.Now;
            TimeKitDiagnosticsSnapshot diagnostics = TimeKit.GetDiagnostics();
            _clockText = string.Format(
                "Tick: {0}\nDate: Year {1} / Month {2} / Day {3}\nTime: {4:00}:{5:00}:{6:00}.{7:000}\nTime Scale: {8:0.##}x\nStatus: {9}\nUnity timeScale: {10:0.##}",
                TimeKit.Tick, now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second,
                now.Millisecond, TimeKit.TimeScale, TimeKit.IsPaused ? "Paused" : "Running", Time.timeScale);

            string finish = _workFinishTick < 0L
                ? "--"
                : string.Format("Tick {0} / {1}", _workFinishTick, TimeKit.ToDateTime(_workFinishTick));
            long remaining = _workFinishTick < 0L ? 0L : Math.Max(0L, _workFinishTick - TimeKit.Tick);
            _workText = string.Format(
                "Workshop: {0}\nFinish At: {1}\nRemaining: {2} ticks\nTimerHandle valid: {3}\nCompleted Count: {4}",
                _workStatus, finish, remaining, _workHandle.IsValid ? "Yes" : "No", _workCompletedCount);
            _periodicText = string.Format(
                "Policy: {0}\nInterval: 1 game hour\nCallback Count: {1}\nLast ElapsedCount: {2}\nTimerHandle valid: {3}",
                _catchUpPolicy, _periodicCallbackCount, _lastElapsedCount, _periodicHandle.IsValid ? "Yes" : "No");
            _diagnosticsText = string.Format(
                "Active Timers: {0}\nHeap Count: {1}\nBacklog: {2}\nCallbacks Last Update: {3}",
                diagnostics.ActiveTimerCount, diagnostics.HeapCount, diagnostics.DueBacklogCount,
                diagnostics.CallbacksExecutedLastUpdate);
        }

        private void CleanupTimers()
        {
            if (_workHandle.IsValid)
            {
                _workHandle.Cancel();
            }

            if (_periodicHandle.IsValid)
            {
                _periodicHandle.Cancel();
            }

            _workHandle = TimerHandle.Invalid;
            _periodicHandle = TimerHandle.Invalid;
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(16f, 16f, 640f, 760f), GUI.skin.box);
            GUILayout.Label("TimeKit Sample", _titleStyle);
            GUILayout.Label("游戏世界时间 / Timer / Catch-Up / Unity timeScale 解耦", _bodyStyle);

            DrawSection("WORLD CLOCK", _clockText);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pause TimeKit", GUILayout.Height(26f))) TimeKit.Pause();
                if (GUILayout.Button("Resume TimeKit", GUILayout.Height(26f))) TimeKit.Resume();
                if (GUILayout.Button("1x", GUILayout.Height(26f))) TimeKit.TimeScale = DefaultTimeScale;
                if (GUILayout.Button("10x", GUILayout.Height(26f))) TimeKit.TimeScale = FastTimeScale;
                if (GUILayout.Button("60x", GUILayout.Height(26f))) TimeKit.TimeScale = VeryFastTimeScale;
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+1 Hour", GUILayout.Height(26f))) AdvanceTime(GameDuration.Hours(1d));
                if (GUILayout.Button("+1 Day", GUILayout.Height(26f))) AdvanceTime(GameDuration.Days(1d));
            }

            DrawSection("ONE-SHOT WORKSHOP TIMER", _workText);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start 2-hour Work", GUILayout.Height(26f))) StartWorkshop();
                if (GUILayout.Button("Cancel", GUILayout.Height(26f))) CancelWorkshop();
            }

            DrawSection("PERIODIC / CATCH-UP", _periodicText);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All")) SetCatchUpPolicy(TimerCatchUpPolicy.All);
                if (GUILayout.Button("Once")) SetCatchUpPolicy(TimerCatchUpPolicy.Once);
                if (GUILayout.Button("Latest")) SetCatchUpPolicy(TimerCatchUpPolicy.Latest);
                if (GUILayout.Button("Skip")) SetCatchUpPolicy(TimerCatchUpPolicy.Skip);
                if (GUILayout.Button("+5 Hours")) AdvanceTime(GameDuration.Hours(5d));
            }
            GUILayout.Label("All=逐次补发，Once=只回调一次并从当前时间重新计时，Latest=合并遗漏次数，Skip=跳过遗漏。", _bodyStyle);

            DrawSection("UNITY TIME SCALE VS TIMEKIT", "Unity timeScale=0 只会暂停 Unity scaled 时间；TimeKit 使用 unscaled 时间，仍会继续推进。点击 TimeKit Pause 才会停止世界时间。");
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Unity timeScale = 0", GUILayout.Height(26f))) Time.timeScale = 0f;
                if (GUILayout.Button("Unity timeScale = 1", GUILayout.Height(26f))) Time.timeScale = 1f;
            }

            DrawSection("DIAGNOSTICS", _diagnosticsText);
            GUILayout.EndArea();
        }

        private void DrawSection(string title, string body)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, _sectionStyle);
            GUILayout.Label(body ?? string.Empty, _bodyStyle);
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
