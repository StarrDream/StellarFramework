#if ENABLE_LOG
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace StellarFramework
{
    /// <summary>
    /// 实机日志与设备状态聚合控制台
    /// 提供日志滚动查看、分类过滤、关键字搜索与底层硬件状态的实时监控
    /// 仅在定义了 ENABLE_LOG 宏时编译，Release 包零开销
    /// </summary>
    public class LogViewer : MonoBehaviour
    {
        private struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
        }

        private static LogViewer _instance;
        private bool _isVisible = false;

        private const int MaxLogCount = 500;

        private ConcurrentQueue<LogEntry> _pendingLogs = new ConcurrentQueue<LogEntry>();
        private List<LogEntry> _logs = new List<LogEntry>();

        // 视图控制
        private int _currentTab = 0; // 0: 日志, 1: 设备状态
        private readonly string[] _tabNames = { "日志 (Log)", "设备 (System)" };
        private Vector2 _logScrollPosition;
        private Vector2 _sysScrollPosition;
        
        // 日志过滤与搜索状态
        private bool _autoScroll = true;
        private bool _showInfo = true;
        private bool _showWarning = true;
        private bool _showError = true;
        private string _searchString = "";

        // 动态监控数据
        private string _dynamicMemoryInfo = "";
        private float _nextUpdateTime = 0f;
        private const float UpdateInterval = 1f; 

        // 静态硬件缓存
        private string _staticDeviceInfo = "";
        private bool _deviceInfoInitialized = false;

        // GUI 样式
        private GUIStyle _logStyle;
        private GUIStyle _warnStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _infoTitleStyle;
        private GUIStyle _infoContentStyle;
        private GUIStyle _searchFieldStyle;
        private bool _stylesInitialized = false;
        private Rect _entryButtonRect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            var go = new GameObject("[Stellar_LogViewer]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LogViewer>();
        }

        private void OnEnable()
        {
            Application.logMessageReceivedThreaded += HandleLogThreaded;
        }

        private void OnDisable()
        {
            Application.logMessageReceivedThreaded -= HandleLogThreaded;
        }

        private void HandleLogThreaded(string logString, string stackTrace, LogType type)
        {
            _pendingLogs.Enqueue(new LogEntry
            {
                Message = logString,
                StackTrace = stackTrace,
                Type = type
            });
        }

        private void Update()
        {
            bool hasNewLog = false;
            while (_pendingLogs.TryDequeue(out var entry))
            {
                _logs.Add(entry);
                hasNewLog = true;
                if (_logs.Count > MaxLogCount)
                {
                    _logs.RemoveAt(0);
                }
            }

            if (hasNewLog && _autoScroll && _currentTab == 0)
            {
                _logScrollPosition.y = float.MaxValue;
            }

            if (_isVisible && Time.unscaledTime >= _nextUpdateTime)
            {
                UpdateDynamicInfo();
                _nextUpdateTime = Time.unscaledTime + UpdateInterval;
            }
        }

        private void UpdateDynamicInfo()
        {
            long totalReserved = Profiler.GetTotalReservedMemoryLong();
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long gcMemory = GC.GetTotalMemory(false);
            
            long systemMemoryBytes = (long)SystemInfo.systemMemorySize * 1024 * 1024;
            long approximateFree = systemMemoryBytes - totalReserved;

            _dynamicMemoryInfo = 
                $"[Unity 内存分配]\n" +
                $"系统预留 (Reserved): {ToMB(totalReserved):F1} MB\n" +
                $"实际使用 (Allocated): {ToMB(totalAllocated):F1} MB\n" +
                $"脚本堆内存 (Mono): {ToMB(gcMemory):F1} MB\n\n" +
                $"[系统内存裕量 (近似值)]\n" +
                $"设备总内存 (RAM): {SystemInfo.systemMemorySize} MB\n" +
                $"剩余可用估算 (Free): {ToMB(approximateFree):F1} MB";
        }

        private void InitializeStaticDeviceInfo()
        {
            _staticDeviceInfo = 
                $"[设备标识]\n" +
                $"Device ID: {SystemInfo.deviceUniqueIdentifier}\n" +
                $"Device Model: {SystemInfo.deviceModel}\n" +
                $"OS: {SystemInfo.operatingSystem}\n\n" +
                $"[处理器 (CPU)]\n" +
                $"型号: {SystemInfo.processorType}\n" +
                $"核心数: {SystemInfo.processorCount} Cores\n" +
                $"基准频率: {SystemInfo.processorFrequency} MHz\n\n" +
                $"[图形处理器 (GPU)]\n" +
                $"型号: {SystemInfo.graphicsDeviceName}\n" +
                $"图形 API: {SystemInfo.graphicsDeviceType}\n" +
                $"总显存 (VRAM): {SystemInfo.graphicsMemorySize} MB\n" +
                $"Shader 级别: {SystemInfo.graphicsShaderLevel}";

            _deviceInfoInitialized = true;
        }

        private float ToMB(long bytes)
        {
            return bytes / 1024f / 1024f;
        }

        private void OnGUI()
        {
            if (!_stylesInitialized)
            {
                InitializeStyles();
            }

            if (!_isVisible)
            {
                if (GUI.Button(_entryButtonRect, "Log", GUI.skin.button))
                {
                    _isVisible = true;
                    UpdateDynamicInfo();
                    _nextUpdateTime = Time.unscaledTime + UpdateInterval;
                }
                return;
            }

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));

            // 顶部导航与页签
            GUILayout.BeginHorizontal();
            
            _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(40), GUILayout.Width(Screen.width * 0.4f));
            
            GUILayout.FlexibleSpace();

            if (_currentTab == 0)
            {
                _autoScroll = GUILayout.Toggle(_autoScroll, "自动滚动", GUILayout.Width(80), GUILayout.Height(40));
                if (GUILayout.Button("清空", GUILayout.Width(60), GUILayout.Height(40)))
                {
                    _logs.Clear();
                }
            }

            GUILayout.Space(10);
            if (GUILayout.Button("关闭", GUILayout.Width(60), GUILayout.Height(40)))
            {
                _isVisible = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // 视图分发
            if (_currentTab == 0)
            {
                DrawLogView();
            }
            else
            {
                DrawSystemView();
            }

            GUILayout.EndArea();
        }

        private void DrawLogView()
        {
            // 过滤与搜索控制栏
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索:", GUILayout.Width(40), GUILayout.Height(30));
            _searchString = GUILayout.TextField(_searchString, _searchFieldStyle, GUILayout.Height(30));
            
            GUILayout.Space(10);
            _showInfo = GUILayout.Toggle(_showInfo, "普通", GUILayout.Width(60), GUILayout.Height(30));
            _showWarning = GUILayout.Toggle(_showWarning, "警告", GUILayout.Width(60), GUILayout.Height(30));
            _showError = GUILayout.Toggle(_showError, "错误", GUILayout.Width(60), GUILayout.Height(30));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);

            _logScrollPosition = GUILayout.BeginScrollView(_logScrollPosition);

            bool hasSearch = !string.IsNullOrEmpty(_searchString);

            for (int i = 0; i < _logs.Count; i++)
            {
                var log = _logs[i];
                
                // 状态过滤拦截
                if (log.Type == LogType.Log && !_showInfo) continue;
                if (log.Type == LogType.Warning && !_showWarning) continue;
                if ((log.Type == LogType.Error || log.Type == LogType.Exception || log.Type == LogType.Assert) && !_showError) continue;

                // 关键字搜索拦截 (忽略大小写)
                if (hasSearch && log.Message.IndexOf(_searchString, StringComparison.OrdinalIgnoreCase) < 0) continue;

                GUIStyle currentStyle = _logStyle;
                if (log.Type == LogType.Warning) currentStyle = _warnStyle;
                else if (log.Type == LogType.Error || log.Type == LogType.Exception || log.Type == LogType.Assert)
                    currentStyle = _errorStyle;

                GUILayout.Label(log.Message, currentStyle);
            }

            GUILayout.EndScrollView();
        }

        private void DrawSystemView()
        {
            if (!_deviceInfoInitialized)
            {
                InitializeStaticDeviceInfo();
            }

            _sysScrollPosition = GUILayout.BeginScrollView(_sysScrollPosition);

            GUILayout.Label("实时状态监控", _infoTitleStyle);
            GUILayout.Label(_dynamicMemoryInfo, _infoContentStyle);
            
            GUILayout.Space(20);
            
            GUILayout.Label("硬件规格参数", _infoTitleStyle);
            GUILayout.Label(_staticDeviceInfo, _infoContentStyle);

            GUILayout.EndScrollView();
        }

        private void InitializeStyles()
        {
            int fontSize = Mathf.Max(14, Screen.width / 60);
            float btnWidth = Mathf.Max(60, Screen.width / 15);
            float btnHeight = Mathf.Max(30, Screen.height / 20);

            _entryButtonRect = new Rect(10, 10, btnWidth, btnHeight);

            _logStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.white }, fontSize = fontSize, wordWrap = true };
            _warnStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.yellow }, fontSize = fontSize, wordWrap = true };
            _errorStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.4f, 0.4f) }, fontSize = fontSize, wordWrap = true };

            _infoTitleStyle = new GUIStyle(GUI.skin.label) 
            { 
                normal = { textColor = new Color(0.4f, 0.8f, 1f) }, 
                fontSize = fontSize + 2, 
                fontStyle = FontStyle.Bold 
            };
            
            _infoContentStyle = new GUIStyle(GUI.skin.label) 
            { 
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }, 
                fontSize = fontSize,
                wordWrap = true
            };

            _searchFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft
            };

            _stylesInitialized = true;
        }
    }
}
#endif
