using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor
{
    public enum ConfigWorkspace
    {
        StreamingAssets,
        PersistentDataPath
    }

    /// <summary>
    /// ConfigKit 复合编辑器主窗口 (双工作区版)
    /// 职责: 提供统一的配置管理入口，支持在包内目录与沙盒目录之间无缝切换
    /// </summary>
    public class ConfigKitWindow : EditorWindow
    {
        private ConfigWorkspace _currentWorkspace = ConfigWorkspace.StreamingAssets;

        private Vector2 _leftScrollPos;
        private Vector2 _rightScrollPos;
        private List<string> _normalConfigs = new List<string>();
        private List<string> _netConfigs = new List<string>();

        private string _selectedFilePath = string.Empty;
        private bool _isSelectedNetConfig = false;
        private string _newConfigName = "";

        // 预留给渲染器的委托
        public Action<string> OnDrawNormalConfigPanel;
        public Action<string> OnDrawNetConfigPanel;

        public static void ShowWindow()
        {
            var window = GetWindow<ConfigKitWindow>("ConfigKit Dashboard");
            window.minSize = new Vector2(850, 600);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureDirectories();
            ScanConfigs();
        }

        #region 动态路径解析

        private string GetBaseDir()
        {
            if (_currentWorkspace == ConfigWorkspace.StreamingAssets)
                return "Assets/StreamingAssets/Configs";
            else
                return Path.Combine(Application.persistentDataPath, "Configs").Replace("\\", "/");
        }

        private string GetNormalDir() => GetBaseDir() + "/Normal";
        private string GetNetDir() => GetBaseDir() + "/Net";

        #endregion

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();

            // 左侧导航栏
            DrawLeftPanel();

            // 分割线
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // 右侧内容面板
            DrawRightPanel();

            GUILayout.EndHorizontal();
        }

        #region 目录与文件管理

        private void EnsureDirectories()
        {
            string baseDir = GetBaseDir();
            string normalDir = GetNormalDir();
            string netDir = GetNetDir();

            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
            if (!Directory.Exists(normalDir)) Directory.CreateDirectory(normalDir);
            if (!Directory.Exists(netDir)) Directory.CreateDirectory(netDir);

            if (_currentWorkspace == ConfigWorkspace.StreamingAssets)
            {
                AssetDatabase.Refresh();
            }
        }

        private void ScanConfigs()
        {
            _normalConfigs.Clear();
            _netConfigs.Clear();

            string normalDir = GetNormalDir();
            string netDir = GetNetDir();

            if (Directory.Exists(normalDir))
            {
                string[] files = Directory.GetFiles(normalDir, "*.json", SearchOption.TopDirectoryOnly);
                foreach (var f in files) _normalConfigs.Add(f.Replace("\\", "/"));
            }

            if (Directory.Exists(netDir))
            {
                string[] files = Directory.GetFiles(netDir, "*.json", SearchOption.TopDirectoryOnly);
                foreach (var f in files) _netConfigs.Add(f.Replace("\\", "/"));
            }
        }

        private void CreateNewConfig(bool isNetConfig)
        {
            if (string.IsNullOrWhiteSpace(_newConfigName))
            {
                Debug.LogError("[ConfigKitWindow] 创建失败: 配置文件名不能为空");
                return;
            }

            string fileName = _newConfigName.Trim();
            if (!fileName.EndsWith(".json")) fileName += ".json";

            string targetDir = isNetConfig ? GetNetDir() : GetNormalDir();
            string fullPath = Path.Combine(targetDir, fileName).Replace("\\", "/");

            if (File.Exists(fullPath))
            {
                Debug.LogError($"[ConfigKitWindow] 创建失败: 文件已存在 ({fullPath})");
                return;
            }

            try
            {
                JObject root = new JObject();
                if (isNetConfig)
                {
                    root["ActiveProfile"] = "Dev";
                    root["Environments"] = new JObject { ["Dev"] = new JObject(), ["Release"] = new JObject() };
                    root["Endpoints"] = new JObject();
                }

                File.WriteAllText(fullPath, root.ToString(Newtonsoft.Json.Formatting.Indented),
                    new UTF8Encoding(false));

                _newConfigName = "";
                GUI.FocusControl(null);

                if (_currentWorkspace == ConfigWorkspace.StreamingAssets) AssetDatabase.Refresh();
                ScanConfigs();

                Debug.Log($"[ConfigKitWindow] 创建成功: {fullPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigKitWindow] 创建文件异常: {fullPath}\n{ex.Message}");
            }
        }

        private void DeleteConfig(string path)
        {
            if (!File.Exists(path)) return;

            try
            {
                File.Delete(path);
                string metaPath = path + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);

                if (_selectedFilePath == path)
                {
                    _selectedFilePath = string.Empty;
                }

                if (_currentWorkspace == ConfigWorkspace.StreamingAssets) AssetDatabase.Refresh();
                ScanConfigs();

                Debug.Log($"[ConfigKitWindow] 删除成功: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigKitWindow] 删除文件异常: {path}\n{ex.Message}");
            }
        }

        #endregion

        #region GUI 绘制

        private void DrawLeftPanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(280)); // 稍微加宽以适应长按钮

            // 1. 工作区切换
            GUILayout.BeginVertical("box");
            GUILayout.Label("工作区 (Workspace)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _currentWorkspace = (ConfigWorkspace)GUILayout.Toolbar((int)_currentWorkspace,
                new string[] { "包内默认\n(StreamingAssets)", "本地存档\n(PersistentData)" }, GUILayout.Height(36));
            if (EditorGUI.EndChangeCheck())
            {
                _selectedFilePath = string.Empty;
                EnsureDirectories();
                ScanConfigs();
                GUI.FocusControl(null);
            }

            if (_currentWorkspace == ConfigWorkspace.PersistentDataPath)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
                EditorGUILayout.HelpBox("当前处于沙盒目录。此处的配置会覆盖包内同名配置，常用于测试存档与热更逻辑。", MessageType.Warning);
                GUI.backgroundColor = Color.white;
            }

            GUILayout.EndVertical();
            GUILayout.Space(5);

            // 2. 顶部工具栏：创建新配置
            GUILayout.BeginVertical("box");
            GUILayout.Label("新建配置", EditorStyles.boldLabel);
            _newConfigName = GUILayout.TextField(_newConfigName);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Normal")) CreateNewConfig(false);
            if (GUILayout.Button("+ Net")) CreateNewConfig(true);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(5);

            // 3. 列表区域
            _leftScrollPos = GUILayout.BeginScrollView(_leftScrollPos);
            DrawConfigList("普通配置 (Normal)", _normalConfigs, false);
            GUILayout.Space(10);
            DrawConfigList("网络配置 (Net)", _netConfigs, true);
            GUILayout.EndScrollView();

            // 4. 底部工具栏
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新列表", GUILayout.Height(24))) ScanConfigs();
            if (GUILayout.Button("打开目录", GUILayout.Height(24))) EditorUtility.RevealInFinder(GetBaseDir());
            GUILayout.EndHorizontal();

            if (_currentWorkspace == ConfigWorkspace.PersistentDataPath)
            {
                GUI.backgroundColor = new Color(0.9f, 0.25f, 0.25f);
                if (GUILayout.Button("清空所有本地存档", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog("危险操作", "确定要清空 PersistentDataPath 下的所有配置文件吗？\n游戏将恢复读取包内默认配置。",
                            "确定清空", "取消"))
                    {
                        if (Directory.Exists(GetBaseDir()))
                        {
                            Directory.Delete(GetBaseDir(), true);
                            EnsureDirectories();
                            ScanConfigs();
                            _selectedFilePath = string.Empty;
                            Debug.Log("[ConfigKitWindow] 本地存档已全部清空");
                        }
                    }
                }

                GUI.backgroundColor = Color.white;
            }

            GUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        private void DrawConfigList(string header, List<string> configs, bool isNet)
        {
            GUILayout.Label(header, EditorStyles.boldLabel);
            if (configs.Count == 0)
            {
                GUILayout.Label("  (空)", EditorStyles.miniLabel);
                return;
            }

            foreach (var path in configs)
            {
                string fileName = Path.GetFileName(path);
                bool isSelected = _selectedFilePath == path;

                GUILayout.BeginHorizontal();

                GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
                if (GUILayout.Button(fileName, EditorStyles.toolbarButton))
                {
                    _selectedFilePath = path;
                    _isSelectedNetConfig = isNet;
                    GUI.FocusControl(null);
                }

                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    if (EditorUtility.DisplayDialog("删除确认", $"确定要删除配置 {fileName} 吗？\n此操作不可逆！", "删除", "取消"))
                    {
                        DeleteConfig(path);
                        GUIUtility.ExitGUI();
                    }
                }

                GUI.backgroundColor = Color.white;

                GUILayout.EndHorizontal();
            }
        }

        private void DrawRightPanel()
        {
            GUILayout.BeginVertical();

            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("请在左侧选择或创建一个配置文件", EditorStyles.largeLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            _rightScrollPos = GUILayout.BeginScrollView(_rightScrollPos);

            if (_isSelectedNetConfig)
            {
                if (OnDrawNetConfigPanel != null) OnDrawNetConfigPanel.Invoke(_selectedFilePath);
                else GUILayout.Label($"[NetConfig 渲染逻辑待接入]\n当前文件: {_selectedFilePath}");
            }
            else
            {
                if (OnDrawNormalConfigPanel != null) OnDrawNormalConfigPanel.Invoke(_selectedFilePath);
                else GUILayout.Label($"[NormalConfig 渲染逻辑待接入]\n当前文件: {_selectedFilePath}");
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        #endregion
    }
}