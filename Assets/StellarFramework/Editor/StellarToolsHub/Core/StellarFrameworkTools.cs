using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor
{
    public class StellarFrameworkTools : EditorWindow
    {
        [MenuItem("StellarFramework/Tools Hub %#t")]
        public static void ShowWindow()
        {
            var window = GetWindow<StellarFrameworkTools>("Stellar Tools");
            window.minSize = new Vector2(1000, 680);
            window.Show();
        }

        // 模块列表
        private readonly List<ToolModule> _allModules = new List<ToolModule>();

        // 分组后的视图数据 Key: GroupName, Value: Modules
        private Dictionary<string, List<ToolModule>> _groupedModules = new Dictionary<string, List<ToolModule>>();

        private ToolModule _currentModule;
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;
        private string _search = "";

        // --- 样式定义 (公开给 Module 使用) ---
        private static readonly Color Accent = new Color(0.35f, 0.68f, 1.00f);

        private bool _stylesReady;
        private GUIStyle _topBarStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subTitleStyle;
        private GUIStyle _sidebarHeaderStyle;
        private GUIStyle _sidebarButtonStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _miniHintStyle;
        private GUIStyle _searchFieldStyle;
        private GUIStyle _searchCancelStyle;

        // 公开样式属性
        public GUIStyle SectionHeaderStyle { get; private set; }
        public GUIStyle PrimaryButtonStyle { get; private set; }
        public GUIStyle DangerButtonStyle { get; private set; }
        public GUIStyle GhostButtonStyle { get; private set; }
        public GUIStyle SidebarButtonStyle => _sidebarButtonStyle; // 允许模块访问侧边栏样式(如果需要)

        private void OnEnable()
        {
            ScanAndRegisterModules();
            _stylesReady = false;
        }

        private void OnDisable()
        {
            if (_currentModule != null) _currentModule.OnDisable();
        }

        private void OnSelectionChange()
        {
            if (_currentModule != null)
            {
                _currentModule.OnSelectionChange();
                Repaint();
            }
        }

        /// <summary>
        /// 核心：使用反射扫描所有带有 [StellarTool] 特性的类
        /// </summary>
        private void ScanAndRegisterModules()
        {
            _allModules.Clear();
            _groupedModules.Clear();

            // 1. 获取所有程序集中的类型 (使用 TypeCache 优化性能，Unity 2019.2+)
            var derivedTypes = TypeCache.GetTypesDerivedFrom<ToolModule>();

            foreach (var type in derivedTypes)
            {
                if (type.IsAbstract) continue;

                // 2. 检查是否有特性标记
                var attr = type.GetCustomAttribute<StellarToolAttribute>();
                if (attr == null) continue; // 没有标记的不加载

                try
                {
                    // 3. 实例化
                    var module = (ToolModule)Activator.CreateInstance(type);
                    module.Initialize(this);

                    // 4. 注入元数据
                    module.Title = attr.Title;
                    module.Group = attr.Group;
                    module.Order = attr.Order;

                    _allModules.Add(module);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[StellarFrameworkTools] 无法实例化模块 {type.Name}: {e.Message}");
                }
            }

            // 5. 排序并分组
            // 先按 Order 排序
            var sortedModules = _allModules.OrderBy(m => m.Order).ToList();

            // 分组
            foreach (var module in sortedModules)
            {
                if (!_groupedModules.ContainsKey(module.Group))
                {
                    _groupedModules[module.Group] = new List<ToolModule>();
                }

                _groupedModules[module.Group].Add(module);
            }

            // 6. 默认选中第一个
            if (_allModules.Count > 0 && _currentModule == null)
            {
                _currentModule = sortedModules[0];
                _currentModule.OnEnable();
            }

            Debug.Log($"[StellarFrameworkTools] 已加载 {_allModules.Count} 个工具模块");
        }

        private void OnGUI()
        {
            EnsureStylesOnGUI();
            DrawTopBar();

            EditorGUILayout.BeginHorizontal();

            // Sidebar
            using (new GUILayout.VerticalScope(_cardStyle, GUILayout.Width(260), GUILayout.ExpandHeight(true)))
            {
                DrawSidebar();
            }

            // Content
            using (new GUILayout.VerticalScope(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
            {
                DrawContent();
            }

            EditorGUILayout.EndHorizontal();
            DrawFooter();
        }

        private void EnsureStylesOnGUI()
        {
            if (_stylesReady) return;

            _topBarStyle = new GUIStyle(EditorStyles.toolbar) { fixedHeight = 34 };
            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            _subTitleStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(1f, 1f, 1f, 0.65f) } };
            _sidebarHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 10, 10), normal = { textColor = Accent } };

            _sidebarButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 32,
                fontSize = 12,
                margin = new RectOffset(2, 2, 1, 1),
                padding = new RectOffset(14, 10, 6, 6) // 增加左边距以体现层级
            };

            _cardStyle = new GUIStyle("HelpBox") { padding = new RectOffset(10, 10, 10, 10), margin = new RectOffset(8, 8, 8, 8) };

            SectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = Accent },
                margin = new RectOffset(0, 0, 10, 4)
            };

            _miniHintStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { fontSize = 10, normal = { textColor = new Color(1f, 1f, 1f, 0.62f) } };

            _searchFieldStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? new GUIStyle("ToolbarSeachTextField");
            _searchCancelStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? new GUIStyle("ToolbarSeachCancelButton");

            PrimaryButtonStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontSize = 12, fontStyle = FontStyle.Bold };
            DangerButtonStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontSize = 12 };
            GhostButtonStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 26, fontSize = 11 };

            _stylesReady = true;
        }

        private void DrawTopBar()
        {
            using (new GUILayout.HorizontalScope(_topBarStyle))
            {
                GUILayout.Space(8);
                GUILayout.Label("StellarFramework Tools", _titleStyle);
                GUILayout.Space(10);
                GUILayout.Label("统一入口 | Editor 工具集成版", _subTitleStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    ScanAndRegisterModules();
                    if (_currentModule != null) _currentModule.OnEnable();
                }
            }
        }

        private void DrawSidebar()
        {
            GUILayout.Label("工具列表", _sidebarHeaderStyle);

            using (new GUILayout.HorizontalScope())
            {
                _search = GUILayout.TextField(_search, _searchFieldStyle);
                if (GUILayout.Button(GUIContent.none, _searchCancelStyle))
                {
                    _search = "";
                    GUI.FocusControl(null);
                    Repaint();
                }
            }

            GUILayout.Space(6);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            // 遍历分组
            foreach (var group in _groupedModules)
            {
                string groupName = group.Key;
                var modules = group.Value;

                // 搜索过滤
                if (!string.IsNullOrEmpty(_search))
                {
                    modules = modules.Where(m => m.Title.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    if (modules.Count == 0) continue;
                }

                // 绘制分组头
                GUILayout.Label(groupName, EditorStyles.miniBoldLabel);

                // 绘制该组下的模块
                foreach (var m in modules)
                {
                    var old = GUI.backgroundColor;
                    if (_currentModule == m) GUI.backgroundColor = Accent;

                    var icon = EditorGUIUtility.IconContent(m.Icon).image;
                    var label = new GUIContent($" {m.Title}", icon);

                    if (GUILayout.Button(label, _sidebarButtonStyle))
                    {
                        if (_currentModule != m)
                        {
                            if (_currentModule != null) _currentModule.OnDisable();
                            _currentModule = m;
                            _currentModule.OnEnable();
                            _contentScroll = Vector2.zero;
                            GUI.FocusControl(null);
                        }
                    }

                    GUI.backgroundColor = old;
                }

                GUILayout.Space(4); // 分组间距
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawContent()
        {
            if (_currentModule == null) return;

            using (new GUILayout.VerticalScope(_cardStyle))
            {
                GUILayout.Label(_currentModule.Title, _titleStyle);
                GUILayout.Label(_currentModule.Description, _miniHintStyle);
            }

            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
            GUILayout.Space(6);

            try
            {
                _currentModule.OnGUI();
            }
            catch (Exception e)
            {
                EditorGUILayout.HelpBox($"模块绘制出错: {e.Message}\n{e.StackTrace}", MessageType.Error);
            }

            GUILayout.Space(18);
            EditorGUILayout.EndScrollView();
        }

        private void DrawFooter()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("v2.4 Integrated", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping Framework", GhostButtonStyle, GUILayout.Width(120)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>("Assets/StellarFramework");
                    if (obj) EditorGUIUtility.PingObject(obj);
                }
            }
        }
    }
}