using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// Source-project-only exporter. This window lives in the Packaging directory, which is excluded from all consumer
    /// distribution profiles, so a business project never receives this authoring tool.
    /// </summary>
    internal sealed class StellarFrameworkPackageExportWindow : EditorWindow
    {
        private const int BasicSection = 0;
        private const int CompleteSection = 1;
        private const int ExtensionDeliverySection = 2;
        private const int StandaloneSection = 3;

        private static readonly Color Accent = new Color(0.35f, 0.68f, 1.00f);
        private static readonly Color AccentDark = new Color(0.16f, 0.42f, 0.80f);
        private static readonly Color WindowBackground = new Color(0.10f, 0.11f, 0.13f);
        private static readonly Color PanelBackground = new Color(0.14f, 0.16f, 0.19f);
        private static readonly Color CardBackgroundSoft = new Color(0.16f, 0.18f, 0.21f);
        private static readonly Color BorderColor = new Color(0.27f, 0.31f, 0.37f);
        private static readonly Color TextPrimary = new Color(0.96f, 0.97f, 0.99f);
        private static readonly Color TextSecondary = new Color(0.73f, 0.77f, 0.83f);

        private readonly HashSet<string> _selectedProfileIds = new HashSet<string>(StringComparer.Ordinal);
        private Vector2 _scrollPosition;
        private string _outputFileName = "StellarFramework-CombinedKits.unitypackage";
        private string _search = string.Empty;
        private StellarFrameworkPackagePublisher.DistributionProfile[] _profiles = Array.Empty<StellarFrameworkPackagePublisher.DistributionProfile>();
        private StellarFrameworkPackagePublisher.RecommendedProfile[] _recommendedProfiles =
            Array.Empty<StellarFrameworkPackagePublisher.RecommendedProfile>();
        private readonly Dictionary<int, Button> _sectionButtons = new Dictionary<int, Button>();
        private int _activeSection;
        private ToolbarSearchField _searchField;
        private Label _sectionTitleLabel;
        private Label _sectionDescriptionLabel;
        private VisualElement _contentHost;

        [MenuItem("StellarFramework/Export")]
        private static void Open()
        {
            StellarFrameworkPackageExportWindow window =
                GetWindow<StellarFrameworkPackageExportWindow>("StellarFramework Export");
            window.minSize = new Vector2(1000f, 680f);
            window.Show();
        }

        [MenuItem("StellarFramework/Export", true)]
        private static bool ValidateOpen()
        {
            return StellarFrameworkPackagePublisher.IsFrameworkSourceProject();
        }

        private void OnEnable()
        {
            ReloadProfiles();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.backgroundColor = WindowBackground;
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            if (!StellarFrameworkPackagePublisher.IsFrameworkSourceProject())
            {
                rootVisualElement.Add(new HelpBox(
                    "此窗口只在 StellarFramework 原始框架工程中可用，不会随导出包进入业务项目。",
                    HelpBoxMessageType.Info));
                return;
            }

            rootVisualElement.Add(BuildTopBar());

            TwoPaneSplitView splitView = new TwoPaneSplitView(
                0,
                288,
                TwoPaneSplitViewOrientation.Horizontal)
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

            RefreshSidebarSelection();
            RefreshContent();
        }

        private VisualElement BuildTopBar()
        {
            VisualElement topBar = CreateCardContainer();
            topBar.style.flexGrow = 0f;
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems = Align.Center;
            topBar.style.minHeight = 60;
            topBar.style.paddingLeft = 16;
            topBar.style.paddingRight = 16;
            topBar.style.paddingTop = 12;
            topBar.style.paddingBottom = 12;

            VisualElement titleGroup = new VisualElement();
            titleGroup.style.flexDirection = FlexDirection.Column;
            titleGroup.Add(new Label("StellarFramework Export")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 18,
                    color = TextPrimary
                }
            });
            titleGroup.Add(new Label("统一导出中心 | 推荐组合、Kit、Adapter、Tooling 与源码")
            {
                style =
                {
                    marginTop = 2,
                    fontSize = 11,
                    color = TextSecondary
                }
            });
            topBar.Add(titleGroup);

            topBar.Add(new VisualElement
            {
                style = { flexGrow = 1f }
            });

            Button refreshButton = new Button(() =>
            {
                ReloadProfiles();
                RefreshContent();
            })
            {
                text = "刷新"
            };
            refreshButton.style.width = 72;
            refreshButton.style.height = 28;
            refreshButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            topBar.Add(refreshButton);

            Button cleanupButton = new Button(ConfirmAndCleanLegacyArtifacts)
            {
                text = "清理旧产物"
            };
            cleanupButton.tooltip = "只清理 BuildArtifacts/Kits 中明确标记为 Legacy / Validation 的历史包；当前 Catalog 输出受保护。";
            cleanupButton.style.width = 96;
            cleanupButton.style.height = 28;
            cleanupButton.style.marginLeft = 8;
            topBar.Add(cleanupButton);

            return topBar;
        }

        private static void ConfirmAndCleanLegacyArtifacts()
        {
            string[] candidates = StellarFrameworkPackagePublisher.GetLegacyAndValidationKitArtifactPaths();
            if (candidates.Length == 0)
            {
                EditorUtility.DisplayDialog("BuildArtifacts 清理", "没有发现可清理的 Legacy / Validation 产物。", "确定");
                return;
            }

            string preview = string.Join("\n", candidates.Take(12).Select(path => "• " + path));
            if (candidates.Length > 12)
            {
                preview += $"\n… 另有 {candidates.Length - 12} 个文件";
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "清理 Legacy / Validation 产物",
                $"将删除 {candidates.Length} 个历史导出文件。当前 Catalog 与 Recommended Profile 的正式输出已受保护。\n\n{preview}",
                "删除历史产物",
                "取消");
            if (!confirmed)
            {
                return;
            }

            int deleted = StellarFrameworkPackagePublisher.CleanLegacyAndValidationKitArtifacts();
            EditorUtility.DisplayDialog("BuildArtifacts 清理完成", $"已删除 {deleted} 个 Legacy / Validation 文件。", "确定");
        }

        private VisualElement BuildSidebarPane()
        {
            VisualElement sidebar = CreateCardContainer();
            sidebar.style.flexGrow = 1f;
            sidebar.style.marginRight = 6;

            sidebar.Add(new Label("导出导航")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    color = TextPrimary,
                    marginBottom = 10
                }
            });

            _searchField = new ToolbarSearchField
            {
                value = _search
            };
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                RefreshContent();
            });
            sidebar.Add(_searchField);

            ScrollView scroll = new ScrollView
            {
                style =
                {
                    flexGrow = 1f,
                    marginTop = 10
                }
            };

            AddSidebarGroup(scroll, "功能交付",
                (BasicSection, "01  基础功能"),
                (CompleteSection, "02  完整功能"),
                (ExtensionDeliverySection, "03  扩展功能"));
            AddSidebarGroup(scroll, "其他交付",
                (StandaloneSection, "源码与完整框架"));

            sidebar.Add(scroll);
            return sidebar;
        }

        private void AddSidebarGroup(VisualElement parent, string groupTitle,
            params (int Section, string Label)[] entries)
        {
            parent.Add(new Label(groupTitle)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = TextSecondary,
                    marginTop = 10,
                    marginBottom = 6,
                    fontSize = 11
                }
            });

            foreach ((int section, string label) in entries)
            {
                Button button = new Button(() => SelectSection(section))
                {
                    text = label
                };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.height = 36;
                button.style.marginBottom = 4;
                button.style.borderTopLeftRadius = 8;
                button.style.borderTopRightRadius = 8;
                button.style.borderBottomLeftRadius = 8;
                button.style.borderBottomRightRadius = 8;
                button.style.borderLeftWidth = 1;
                button.style.borderRightWidth = 1;
                button.style.borderTopWidth = 1;
                button.style.borderBottomWidth = 1;
                button.style.paddingLeft = 12;
                _sectionButtons[section] = button;
                parent.Add(button);
            }
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

            VisualElement header = CreateCardContainer();
            header.style.flexGrow = 0f;
            header.style.marginBottom = 10;
            _sectionTitleLabel = new Label
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 18,
                    color = TextPrimary
                }
            };
            _sectionDescriptionLabel = new Label
            {
                style =
                {
                    marginTop = 4,
                    color = TextSecondary,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            header.Add(_sectionTitleLabel);
            header.Add(_sectionDescriptionLabel);
            contentPane.Add(header);

            _contentHost = CreateCardContainer();
            _contentHost.style.flexGrow = 1f;
            _contentHost.style.paddingLeft = 10;
            _contentHost.style.paddingRight = 10;
            _contentHost.style.paddingTop = 10;
            _contentHost.style.paddingBottom = 10;
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
            footer.Add(new Label("依赖闭包自动解析 · Recommended Profile 不创建新的 Runtime 模块")
            {
                style = { color = TextSecondary }
            });
            footer.Add(new VisualElement
            {
                style = { flexGrow = 1f }
            });
            return footer;
        }

        private static VisualElement CreateCardContainer()
        {
            return new VisualElement
            {
                style =
                {
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
                    borderLeftWidth = 1,
                    paddingLeft = 14,
                    paddingRight = 14,
                    paddingTop = 14,
                    paddingBottom = 14
                }
            };
        }

        private void SelectSection(int section)
        {
            if (_activeSection == section)
            {
                return;
            }

            _activeSection = section;
            ReloadProfiles();
            RefreshSidebarSelection();
            RefreshContent();
        }

        private void RefreshSidebarSelection()
        {
            foreach (KeyValuePair<int, Button> pair in _sectionButtons)
            {
                bool selected = pair.Key == _activeSection;
                pair.Value.style.backgroundColor = selected ? AccentDark : CardBackgroundSoft;
                pair.Value.style.color = TextPrimary;
                pair.Value.style.borderLeftWidth = selected ? 3 : 1;
                pair.Value.style.borderLeftColor = selected ? Accent : BorderColor;
                pair.Value.style.borderRightColor = BorderColor;
                pair.Value.style.borderTopColor = BorderColor;
                pair.Value.style.borderBottomColor = BorderColor;
            }
        }

        private void RefreshContent()
        {
            if (_contentHost == null)
            {
                return;
            }

            _contentHost.Clear();
            (_sectionTitleLabel.text, _sectionDescriptionLabel.text) = GetSectionHeader(_activeSection);

            IMGUIContainer content = new IMGUIContainer(DrawActiveSection)
            {
                style = { flexGrow = 1f }
            };
            _contentHost.Add(content);
        }

        private void DrawActiveSection()
        {
            switch (_activeSection)
            {
                case CompleteSection:
                    DrawRecommendedProfilePage(
                        "complete",
                        "完整功能由多个原子 Profile 组合而成，面向“导入后即可完整开发”的使用场景。");
                    break;
                case BasicSection:
                    DrawProfileExportPage("基础功能", GetBasicDeliveryProfiles());
                    break;
                case ExtensionDeliverySection:
                    DrawExtensionDeliveryPage();
                    break;
                default:
                    DrawStandalonePage();
                    break;
            }
        }

        private static (string Title, string Description) GetSectionHeader(int section)
        {
            switch (section)
            {
                case BasicSection:
                    return ("01 · 基础功能", "可按需单独选择的原子 Runtime Kit。只自动补齐真实硬依赖，不会为了完整体验额外带入 ToolsHub 或无关功能。");
                case CompleteSection:
                    return ("02 · 完整功能", "面向直接投入项目生产的组合交付，例如 Localization Complete、ResKit Complete、UIKit Complete。");
                case ExtensionDeliverySection:
                    return ("03 · 扩展功能", "可选 Adapter / Tooling 与组合型扩展；既包含可单独加入现有项目的扩展，也包含 Hot Update Full 这类完整扩展。");
                default:
                    return ("源码与完整框架", "导出 Architecture / Extensions 单文件，或完整 StellarFramework 单包安装版。");
            }
        }

        private StellarFrameworkPackagePublisher.DistributionProfile[] GetBasicDeliveryProfiles()
        {
            return _profiles
                .Where(profile =>
                    (profile.kind == "kit" || profile.kind == "kit-with-dependencies") &&
                    !string.Equals(profile.tier, "adapter", StringComparison.Ordinal))
                .ToArray();
        }

        private StellarFrameworkPackagePublisher.DistributionProfile[] GetAtomicExtensionProfiles()
        {
            return _profiles
                .Where(profile =>
                    string.Equals(profile.tier, "adapter", StringComparison.Ordinal) ||
                    string.Equals(profile.kind, "tooling", StringComparison.Ordinal))
                .ToArray();
        }

        private void ReloadProfiles()
        {
            _recommendedProfiles = StellarFrameworkPackagePublisher.GetRecommendedProfiles();
            _profiles = StellarFrameworkPackagePublisher.GetSourceProjectExportProfiles();
            if (_activeSection != CompleteSection && _activeSection != ExtensionDeliverySection)
            {
                _selectedProfileIds.RemoveWhere(profileId => _profiles.All(profile => profile.id != profileId));
            }
            Repaint();
        }

        private void DrawRecommendedProfilePage(string deliveryGroup, string introduction)
        {
            EditorGUILayout.HelpBox(
                introduction + "\n组合只引用已有原子 Profile，不创建新的 Runtime 模块。",
                MessageType.Info);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            StellarFrameworkPackagePublisher.RecommendedProfile[] visibleProfiles = _recommendedProfiles
                .Where(profile => string.Equals(profile.deliveryGroup, deliveryGroup, StringComparison.Ordinal))
                .Where(MatchesRecommendedSearch)
                .ToArray();
            foreach (StellarFrameworkPackagePublisher.RecommendedProfile profile in visibleProfiles)
            {
                DrawRecommendedProfileCard(profile);
            }

            if (visibleProfiles.Length == 0)
            {
                EditorGUILayout.HelpBox("当前没有匹配的组合功能。", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawExtensionDeliveryPage()
        {
            EditorGUILayout.HelpBox(
                "扩展功能分为两类：原子扩展可以直接加入现有项目；组合扩展会自动解析多个 Kit / Tooling 的完整闭包。",
                MessageType.Info);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            StellarFrameworkPackagePublisher.DistributionProfile[] atomics = GetAtomicExtensionProfiles()
                .Where(MatchesSearch)
                .OrderBy(profile => profile.displayName, StringComparer.Ordinal)
                .ToArray();
            DrawProfileSection(
                "可单独导出的扩展",
                "Adapter 与 Editor Tooling；只自动补齐自身真实硬依赖。",
                atomics);

            StellarFrameworkPackagePublisher.RecommendedProfile[] composed = _recommendedProfiles
                .Where(profile => string.Equals(profile.deliveryGroup, "extension", StringComparison.Ordinal))
                .Where(MatchesRecommendedSearch)
                .ToArray();
            if (composed.Length > 0)
            {
                EditorGUILayout.LabelField("需要组合导出的完整扩展", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "组合扩展仍由独立 Profile 构成，例如 Hot Update Full。",
                    EditorStyles.miniLabel);
                foreach (StellarFrameworkPackagePublisher.RecommendedProfile profile in composed)
                {
                    DrawRecommendedProfileCard(profile);
                }
            }

            if (atomics.Length == 0 && composed.Length == 0)
            {
                EditorGUILayout.HelpBox("没有匹配的扩展功能。", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            DrawExportFooter();
        }

        private void DrawRecommendedProfileCard(StellarFrameworkPackagePublisher.RecommendedProfile profile)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string maturity = StellarFrameworkPackagePublisher.ResolveRecommendedProfileMaturity(profile.id);
                EditorGUILayout.LabelField(
                    $"{profile.displayName}    [ {GetMaturityLabel(maturity)} ]",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(profile.id, EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(profile.description))
                {
                    EditorGUILayout.LabelField(profile.description, EditorStyles.wordWrappedMiniLabel);
                }

                string[] roots = profile.profileIds ?? Array.Empty<string>();
                EditorGUILayout.LabelField(
                    "组合入口：" + (roots.Length == 0 ? "无" : string.Join("、", roots)),
                    EditorStyles.wordWrappedMiniLabel);

                try
                {
                    string[] closureIds =
                        StellarFrameworkPackagePublisher.ResolveRecommendedProfileClosureIds(profile.id);
                    EditorGUILayout.LabelField(
                        $"最终依赖闭包：{closureIds.Length} 个 Profile\n{string.Join("、", closureIds)}",
                        EditorStyles.wordWrappedMiniLabel);
                }
                catch (Exception exception)
                {
                    EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                }

                EditorGUILayout.LabelField("输出：" + profile.output, EditorStyles.miniLabel);
                if (GUILayout.Button("导出此 Recommended Profile", GUILayout.Height(30f)))
                {
                    ExportRecommendedProfile(profile.id);
                }
            }
        }

        private void DrawProfileExportPage(string profileLabel,
            IEnumerable<StellarFrameworkPackagePublisher.DistributionProfile> sourceProfiles)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFileName = EditorGUILayout.TextField("组合包文件名", _outputFileName);
            }

            EditorGUILayout.Space(4f);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            StellarFrameworkPackagePublisher.DistributionProfile[] visibleProfiles = sourceProfiles
                .Where(MatchesSearch)
                .ToArray();

            if (_activeSection == BasicSection)
            {
                foreach (IGrouping<string, StellarFrameworkPackagePublisher.DistributionProfile> categoryGroup in
                         visibleProfiles
                             .GroupBy(profile => profile.category ?? string.Empty)
                             .OrderBy(group => GetCategoryLabel(group.Key), StringComparer.Ordinal))
                {
                    DrawProfileSection(
                        GetCategoryLabel(categoryGroup.Key),
                        "按领域分类，实际导出仍只取所选 Profile 与真实依赖闭包。",
                        categoryGroup.OrderBy(profile => profile.displayName, StringComparer.Ordinal));
                }
            }
            else
            {
                const string sectionDescription =
                    "Editor-only / shared support / ToolsHub 能力；不会因为显示在这里就自动进入其他 Kit。";
                DrawProfileSection(
                    profileLabel,
                    sectionDescription,
                    visibleProfiles.OrderBy(profile => profile.displayName, StringComparer.Ordinal));
            }

            if (visibleProfiles.Length == 0)
            {
                EditorGUILayout.HelpBox("没有匹配的 Kit / Profile。", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            DrawExportFooter();
        }

        private void DrawProfileSection(string title, string description,
            IEnumerable<StellarFrameworkPackagePublisher.DistributionProfile> profiles)
        {
            StellarFrameworkPackagePublisher.DistributionProfile[] sectionProfiles = profiles.ToArray();
            if (sectionProfiles.Length == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            foreach (StellarFrameworkPackagePublisher.DistributionProfile profile in sectionProfiles)
            {
                DrawProfileCard(profile);
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawProfileCard(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool selected = _selectedProfileIds.Contains(profile.id);
                bool nextSelected = EditorGUILayout.ToggleLeft(
                    $"{profile.displayName}    {GetProfileBadge(profile)}", selected, EditorStyles.boldLabel);
                if (nextSelected != selected)
                {
                    if (nextSelected)
                    {
                        _selectedProfileIds.Add(profile.id);
                    }
                    else
                    {
                        _selectedProfileIds.Remove(profile.id);
                    }
                }

                EditorGUILayout.LabelField(profile.id, EditorStyles.miniLabel);
                string[] requiredProfiles = profile.requiredProfileIds ?? Array.Empty<string>();
                EditorGUILayout.LabelField(requiredProfiles.Length == 0
                    ? "自动包含：无其他 StellarFramework Kit"
                    : "自动包含：" + string.Join("、", requiredProfiles), EditorStyles.wordWrappedMiniLabel);

                string[] requiredUpm = profile.requiredUpm ?? Array.Empty<string>();
                if (requiredUpm.Length > 0)
                {
                    EditorGUILayout.LabelField("额外 UPM：" + string.Join("、", requiredUpm),
                        EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawExportFooter()
        {
            string[] selectedIds = _selectedProfileIds.OrderBy(profileId => profileId).ToArray();
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"已选择 {selectedIds.Length} 个 Profile", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(selectedIds.Length == 0))
                {
                    if (GUILayout.Button("清空选择", GUILayout.Width(84f)))
                    {
                        _selectedProfileIds.Clear();
                        Repaint();
                        return;
                    }
                }
            }
            DrawDependencyPreview(selectedIds);

            using (new EditorGUI.DisabledScope(selectedIds.Length == 0))
            {
                if (GUILayout.Button("导出已选组合（自动合并依赖）", GUILayout.Height(34f)))
                {
                    ExportSelectedProfiles(selectedIds);
                }
            }
        }

        private static bool HasProfileDependencies(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            return profile.requiredProfileIds != null && profile.requiredProfileIds.Length > 0;
        }

        private bool MatchesSearch(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return true;
            }

            return ContainsIgnoreCase(profile.displayName, _search) || ContainsIgnoreCase(profile.id, _search) ||
                   ContainsIgnoreCase(profile.tier, _search) || ContainsIgnoreCase(profile.category, _search) ||
                   ContainsIgnoreCase(profile.maturity, _search);
        }

        private bool MatchesRecommendedSearch(StellarFrameworkPackagePublisher.RecommendedProfile profile)
        {
            if (string.IsNullOrWhiteSpace(_search))
            {
                return true;
            }

            string query = _search.Trim();
            return ContainsIgnoreCase(profile.displayName, query) ||
                   ContainsIgnoreCase(profile.id, query) ||
                   ContainsIgnoreCase(profile.description, query) ||
                   (profile.profileIds ?? Array.Empty<string>()).Any(value => ContainsIgnoreCase(value, query));
        }

        private static bool ContainsIgnoreCase(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetProfileBadge(StellarFrameworkPackagePublisher.DistributionProfile profile)
        {
            string dependencyBadge = HasProfileDependencies(profile) ? "自动带依赖" : "独立";
            string maturityBadge = GetMaturityLabel(profile.maturity);
            if (string.IsNullOrWhiteSpace(profile.tier))
            {
                return "[ " + maturityBadge + " · " + dependencyBadge + " ]";
            }

            return "[ " + maturityBadge + " · " + GetTierLabel(profile.tier) + " · " + GetCategoryLabel(profile.category) + " · " +
                   dependencyBadge + " ]";
        }

        private static string GetMaturityLabel(string maturity)
        {
            switch (maturity)
            {
                case "stable":
                    return "Stable";
                case "rc":
                    return "RC";
                case "experimental":
                    return "Experimental";
                default:
                    return "Unknown";
            }
        }

        private static string GetTierLabel(string tier)
        {
            switch (tier)
            {
                case "foundation":
                    return "Foundation";
                case "extension":
                    return "Extension";
                case "adapter":
                    return "Adapter";
                default:
                    return "未分类";
            }
        }

        private static string GetCategoryLabel(string category)
        {
            switch (category)
            {
                case "diagnostics":
                    return "诊断";
                case "infrastructure":
                    return "基础设施";
                case "flow":
                    return "流程与状态";
                case "data":
                    return "数据与服务";
                case "network":
                    return "网络";
                case "resource":
                    return "资源";
                case "simulation":
                    return "模拟基础";
                case "presentation":
                    return "表现";
                case "world":
                    return "世界";
                case "gameplay":
                    return "游戏系统";
                case "runtime-delivery":
                    return "发布与运行环境";
                default:
                    return "其他";
            }
        }

        private static void DrawStandalonePage()
        {
            EditorGUILayout.HelpBox(
                "下面两个导出不会包含任何 StellarFramework Kit，也不会携带 ToolsHub、ResKit、热更或 HybridCLR。\n" +
                "LogKit 调用会替换为 UnityEngine.Debug，适合只想复制一个 .cs 文件的项目。",
                MessageType.Info);

            DrawStandaloneCard(
                "Architecture.cs",
                "框架的架构基类单文件版。适合只使用 Architecture 设计模式，不引入其他 Kit。",
                "导出 Architecture.cs",
                StellarFrameworkPackagePublisher.ExportStandaloneArchitecture);
            DrawStandaloneCard(
                "Extensions.cs",
                "全部静态扩展的单文件版。适合只需要集合、字符串、Transform、颜色等扩展方法。",
                "导出 Extensions.cs",
                StellarFrameworkPackagePublisher.ExportStandaloneExtensions);

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("同时导出 Architecture.cs + Extensions.cs", GUILayout.Height(34f)))
            {
                StellarFrameworkPackagePublisher.ExportAllStandaloneSources();
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("完整框架", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "需要完整框架时可导出单包安装版。该包包含完整能力与安装器，和按需 Kit 导出是两种不同的交付方式。",
                MessageType.None);
            if (GUILayout.Button("导出完整单包安装版", GUILayout.Height(30f)))
            {
                StellarFrameworkPackagePublisher.ExportSinglePackageInstaller();
            }
        }

        private static void DrawStandaloneCard(string title, string description, string buttonLabel, Action exportAction)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title + "    [ 无 Kit 依赖 ]", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(buttonLabel, GUILayout.Height(28f)))
                {
                    exportAction();
                }
            }
        }

        private static void DrawDependencyPreview(IEnumerable<string> selectedIds)
        {
            try
            {
                string[] closureIds = selectedIds
                    .SelectMany(StellarFrameworkPackagePublisher.ResolveKitProfileClosureIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(profileId => profileId)
                    .ToArray();
                EditorGUILayout.HelpBox(
                    $"导出内容：{closureIds.Length} 个去重 Profile\n{string.Join("、", closureIds)}",
                    MessageType.None);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }

        private void ExportSelectedProfiles(IEnumerable<string> selectedIds)
        {
            try
            {
                string outputPath = StellarFrameworkPackagePublisher.ExportKitPackageGroupInternal(
                    selectedIds, _outputFileName);
                EditorUtility.RevealInFinder(outputPath);
                ShowNotification(new GUIContent("组合 Kit 导出完成"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Kit 导出失败", exception.Message, "确定");
            }
        }

        private void ExportRecommendedProfile(string recommendedProfileId)
        {
            try
            {
                string outputPath =
                    StellarFrameworkPackagePublisher.ExportRecommendedProfileInternal(recommendedProfileId);
                EditorUtility.RevealInFinder(outputPath);
                ShowNotification(new GUIContent("Recommended Profile 导出完成"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Recommended Profile 导出失败", exception.Message, "确定");
            }
        }
    }
}
