using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor
{
    public class StellarFrameworkTools : EditorWindow
    {
        private static readonly string[] PreferredGroupOrder =
        {
            "Start Here",
            "资源管理",
            "框架核心",
            "热更新",
            "样例支持",
            "生产力",
            "常用工具"
        };

        private static readonly Color Accent = new Color(0.35f, 0.68f, 1.00f);
        private static readonly Color AccentDark = new Color(0.16f, 0.42f, 0.80f);
        private static readonly Color WindowBackground = new Color(0.10f, 0.11f, 0.13f);
        private static readonly Color PanelBackground = new Color(0.14f, 0.16f, 0.19f);
        private static readonly Color CardBackground = new Color(0.18f, 0.20f, 0.24f);
        private static readonly Color CardBackgroundSoft = new Color(0.16f, 0.18f, 0.21f);
        private static readonly Color BorderColor = new Color(0.27f, 0.31f, 0.37f);
        private static readonly Color TextPrimary = new Color(0.96f, 0.97f, 0.99f);
        private static readonly Color TextSecondary = new Color(0.73f, 0.77f, 0.83f);

        [MenuItem("StellarFramework/Tools Hub %#t")]
        public static void ShowWindow()
        {
            StellarFrameworkTools window = GetWindow<StellarFrameworkTools>("Tools Hub");
            window.minSize = new Vector2(1000, 680);
            window.Show();
        }

        private readonly List<ToolModule> _allModules = new List<ToolModule>();
        private Dictionary<string, List<ToolModule>> _groupedModules = new Dictionary<string, List<ToolModule>>();
        private readonly Dictionary<ToolModule, Button> _moduleButtons = new Dictionary<ToolModule, Button>();

        private ToolModule _currentModule;
        private string _search = string.Empty;

        private ToolbarSearchField _searchField;
        private ScrollView _sidebarScrollView;
        private Label _moduleTitleLabel;
        private Label _moduleDescriptionLabel;
        private VisualElement _contentHost;

        private bool _legacyStylesReady;
        private GUIStyle _sidebarButtonStyle;

        public GUIStyle SectionHeaderStyle { get; private set; }
        public GUIStyle PrimaryButtonStyle { get; private set; }
        public GUIStyle DangerButtonStyle { get; private set; }
        public GUIStyle GhostButtonStyle { get; private set; }
        public GUIStyle SidebarButtonStyle => _sidebarButtonStyle;

        private void OnEnable()
        {
            ScanAndRegisterModules(_currentModule?.Title);
            if (rootVisualElement != null && rootVisualElement.childCount > 0)
            {
                RebuildUi();
            }
        }

        private void OnDisable()
        {
            _currentModule?.OnDisable();
        }

        private void OnSelectionChange()
        {
            if (_currentModule == null)
            {
                return;
            }

            _currentModule.OnSelectionChange();
            _contentHost?.MarkDirtyRepaint();
        }

        public void CreateGUI()
        {
            RebuildUi();
        }

        private void EnsureLegacyStyles()
        {
            if (_legacyStylesReady)
            {
                return;
            }

            SectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = Accent },
                margin = new RectOffset(0, 0, 10, 4)
            };

            _sidebarButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 32,
                fontSize = 12,
                margin = new RectOffset(2, 2, 1, 1),
                padding = new RectOffset(14, 10, 6, 6)
            };

            PrimaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 30,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            DangerButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 30,
                fontSize = 12
            };

            GhostButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 26,
                fontSize = 11
            };

            _legacyStylesReady = true;
        }

        private void RebuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.backgroundColor = WindowBackground;
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            rootVisualElement.Add(BuildTopBar());

            TwoPaneSplitView splitView = new TwoPaneSplitView(0, 288, TwoPaneSplitViewOrientation.Horizontal)
            {
                style =
                {
                    flexGrow = 1f,
                    marginTop = 10
                }
            };

            splitView.Add(BuildSidebarPane());
            splitView.Add(BuildContentPane());
            rootVisualElement.Add(splitView);
            rootVisualElement.Add(BuildFooter());

            RefreshSidebar();
            RefreshContent();
        }

        private VisualElement BuildTopBar()
        {
            VisualElement topBar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 16,
                    paddingRight = 16,
                    paddingTop = 12,
                    paddingBottom = 12,
                    minHeight = 60,
                    backgroundColor = PanelBackground,
                    borderTopLeftRadius = 10,
                    borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    borderTopColor = BorderColor,
                    borderRightColor = BorderColor,
                    borderBottomColor = BorderColor,
                    borderLeftColor = BorderColor,
                    borderTopWidth = 1,
                    borderRightWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1
                }
            };

            VisualElement titleGroup = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column
                }
            };

            Label title = new Label("StellarFramework Tools Hub")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 18,
                    color = TextPrimary
                }
            };
            titleGroup.Add(title);

            Label subtitle = new Label("统一入口 | Editor 工具集成版")
            {
                style =
                {
                    fontSize = 11,
                    color = TextSecondary,
                    marginTop = 2
                }
            };
            titleGroup.Add(subtitle);
            topBar.Add(titleGroup);

            VisualElement spacer = new VisualElement
            {
                style =
                {
                    flexGrow = 1f
                }
            };
            topBar.Add(spacer);

            Button refreshButton = new Button(() =>
            {
                string selectedTitle = _currentModule?.Title;
                _currentModule?.OnDisable();
                ScanAndRegisterModules(selectedTitle);
                RebuildUi();
            })
            {
                text = "刷新"
            };
            refreshButton.style.width = 72;
            refreshButton.style.height = 28;
            refreshButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            topBar.Add(refreshButton);

            return topBar;
        }

        private VisualElement BuildSidebarPane()
        {
            VisualElement sidebarPane = CreateCardContainer();
            sidebarPane.style.flexGrow = 1f;
            sidebarPane.style.marginRight = 6;

            Label header = new Label("工具列表")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    color = TextPrimary,
                    marginBottom = 10
                }
            };
            sidebarPane.Add(header);

            _searchField = new ToolbarSearchField();
            _searchField.value = _search;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                RefreshSidebar();
            });
            sidebarPane.Add(_searchField);

            _sidebarScrollView = new ScrollView
            {
                style =
                {
                    flexGrow = 1f,
                    marginTop = 10
                }
            };
            sidebarPane.Add(_sidebarScrollView);

            return sidebarPane;
        }

        private VisualElement BuildContentPane()
        {
            VisualElement contentPane = new VisualElement
            {
                style =
                {
                    flexGrow = 1f,
                    marginLeft = 6
                }
            };

            VisualElement headerCard = CreateCardContainer();
            headerCard.style.flexGrow = 0f;
            headerCard.style.marginBottom = 10;

            _moduleTitleLabel = new Label
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 18,
                    color = TextPrimary
                }
            };
            headerCard.Add(_moduleTitleLabel);

            _moduleDescriptionLabel = new Label
            {
                style =
                {
                    marginTop = 4,
                    color = TextSecondary,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            headerCard.Add(_moduleDescriptionLabel);
            contentPane.Add(headerCard);

            _contentHost = CreateCardContainer();
            _contentHost.style.flexGrow = 1f;
            _contentHost.style.paddingLeft = 0;
            _contentHost.style.paddingRight = 0;
            _contentHost.style.paddingTop = 0;
            _contentHost.style.paddingBottom = 0;
            contentPane.Add(_contentHost);

            return contentPane;
        }

        private VisualElement BuildFooter()
        {
            VisualElement footer = CreateCardContainer();
            footer.style.flexGrow = 0f;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.marginTop = 10;
            footer.style.paddingTop = 8;
            footer.style.paddingBottom = 8;

            footer.Add(new Label("v2.4 集成版")
            {
                style =
                {
                    color = TextSecondary
                }
            });

            footer.Add(new VisualElement
            {
                style =
                {
                    flexGrow = 1f
                }
            });

            Button locateButton = new Button(() =>
            {
                Object obj = AssetDatabase.LoadAssetAtPath<Object>("Assets/StellarFramework");
                if (obj)
                {
                    EditorGUIUtility.PingObject(obj);
                }
            })
            {
                text = "定位框架目录"
            };
            locateButton.style.width = 128;
            locateButton.style.height = 26;
            footer.Add(locateButton);

            return footer;
        }

        private void RefreshSidebar()
        {
            if (_sidebarScrollView == null)
            {
                return;
            }

            _sidebarScrollView.Clear();
            _moduleButtons.Clear();

            foreach (KeyValuePair<string, List<ToolModule>> group in _groupedModules)
            {
                List<ToolModule> modules = group.Value;
                if (!string.IsNullOrEmpty(_search))
                {
                    modules = modules
                        .Where(m => m.Title.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                    if (modules.Count == 0)
                    {
                        continue;
                    }
                }

                Label groupLabel = new Label(group.Key)
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = TextSecondary,
                        marginTop = 10,
                        marginBottom = 6,
                        fontSize = 11
                    }
                };
                _sidebarScrollView.Add(groupLabel);

                foreach (ToolModule module in modules)
                {
                    Button button = new Button(() => SelectModule(module))
                    {
                        text = module.Title
                    };
                    button.style.unityTextAlign = TextAnchor.MiddleLeft;
                    button.style.height = 36;
                    button.style.marginBottom = 4;
                    button.style.backgroundColor = module == _currentModule ? AccentDark : CardBackgroundSoft;
                    button.style.color = TextPrimary;
                    button.style.borderTopLeftRadius = 8;
                    button.style.borderTopRightRadius = 8;
                    button.style.borderBottomLeftRadius = 8;
                    button.style.borderBottomRightRadius = 8;
                    button.style.borderLeftWidth = module == _currentModule ? 3 : 1;
                    button.style.borderRightWidth = 1;
                    button.style.borderTopWidth = 1;
                    button.style.borderBottomWidth = 1;
                    button.style.borderLeftColor = module == _currentModule ? Accent : BorderColor;
                    button.style.borderRightColor = BorderColor;
                    button.style.borderTopColor = BorderColor;
                    button.style.borderBottomColor = BorderColor;
                    button.style.paddingLeft = 12;
                    _moduleButtons[module] = button;
                    _sidebarScrollView.Add(button);
                }
            }
        }

        private void RefreshContent()
        {
            if (_contentHost == null)
            {
                return;
            }

            _contentHost.Clear();

            if (_currentModule == null)
            {
                _moduleTitleLabel.text = "未选择模块";
                _moduleDescriptionLabel.text = "请选择左侧工具模块。";
                return;
            }

            _moduleTitleLabel.text = _currentModule.Title;
            _moduleDescriptionLabel.text = _currentModule.Description;

            try
            {
                VisualElement customView = _currentModule.CreateView();
                if (customView != null)
                {
                    customView.style.flexGrow = 1f;
                    _contentHost.Add(customView);
                    return;
                }

                ScrollView scrollView = new ScrollView
                {
                    style =
                    {
                        flexGrow = 1f
                    }
                };

                IMGUIContainer legacyContainer = new IMGUIContainer(() =>
                {
                    try
                    {
                        EnsureLegacyStyles();
                        _currentModule.OnGUI();
                    }
                    catch (Exception exception)
                    {
                        EditorGUILayout.HelpBox(
                            $"模块绘制出错: {exception.Message}\n{exception.StackTrace}",
                            MessageType.Error);
                    }
                })
                {
                    style =
                    {
                        flexGrow = 1f
                    }
                };

                scrollView.Add(legacyContainer);
                _contentHost.Add(scrollView);
            }
            catch (Exception exception)
            {
                HelpBox helpBox = new HelpBox(
                    $"模块视图创建失败: {exception.Message}\n{exception.StackTrace}",
                    HelpBoxMessageType.Error);
                _contentHost.Add(helpBox);
            }
        }

        private void SelectModule(ToolModule module)
        {
            if (module == null || module == _currentModule)
            {
                return;
            }

            _currentModule?.OnDisable();
            _currentModule = module;
            _currentModule.OnEnable();
            RefreshSidebar();
            RefreshContent();
        }

        private void ScanAndRegisterModules(string preferredTitle = null)
        {
            Type currentType = _currentModule?.GetType();
            string previousTitle = preferredTitle ?? _currentModule?.Title;

            _allModules.Clear();
            _groupedModules.Clear();

            IEnumerable<Type> derivedTypes = GetToolModuleTypes();
            foreach (Type type in derivedTypes)
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                StellarToolAttribute attr = type.GetCustomAttribute<StellarToolAttribute>();
                if (attr == null)
                {
                    continue;
                }

                if (!IsModuleAvailable(attr))
                {
                    continue;
                }

                try
                {
                    ToolModule module = (ToolModule)Activator.CreateInstance(type);
                    module.Initialize(this);
                    module.Title = attr.Title;
                    module.Group = attr.Group;
                    module.Order = attr.Order;
                    _allModules.Add(module);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[StellarFrameworkTools] 无法实例化模块 {type.Name}: {exception.Message}");
                }
            }

            List<ToolModule> sortedModules = _allModules.OrderBy(m => m.Order).ToList();
            foreach (ToolModule module in sortedModules)
            {
                if (!_groupedModules.ContainsKey(module.Group))
                {
                    _groupedModules[module.Group] = new List<ToolModule>();
                }

                _groupedModules[module.Group].Add(module);
            }

            _groupedModules = _groupedModules
                .OrderBy(pair => GetGroupOrder(pair.Key))
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            ToolModule selected = sortedModules.FirstOrDefault(module =>
                currentType != null && module.GetType() == currentType);
            if (selected == null && !string.IsNullOrEmpty(previousTitle))
            {
                selected = sortedModules.FirstOrDefault(module =>
                    string.Equals(module.Title, previousTitle, StringComparison.Ordinal));
            }

            if (selected == null && sortedModules.Count > 0)
            {
                selected = sortedModules[0];
            }

            _currentModule = selected;
            _currentModule?.OnEnable();
            Debug.Log($"[StellarFrameworkTools] 已加载 {_allModules.Count} 个工具模块");
        }

        private static int GetGroupOrder(string groupName)
        {
            for (int i = 0; i < PreferredGroupOrder.Length; i++)
            {
                if (string.Equals(PreferredGroupOrder[i], groupName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return PreferredGroupOrder.Length;
        }

        private static IEnumerable<Type> GetToolModuleTypes()
        {
#if UNITY_2019_2_OR_NEWER
            return TypeCache.GetTypesDerivedFrom<ToolModule>();
#else
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(type => type != null && typeof(ToolModule).IsAssignableFrom(type));
#endif
        }

        private static bool IsModuleAvailable(StellarToolAttribute attribute)
        {
            string[] requiredAssemblyNames = attribute.RequiredAssemblyNames;
            if (requiredAssemblyNames == null || requiredAssemblyNames.Length == 0)
            {
                return true;
            }

            HashSet<string> loadedAssemblyNames = new HashSet<string>(
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetName().Name),
                StringComparer.Ordinal);

            return requiredAssemblyNames.All(requiredAssemblyName =>
                !string.IsNullOrWhiteSpace(requiredAssemblyName) &&
                loadedAssemblyNames.Contains(requiredAssemblyName));
        }

#if !UNITY_2019_2_OR_NEWER
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
#endif

        private static VisualElement CreateCardContainer()
        {
            return new VisualElement
            {
                style =
                {
                    paddingLeft = 14,
                    paddingRight = 14,
                    paddingTop = 14,
                    paddingBottom = 14,
                    backgroundColor = CardBackground,
                    borderTopLeftRadius = 10,
                    borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    borderTopColor = BorderColor,
                    borderRightColor = BorderColor,
                    borderBottomColor = BorderColor,
                    borderLeftColor = BorderColor,
                    borderTopWidth = 1,
                    borderRightWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1
                }
            };
        }
    }
}
