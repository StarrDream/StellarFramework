using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Editor.Modules.FlowKit
{
    internal sealed class FlowKitEditorWorkspace : IDisposable
    {
        private static readonly FlowValueKind[] AuthorableKinds =
        {
            FlowValueKind.Bool, FlowValueKind.Int, FlowValueKind.Long, FlowValueKind.Float,
            FlowValueKind.Double, FlowValueKind.String, FlowValueKind.Vector2, FlowValueKind.Vector3,
            FlowValueKind.BindingReference, FlowValueKind.Asset, FlowValueKind.Enum, FlowValueKind.None
        };

        private readonly FlowNodeRegistry _registry;
        private readonly VisualElement _root = new VisualElement();
        private readonly VisualElement _nodeList = new VisualElement();
        private readonly ScrollView _validationList = new ScrollView();
        private readonly ScrollView _referenceList = new ScrollView();
        private readonly ScrollView _helpList = new ScrollView();
        private readonly ScrollView _runtimeList = new ScrollView();
        private readonly VisualElement _bottomContent = new VisualElement();
        private int _bottomTab;
        private readonly FlowGraphCanvas _canvas = new FlowGraphCanvas();
        private readonly IMGUIContainer _inspector;
        private readonly Label _fileLabel = new Label();
        private readonly Label _statusLabel = new Label();
        private FlowGraphDocument _document;
        private FlowCompileResult _lastResult;
        private FlowEditorUndoState _undoState;
        private string _selectedNodeId;
        private string _nodeSearch = string.Empty;
        private string _newArgumentKey = string.Empty;
        private FlowValueKind _newArgumentKind = FlowValueKind.String;
        private Vector2 _inspectorScroll;
        private bool _disposed;
        private double _nextRuntimeRefresh;
        private double _nextValidationTime = -1d;
        private FlowHost _debugHost;
        private FlowRunId _debugRunId;
        private readonly List<FlowExecutionDiagnostics> _debugExecutions = new List<FlowExecutionDiagnostics>();
        private readonly HashSet<string> _activeRuntimeNodes = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _selectionBuffer = new List<string>();
        private int _pasteSerial;

        public FlowKitEditorWorkspace()
        {
            _registry = FlowKitEditorRegistry.Create();
            _inspector = new IMGUIContainer(DrawInspector);
            BuildLayout();
            BindCanvas();
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += OnEditorUpdate;
            NewDocument(false);
        }

        public VisualElement Root => _root;
        public bool IsDirty => _document != null && _document.IsDirty;
        public string CurrentPath => _document?.RuntimePath ?? string.Empty;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            if (_undoState != null) UnityEngine.Object.DestroyImmediate(_undoState);
        }

        private void OnEditorUpdate()
        {
            if (_disposed) return;
            double now = EditorApplication.timeSinceStartup;
            if (_nextValidationTime >= 0d && now >= _nextValidationTime)
            {
                _nextValidationTime = -1d;
                Validate();
            }
            bool needsRuntimeRefresh = _bottomTab == 2 || _debugRunId.IsValid;
            if (!needsRuntimeRefresh || now < _nextRuntimeRefresh) return;
            _nextRuntimeRefresh = now + 0.25d;
            RefreshRuntimeOverlay();
            if (_bottomTab == 2) RebuildRuntime();
        }

        private void ScheduleValidation()
        {
            _nextValidationTime = EditorApplication.timeSinceStartup + 0.35d;
        }

        private void RebuildRuntime()
        {
            _runtimeList.Clear();
            FlowHost[] hosts = Resources.FindObjectsOfTypeAll<FlowHost>();
            int sceneHostCount = 0;
            for (int i = 0; i < hosts.Length; i++)
            {
                FlowHost host = hosts[i];
                if (host == null || EditorUtility.IsPersistent(host) || !host.gameObject.scene.IsValid()) continue;
                sceneHostCount++;

                var card = new VisualElement();
                FlowKitEditorTheme.ApplyCard(card);
                card.style.marginLeft = 6f;
                card.style.marginRight = 6f;
                card.style.marginBottom = 5f;
                card.style.paddingLeft = 8f;
                card.style.paddingRight = 8f;
                card.style.paddingTop = 5f;
                card.style.paddingBottom = 5f;

                var title = new Button(() =>
                {
                    Selection.activeObject = host.gameObject;
                    EditorGUIUtility.PingObject(host.gameObject);
                })                {
                    text = $"{host.gameObject.name}  ·  {host.gameObject.scene.name}"
                };
                title.style.unityTextAlign = TextAnchor.MiddleLeft;
                FlowKitEditorTheme.ApplyButton(title);
                card.Add(title);

                if (!host.IsInitialized || host.Runner == null)
                {
                    card.Add(new Label("尚未初始化"));
                    _runtimeList.Add(card);
                    continue;
                }

                FlowRuntimeDiagnostics d = host.Runner.CaptureDiagnostics();
                card.Add(new Label($"运行流程 {d.ActiveRuns}   待激活 {d.PendingActivations}   待完成 {d.PendingCompletions}   待处理信号 {d.PendingSignals}"));
                card.Add(new Label($"计时器 {d.ActiveTimers}   Polling {d.ActivePollers}   外部操作 {d.ActiveOperations}   状态 {d.StateCount}   绑定 {d.ActiveBindings}"));

                for (int r = 0; r < host.Runner.ActiveRunCount; r++)
                {
                    FlowRun run = host.Runner.GetActiveRun(r);
                    AddRunRow(card, host, run);
                }

                _runtimeList.Add(card);            }

            if (sceneHostCount == 0)
            {
                _runtimeList.Add(new Label(Application.isPlaying
                    ? "当前场景没有 FlowHost。"
                    : "进入 Play Mode 后可在这里查看 FlowHost 运行状态。"));
            }

            FlowRun selected = ResolveDebugRun();
            if (selected != null) AddTraceSection(selected);
        }

        private void AddRunRow(VisualElement parent, FlowHost host, FlowRun run)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4f;
            bool selected = ReferenceEquals(host, _debugHost) && run.RunId.Equals(_debugRunId);
            string state = selected ? "● 调试中" : run.Status.ToString();
            var label = new Label($"Run {run.RunId.Value} · {run.Plan.FlowId} · {state}");
            label.style.flexGrow = 1f;
            label.style.color = selected ? FlowKitEditorTheme.Success : FlowKitEditorTheme.TextPrimary;
            row.Add(label);
            bool sameFlow = _document != null &&
                string.Equals(_document.Graph.FlowId, run.Plan.FlowId, StringComparison.Ordinal);
            var bind = new Button(() => SetDebugRun(host, run))
            {
                text = selected ? "取消调试" : "调试此流程"
            };
            bind.SetEnabled(selected || sameFlow);
            if (!sameFlow && !selected) bind.tooltip = "当前打开的 FlowId 与运行流程不同。";
            FlowKitEditorTheme.ApplyButton(bind, selected);
            row.Add(bind);
            parent.Add(row);
        }

        private void SetDebugRun(FlowHost host, FlowRun run)
        {
            if (run == null || host == null) return;
            if (ReferenceEquals(_debugHost, host) && run.RunId.Equals(_debugRunId))
            {
                _debugHost = null;
                _debugRunId = default(FlowRunId);
                _activeRuntimeNodes.Clear();
                _canvas.SetRuntimeActiveNodes(null);
                return;
            }

            _debugHost = host;            _debugRunId = run.RunId;
            RefreshRuntimeOverlay();
        }

        private FlowRun ResolveDebugRun()
        {
            if (_debugHost == null || !_debugRunId.IsValid || !_debugHost.IsInitialized ||
                _debugHost.Runner == null) return null;
            for (int i = 0; i < _debugHost.Runner.ActiveRunCount; i++)
            {
                FlowRun run = _debugHost.Runner.GetActiveRun(i);
                if (run.RunId.Equals(_debugRunId)) return run;
            }
            return null;
        }

        private void RefreshRuntimeOverlay()
        {
            FlowRun run = ResolveDebugRun();
            _activeRuntimeNodes.Clear();
            if (run == null)
            {
                if (_debugRunId.IsValid)
                {
                    _debugRunId = default(FlowRunId);
                    _debugHost = null;
                }
                _canvas.SetRuntimeActiveNodes(null);                return;
            }

            if (_document == null ||
                !string.Equals(_document.Graph.FlowId, run.Plan.FlowId, StringComparison.Ordinal))
            {
                _canvas.SetRuntimeActiveNodes(null);
                return;
            }

            _debugExecutions.Clear();
            run.CopyActiveExecutionDiagnostics(_debugExecutions);
            for (int i = 0; i < _debugExecutions.Count; i++)
            {
                if (!string.IsNullOrEmpty(_debugExecutions[i].NodeId))
                    _activeRuntimeNodes.Add(_debugExecutions[i].NodeId);
            }
            _canvas.SetRuntimeActiveNodes(_activeRuntimeNodes);
        }

        private void AddTraceSection(FlowRun run)
        {
            if (!(_debugHost?.Services?.Trace is FlowTraceRingBuffer trace)) return;
            FlowTraceEvent[] events = trace.Snapshot();
            if (events.Length == 0) return;
            _runtimeList.Add(FlowKitEditorTheme.Header("Trace · 最近事件"));            int start = Math.Max(0, events.Length - 80);
            for (int i = events.Length - 1; i >= start; i--)
            {
                FlowTraceEvent evt = events[i];
                if (!evt.RunId.Equals(run.RunId)) continue;
                string node = string.IsNullOrEmpty(evt.NodeId) ? string.Empty : $" · {evt.NodeId}";
                var button = new Button(() =>
                {
                    if (!string.IsNullOrEmpty(evt.NodeId)) _canvas.FocusNode(evt.NodeId);
                })
                {
                    text = $"#{evt.Sequence} · {TraceKindName(evt.Kind)}{node}"
                };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                FlowKitEditorTheme.ApplyButton(button);
                _runtimeList.Add(button);
            }
        }

        private static string TraceKindName(FlowTraceEventKind kind)
        {
            switch (kind)
            {
                case FlowTraceEventKind.RunStarted: return "流程开始";
                case FlowTraceEventKind.NodeActivated: return "节点激活";
                case FlowTraceEventKind.NodeCompleted: return "节点完成";
                case FlowTraceEventKind.NodeFailed: return "节点失败";                case FlowTraceEventKind.RunCompleted: return "流程完成";
                case FlowTraceEventKind.RunFailed: return "流程失败";
                case FlowTraceEventKind.RunCancelled: return "流程取消";
                case FlowTraceEventKind.SignalPublished: return "发送信号";
                case FlowTraceEventKind.TimerScheduled: return "计时开始";
                case FlowTraceEventKind.TimerFired: return "计时触发";
                case FlowTraceEventKind.OperationStarted: return "外部操作开始";
                case FlowTraceEventKind.OperationCompleted: return "外部操作完成";
                default: return kind.ToString();
            }
        }

        public void OpenSelectedAsset()
        {
            TextAsset asset = Selection.activeObject as TextAsset;
            if (asset == null)
            {
                ShowNotification("请先在 Project 中选择一个 Flow JSON 文件。");
                return;
            }
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath)) return;
            OpenPath(Path.GetFullPath(Path.Combine(ProjectRoot, assetPath)));
        }

        private const string FireDrillSampleAssetPath =
            "Assets/StellarFramework/Samples/KitSamples/Example_FlowKit/FireDrillWorkflow4P.flow.json";

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? string.Empty;

        private void BuildLayout()
        {
            _root.style.flexGrow = 1f;
            _root.style.flexDirection = FlexDirection.Column;
            FlowKitEditorTheme.ApplyRoot(_root);
            BuildToolbar();
            BuildMainArea();
            BuildValidationArea();
        }

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 30f;
            toolbar.style.paddingLeft = 4f;
            toolbar.style.paddingRight = 4f;
            toolbar.style.alignItems = Align.Center;
            FlowKitEditorTheme.ApplyToolbar(toolbar);
            toolbar.Add(CreateButton("新建", () => NewDocument(true)));
            toolbar.Add(CreateButton("打开", OpenDialog));
            toolbar.Add(CreateButton("打开选中", OpenSelectedAsset));
            toolbar.Add(CreateButton("保存", Save, true));
            toolbar.Add(CreateButton("另存为", SaveAs));
            toolbar.Add(CreateButton("复制", CopySelection));
            toolbar.Add(CreateButton("粘贴", PasteSystemClipboard));
            toolbar.Add(CreateButton("副本", DuplicateSelection));
            toolbar.Add(CreateButton("校验", Validate, true));
            toolbar.Add(CreateButton("项目校验", ValidateProject));
            if (HasFireDrillSample())
                toolbar.Add(CreateButton("消防样例", OpenFireDrillSample));
            toolbar.Add(CreateButton("定位全部", () => _canvas.FrameAll()));
            _fileLabel.style.marginLeft = 10f;
            _fileLabel.style.flexGrow = 1f;
            toolbar.Add(_fileLabel);
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            toolbar.Add(_statusLabel);
            _root.Add(toolbar);
        }

        private static Button CreateButton(string text, Action action, bool primary = false)
        {
            var button = new Button(action) { text = text };
            FlowKitEditorTheme.ApplyButton(button, primary);
            return button;
        }

        private void BuildMainArea()
        {
            var main = new VisualElement();
            main.style.flexDirection = FlexDirection.Row;
            main.style.flexGrow = 1f;

            var library = new VisualElement();
            library.style.width = 238f;
            library.style.minWidth = 200f;
            library.style.borderRightWidth = 1f;
            library.style.paddingLeft = 8f;
            library.style.paddingRight = 8f;
            library.style.backgroundColor = FlowKitEditorTheme.CardSoft;
            FlowKitEditorTheme.ApplyPanelDivider(library);
            library.Add(FlowKitEditorTheme.Header("节点库"));
            var search = new TextField("搜索") { value = _nodeSearch };
            search.RegisterValueChangedCallback(evt =>
            {
                _nodeSearch = evt.newValue ?? string.Empty;
                RebuildNodeLibrary();
            });
            library.Add(search);
            var libraryScroll = new ScrollView();
            libraryScroll.style.flexGrow = 1f;
            libraryScroll.Add(_nodeList);
            library.Add(libraryScroll);

            _canvas.style.flexGrow = 1f;
            _canvas.style.minWidth = 400f;
            _canvas.style.backgroundColor = FlowKitEditorTheme.Panel;
            _inspector.style.width = 360f;
            _inspector.style.minWidth = 300f;
            _inspector.style.borderLeftWidth = 1f;
            _inspector.style.borderLeftColor = FlowKitEditorTheme.Border;
            _inspector.style.backgroundColor = FlowKitEditorTheme.CardSoft;
            _inspector.style.paddingLeft = 10f;
            _inspector.style.paddingRight = 10f;

            main.Add(library);
            main.Add(_canvas);
            main.Add(_inspector);
            _root.Add(main);
            RebuildNodeLibrary();
        }

        private void BuildValidationArea()
        {
            var bottom = new VisualElement();
            bottom.style.height = 174f;
            bottom.style.minHeight = 120f;
            bottom.style.backgroundColor = FlowKitEditorTheme.CardSoft;
            bottom.style.borderTopWidth = 1f;
            bottom.style.borderTopColor = FlowKitEditorTheme.Border;

            var tabs = new VisualElement();
            tabs.style.height = 28f;
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.alignItems = Align.Center;
            tabs.style.paddingLeft = 6f;
            tabs.Add(CreateButton("校验", () => ShowBottomTab(0), true));
            tabs.Add(CreateButton("流程引用", () => ShowBottomTab(1)));
            tabs.Add(CreateButton("运行时", () => ShowBottomTab(2)));
            tabs.Add(CreateButton("使用帮助", () => ShowBottomTab(3)));
            bottom.Add(tabs);

            _bottomContent.style.flexGrow = 1f;
            _validationList.style.flexGrow = 1f;
            _referenceList.style.flexGrow = 1f;
            _runtimeList.style.flexGrow = 1f;
            _helpList.style.flexGrow = 1f;
            bottom.Add(_bottomContent);
            _root.Add(bottom);
            BuildHelpContent();
            ShowBottomTab(0);
        }

        private void ShowBottomTab(int tab)
        {
            _bottomTab = tab;
            _bottomContent.Clear();
            if (tab == 1)
            {
                RebuildReferences();
                _bottomContent.Add(_referenceList);
                return;
            }
            if (tab == 2)
            {
                RebuildRuntime();
                _bottomContent.Add(_runtimeList);
                return;
            }
            if (tab == 3)
            {
                _bottomContent.Add(_helpList);
                return;
            }
            _bottomContent.Add(_validationList);
        }

        private void BuildHelpContent()
        {
            _helpList.Clear();
            _helpList.Add(new Label("FlowKit 只负责流程管控，不负责玩家具体如何操作。"));
            _helpList.Add(new Label("操作建议：从左侧节点库创建节点，拖拽端口连线，右侧编辑参数，最后点击“校验”。"));
            _helpList.Add(new Label("外部玩法通过 Operation / Signal / State 与 FlowKit 交互；流程自身数据使用 Blackboard。"));
            _helpList.Add(new Label("运行时 JSON 与编辑器布局分离保存，移动节点不会改变运行计划哈希。"));
        }

        private void RebuildReferences()
        {
            _referenceList.Clear();
            if (_document == null) return;
            AddReferenceSection("外部操作", "operation");
            AddReferenceSection("信号", "signal");
            AddReferenceSection("状态", "state");
            AddReferenceSection("黑板键", "key", "blackboardKey");
            AddBindingReferenceSection();
        }

        private void AddReferenceSection(string title, params string[] propertyKeys)
        {
            var values = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _document.Graph.Nodes.Count; i++)
            {
                FlowNodeData node = _document.Graph.Nodes[i];
                if (node?.Parameters == null) continue;
                for (int k = 0; k < propertyKeys.Length; k++)
                {
                    if (node.Parameters.TryGet(propertyKeys[k], out FlowValue value) &&
                        value.Kind == FlowValueKind.String && !string.IsNullOrEmpty(value.StringValue))
                        values.Add(value.StringValue);
                }
            }
            if (values.Count == 0) return;
            _referenceList.Add(FlowKitEditorTheme.Header(title));
            foreach (string value in values) _referenceList.Add(new Label("  • " + value));
        }

        private void AddBindingReferenceSection()
        {
            var values = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _document.Graph.Nodes.Count; i++)
            {
                FlowPropertyBag bag = _document.Graph.Nodes[i]?.Parameters;
                if (bag?.Entries == null) continue;
                for (int j = 0; j < bag.Entries.Count; j++)
                {
                    FlowValue value = bag.Entries[j].Value;
                    if (value.Kind == FlowValueKind.BindingReference && value.BindingReferenceValue.IsValid)
                        values.Add(value.BindingReferenceValue.Id);
                }
            }
            if (values.Count == 0) return;
            _referenceList.Add(FlowKitEditorTheme.Header("场景绑定"));
            foreach (string value in values) _referenceList.Add(new Label("  • " + value));
        }

        private void BindCanvas()
        {
            _canvas.NodeSelected += id =>
            {
                _selectedNodeId = id;
                _inspector.MarkDirtyRepaint();
            };
            _canvas.NodeMoved += OnNodeMoved;
            _canvas.NodesDeleted += OnNodesDeleted;
            _canvas.EdgeCreated += OnEdgeCreated;
            _canvas.EdgeRemoved += OnEdgeRemoved;
            _canvas.NodeCreationRequested += ShowNodeCreationMenu;
            _canvas.SerializeNodes = ids => _document?.CreateClipboard(ids) ?? string.Empty;
            _canvas.CanPasteNodes = FlowGraphDocument.CanPasteClipboard;
            _canvas.PasteNodesRequested = PasteSerializedNodes;
        }

        private void CopySelection()
        {
            if (_document == null) return;
            _selectionBuffer.Clear();
            _canvas.GetSelectedNodeIds(_selectionBuffer);
            string payload = _document.CreateClipboard(_selectionBuffer);
            if (string.IsNullOrEmpty(payload))
            {
                ShowNotification("\u8bf7\u5148\u9009\u62e9\u8981\u590d\u5236\u7684\u8282\u70b9\u3002");
                return;
            }
            GUIUtility.systemCopyBuffer = payload;
            ShowNotification($"\u5df2\u590d\u5236 {_selectionBuffer.Count} \u4e2a\u8282\u70b9\u3002");
        }

        private void PasteSystemClipboard()
        {
            PasteSerializedNodes(GUIUtility.systemCopyBuffer);
        }

        private void DuplicateSelection()
        {
            if (_document == null) return;
            _selectionBuffer.Clear();
            _canvas.GetSelectedNodeIds(_selectionBuffer);
            string payload = _document.CreateClipboard(_selectionBuffer);
            if (string.IsNullOrEmpty(payload))
            {
                ShowNotification("\u8bf7\u5148\u9009\u62e9\u8981\u521b\u5efa\u526f\u672c\u7684\u8282\u70b9\u3002");
                return;
            }
            PasteSerializedNodes(payload);
        }

        private void PasteSerializedNodes(string payload)
        {
            if (_document == null || !FlowGraphDocument.CanPasteClipboard(payload))
            {
                ShowNotification("\u526a\u8d34\u677f\u4e2d\u6ca1\u6709\u53ef\u7528\u7684 FlowKit \u8282\u70b9\u6570\u636e\u3002");
                return;
            }
            RecordUndo("Paste Flow Nodes");
            _pasteSerial = _pasteSerial >= 10 ? 1 : _pasteSerial + 1;
            var offset = new Vector2(28f * _pasteSerial, 28f * _pasteSerial);
            List<string> pasted = _document.PasteClipboard(payload, offset);
            if (pasted.Count == 0) return;
            SyncUndoState();
            ReloadCanvas();
            _canvas.SelectNodes(pasted);
            _selectedNodeId = pasted[0];
            _inspector.MarkDirtyRepaint();
            ScheduleValidation();
        }

        private void RebuildNodeLibrary()
        {
            _nodeList.Clear();
            var groups = new SortedDictionary<string, List<FlowNodeDescriptor>>(StringComparer.Ordinal);
            for (int i = 0; i < _registry.Descriptors.Count; i++)
            {
                FlowNodeDescriptor descriptor = _registry.Descriptors[i];
                string name = FlowKitEditorLocalization.NodeName(descriptor);
                string category = FlowKitEditorLocalization.Category(descriptor);
                if (!string.IsNullOrEmpty(_nodeSearch) &&
                    name.IndexOf(_nodeSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                    descriptor.TypeId.Value.IndexOf(_nodeSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                    category.IndexOf(_nodeSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!groups.TryGetValue(category, out List<FlowNodeDescriptor> list))
                {
                    list = new List<FlowNodeDescriptor>();
                    groups.Add(category, list);
                }
                list.Add(descriptor);
            }

            foreach (KeyValuePair<string, List<FlowNodeDescriptor>> group in groups)
            {
                var foldout = new Foldout { text = group.Key, value = true };
                for (int i = 0; i < group.Value.Count; i++)
                {
                    FlowNodeDescriptor descriptor = group.Value[i];
                    string typeId = descriptor.TypeId.Value;
                    var button = new Button(() => CreateNode(typeId)) { text = FlowKitEditorLocalization.NodeName(descriptor) };
                    button.tooltip = typeId;
                    button.style.unityTextAlign = TextAnchor.MiddleLeft;
                    FlowKitEditorTheme.ApplyButton(button);
                    foldout.Add(button);
                }
                _nodeList.Add(foldout);
            }
        }

        private void ShowNodeCreationMenu(Vector2 position)
        {
            var menu = new GenericMenu();
            for (int i = 0; i < _registry.Descriptors.Count; i++)
            {
                FlowNodeDescriptor descriptor = _registry.Descriptors[i];
                string typeId = descriptor.TypeId.Value;
                string path = FlowKitEditorLocalization.Category(descriptor) + "/" + FlowKitEditorLocalization.NodeName(descriptor);
                menu.AddItem(new GUIContent(path), false, () => CreateNode(typeId, position));
            }
            menu.ShowAsContext();
        }

        private void CreateNode(string typeId)
        {
            int count = _document == null ? 0 : _document.Graph.Nodes.Count;
            CreateNode(typeId, new Vector2(120f + (count % 5) * 28f, 100f + (count % 7) * 28f));
        }

        private void CreateNode(string typeId, Vector2 position)
        {
            if (_document == null) return;
            RecordUndo("创建 Flow 节点");
            FlowNodeData node = _document.AddNode(typeId, position);
            _registry.TryGetDescriptor(typeId, out FlowNodeDescriptor descriptor);
            _canvas.AddNodeView(node, descriptor, position);
            _canvas.FocusNode(node.Id);
            _selectedNodeId = node.Id;
            SyncUndoState();
            UpdateHeader();
            ScheduleValidation();
        }

        private void OnNodeMoved(string nodeId, Vector2 position)
        {
            if (_document == null) return;
            RecordUndo("Move Flow Node");
            _document.SetNodePosition(nodeId, position);
            SyncUndoState();
            UpdateHeader();
        }

        private void OnNodesDeleted(IReadOnlyList<string> nodeIds)
        {
            if (_document == null || nodeIds == null || nodeIds.Count == 0) return;
            RecordUndo("Delete Flow Node");
            for (int i = 0; i < nodeIds.Count; i++) _document.RemoveNode(nodeIds[i]);
            if (!string.IsNullOrEmpty(_selectedNodeId))
            {
                for (int i = 0; i < nodeIds.Count; i++) if (nodeIds[i] == _selectedNodeId) _selectedNodeId = null;
            }
            SyncUndoState();
            UpdateHeader();
            _inspector.MarkDirtyRepaint();
            ScheduleValidation();
        }

        private void OnEdgeCreated(FlowEdgeData edge)
        {
            if (_document == null || edge == null) return;
            RecordUndo("Create Flow Edge");
            _document.AddEdge(edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId);
            SyncUndoState();
            UpdateHeader();
            ScheduleValidation();
        }

        private void OnEdgeRemoved(FlowEdgeData edge)
        {
            if (_document == null || edge == null) return;
            RecordUndo("Delete Flow Edge");
            _document.RemoveEdge(edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId);
            SyncUndoState();
            UpdateHeader();
            ScheduleValidation();
        }

        private void NewDocument(bool confirm)
        {
            if (confirm && !ConfirmDiscardIfDirty()) return;
            _document = FlowGraphDocument.CreateNew(_registry);
            ResetUndoState();
            ReloadCanvas();
            _lastResult = null;
            RebuildValidation();
        }

        private void OpenDialog()
        {
            if (!ConfirmDiscardIfDirty()) return;
            string path = EditorUtility.OpenFilePanel("打开 Flow 流程", Application.dataPath, "json");
            if (!string.IsNullOrEmpty(path)) OpenPath(path, false);
        }

        internal void FrameAll() => _canvas?.FrameAll();

        internal void OpenPath(string path, bool confirm = true)
        {
            if (confirm && !ConfirmDiscardIfDirty()) return;
            try
            {
                _document = FlowGraphDocument.Load(path, _registry);
                _selectedNodeId = null;
                _lastResult = null;
                ResetUndoState();
                ReloadCanvas();
                RebuildValidation();
            }
            catch (Exception exception)
            {
                ShowNotification(exception.Message);
            }
        }

        private bool ConfirmDiscardIfDirty()
        {
            return _document == null || !_document.IsDirty ||
                EditorUtility.DisplayDialog("Flow 尚未保存", "当前流程存在未保存修改，确定放弃这些修改吗？", "放弃修改", "取消");
        }

        private void Save()
        {
            if (_document == null) return;
            if (string.IsNullOrEmpty(_document.RuntimePath))
            {
                SaveAs();
                return;
            }
            try
            {
                _document.Save();
                SyncUndoState();
                UpdateHeader();
                ShowNotification("Flow 已保存。");
            }
            catch (Exception exception)
            {
                ShowNotification(exception.Message);
            }
        }

        private void SaveAs()
        {
            if (_document == null) return;
            string defaultName = SanitizeFileName(string.IsNullOrEmpty(_document.Graph.FlowId) ? "新流程" : _document.Graph.FlowId) + ".flow";
            string assetPath = EditorUtility.SaveFilePanelInProject("保存 Flow 流程", defaultName, "json", "保存运行时 Flow JSON；编辑器布局会自动保存到对应 .flow.editor.json。");
            if (string.IsNullOrEmpty(assetPath)) return;
            _document.SetRuntimePath(Path.GetFullPath(Path.Combine(ProjectRoot, assetPath)));
            Save();
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }

        private void Validate()
        {
            if (_document == null) return;
            _lastResult = FlowCompiler.Compile(_document.Graph, _registry);
            RebuildValidation();
            UpdateHeader();
        }

        private void ValidateProject()
        {
            FlowBuildValidationResult result = FlowKitBuildValidator.ValidateProject();
            if (result.Succeeded)
            {
                ShowNotification($"项目校验通过：{result.GraphCount} 个 Flow。");
                Debug.Log($"FlowKit 项目校验通过：{result.GraphCount} 个流程。");
                return;
            }

            ShowNotification($"项目校验失败：{result.Errors.Count} 个错误，请查看 Console。");
            Debug.LogError(result.CreateSummary());
        }

        private static bool HasFireDrillSample()
        {
            return AssetDatabase.LoadAssetAtPath<TextAsset>(FireDrillSampleAssetPath) != null;
        }

        private void OpenFireDrillSample()
        {
            TextAsset sample = AssetDatabase.LoadAssetAtPath<TextAsset>(FireDrillSampleAssetPath);
            if (sample == null)
            {
                ShowNotification("当前项目未导入 FlowKit 消防样例包。");
                return;
            }

            string absolutePath = Path.GetFullPath(Path.Combine(ProjectRoot, FireDrillSampleAssetPath));
            OpenPath(absolutePath, true);
            EditorApplication.delayCall += () => _canvas?.FrameAll();
        }

        private void RebuildValidation()
        {
            _validationList.Clear();
            if (_lastResult == null)
            {
                _validationList.Add(new Label("尚未校验。完成编辑后点击顶部“校验”。"));
                return;
            }
            if (_lastResult.Issues.Count == 0)
            {
                var success = new Label($"✓ 校验通过 · 节点 {_lastResult.Plan.NodeCount} · PlanHash 0x{_lastResult.Plan.PlanHash:X16}");
                success.style.color = FlowKitEditorTheme.Success;
                success.style.marginLeft = 8f;
                success.style.marginTop = 5f;
                _validationList.Add(success);
                return;
            }
            for (int i = 0; i < _lastResult.Issues.Count; i++)
            {
                FlowValidationIssue issue = _lastResult.Issues[i];
                string severity = issue.IsError ? "错误" : "警告";
                string location = string.IsNullOrEmpty(issue.NodeId) ? string.Empty : $" · {issue.NodeId}";
                var row = new Button(() =>
                {
                    if (!string.IsNullOrEmpty(issue.NodeId)) _canvas.FocusNode(issue.NodeId);
                })
                {
                    text = $"[{severity}] {FlowKitEditorLocalization.ValidationCode(issue.Code)}{location} · {issue.Message}"
                };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.color = issue.IsError ? FlowKitEditorTheme.Error : FlowKitEditorTheme.Warning;
                FlowKitEditorTheme.ApplyButton(row);
                _validationList.Add(row);
            }
        }

        private void ReloadCanvas()
        {
            if (_document == null) return;
            _canvas.LoadDocument(_document, _registry);
            UpdateHeader();
            _inspector.MarkDirtyRepaint();
        }

        private void DrawInspector()
        {
            if (_document == null)
            {
                EditorGUILayout.HelpBox("当前没有打开 Flow 流程。", MessageType.Info);
                return;
            }

            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("流程信息", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                string flowId = EditorGUILayout.TextField("Flow ID", _document.Graph.FlowId ?? string.Empty);
                if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(flowId))
                    Mutate("修改 Flow ID", () => _document.SetFlowId(flowId));

                FlowGraphMetadata metadata = _document.Graph.Metadata ?? new FlowGraphMetadata();
                EditorGUI.BeginChangeCheck();
                string displayName = EditorGUILayout.TextField("显示名称", metadata.DisplayName ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                    Mutate("修改流程名称", () => _document.SetDisplayName(displayName));

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField("流程说明");
                string description = EditorGUILayout.TextArea(metadata.Description ?? string.Empty, GUILayout.MinHeight(42f));
                if (EditorGUI.EndChangeCheck())
                    Mutate("修改流程说明", () => _document.SetDescription(description));

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("入口节点", _document.Graph.EntryNodeId ?? string.Empty);
            }

            EditorGUILayout.Space(8f);
            FlowNodeData node = _document.FindNode(_selectedNodeId);
            if (node == null)
            {
                EditorGUILayout.HelpBox("在中间画布中选择一个节点后，可在这里编辑它的参数。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }
            if (!_registry.TryGetDescriptor(node.TypeId, out FlowNodeDescriptor descriptor))
            {
                EditorGUILayout.HelpBox("当前节点类型未注册：" + node.TypeId, MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.LabelField("节点 · " + FlowKitEditorLocalization.NodeName(descriptor), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            string nodeDisplayName = EditorGUILayout.TextField("节点名称", _document.GetNodeDisplayName(node.Id));
            if (EditorGUI.EndChangeCheck())
                Mutate("修改节点名称", () => _document.SetNodeDisplayName(node.Id, nodeDisplayName));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("节点说明");
            string nodeDescription = EditorGUILayout.TextArea(_document.GetNodeDescription(node.Id), GUILayout.MinHeight(42f));
            if (EditorGUI.EndChangeCheck())
                Mutate("修改节点说明", () => _document.SetNodeDescription(node.Id, nodeDescription));
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("分类", FlowKitEditorLocalization.Category(descriptor));
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("节点 ID", node.Id);
                    EditorGUILayout.TextField("类型 ID", node.TypeId);
                    EditorGUILayout.IntField("定义版本", node.DefinitionVersion);
                }
                if (_document.Graph.EntryNodeId == node.Id)
                    EditorGUILayout.HelpBox("该节点是当前流程入口。", MessageType.Info);
                else if (GUILayout.Button("设为流程入口"))
                    Mutate("设置流程入口", () => _document.SetEntryNode(node.Id));
                DrawPortSummary(descriptor);
            }

            if (descriptor.RequiresCondition)
                DrawConditionSection(node);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("节点参数", EditorStyles.boldLabel);
            for (int i = 0; i < descriptor.Properties.Count; i++)
                DrawProperty(node, descriptor.Properties[i]);

            DrawAdditionalArguments(node, descriptor);
            EditorGUILayout.Space(12f);
            GUI.backgroundColor = new Color(0.75f, 0.28f, 0.28f);
            if (GUILayout.Button("删除节点", GUILayout.Height(28f)) &&
                EditorUtility.DisplayDialog("删除 Flow 节点", $"确定删除“{FlowKitEditorLocalization.NodeName(descriptor)}”吗？\n相关连线也会一并删除。", "删除", "取消"))
            {
                string id = node.Id;
                Mutate("删除 Flow 节点", () => _document.RemoveNode(id));
                _selectedNodeId = null;
                ReloadCanvas();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();
        }

        private static void DrawPortSummary(FlowNodeDescriptor descriptor)
        {
            if (descriptor?.Ports == null || descriptor.Ports.Count == 0) return;
            var inputs = new List<string>();
            var outputs = new List<string>();
            for (int i = 0; i < descriptor.Ports.Count; i++)
            {
                FlowPortDescriptor port = descriptor.Ports[i];
                string label = FlowKitEditorLocalization.PortName(port.Id) + " (" + port.Id + ")";
                if (port.Direction == FlowPortDirection.Input) inputs.Add(label);
                else outputs.Add(label);
            }
            if (inputs.Count > 0) EditorGUILayout.LabelField("输入端口", string.Join("、", inputs));
            if (outputs.Count > 0) EditorGUILayout.LabelField("输出端口", string.Join("、", outputs));
        }

        private void DrawConditionSection(FlowNodeData node)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("条件表达式", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("条件使用类型化 AST 保存，不执行字符串表达式。可读取 Blackboard 或 State。", MessageType.None);

            if (node.Condition == null)
            {
                if (GUILayout.Button("创建条件"))
                {
                    Mutate("创建 Flow 条件", () =>
                        _document.SetCondition(node.Id, CreateDefaultCondition(FlowConditionKind.Compare)));
                }
                return;
            }

            FlowCondition edited = CloneCondition(node.Condition);
            bool structuralChanged = false;
            EditorGUI.BeginChangeCheck();
            edited = DrawConditionEditor(edited, 0, ref structuralChanged);
            bool valueChanged = EditorGUI.EndChangeCheck();
            if (valueChanged || structuralChanged)
                Mutate("修改 Flow 条件", () => _document.SetCondition(node.Id, edited));

            if (GUILayout.Button("重置条件"))
                Mutate("重置 Flow 条件", () =>
                    _document.SetCondition(node.Id, CreateDefaultCondition(FlowConditionKind.Compare)));
        }

        private FlowCondition DrawConditionEditor(FlowCondition condition, int depth, ref bool structuralChanged)
        {
            if (condition == null) condition = FlowCondition.FromBool(false);
            if (depth > 8)
            {
                EditorGUILayout.HelpBox("条件嵌套深度超过 8 层，请拆分条件。", MessageType.Warning);
                return condition;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                FlowConditionKind newKind = DrawConditionKindPopup("类型", condition.Kind);
                if (newKind != condition.Kind)
                {
                    condition = CreateDefaultCondition(newKind);
                    structuralChanged = true;
                }

                switch (condition.Kind)
                {
                    case FlowConditionKind.Constant:
                        FlowValue constant = condition.Constant.Kind == FlowValueKind.None
                            ? FlowValue.FromBool(false) : condition.Constant;
                        condition.Constant = DrawValueEditor("值", constant, FlowValueKind.Any, null, null);
                        break;
                    case FlowConditionKind.Blackboard:
                        condition.Key = DrawConditionKey("黑板键", "key", condition.Key);
                        break;
                    case FlowConditionKind.State:
                        condition.Key = DrawConditionKey("状态 ID", "state", condition.Key);
                        break;
                    case FlowConditionKind.Compare:
                        condition.Operator = DrawComparisonOperatorPopup("比较", condition.Operator);
                        EditorGUILayout.LabelField("左值", EditorStyles.miniBoldLabel);
                        condition.Left = DrawConditionEditor(condition.Left, depth + 1, ref structuralChanged);
                        EditorGUILayout.LabelField("右值", EditorStyles.miniBoldLabel);
                        condition.Right = DrawConditionEditor(condition.Right, depth + 1, ref structuralChanged);
                        break;
                    case FlowConditionKind.Not:
                        EditorGUILayout.LabelField("取反条件", EditorStyles.miniBoldLabel);
                        condition.Left = DrawConditionEditor(condition.Left, depth + 1, ref structuralChanged);
                        break;
                    case FlowConditionKind.All:
                    case FlowConditionKind.Any:
                        DrawConditionChildren(condition, depth, ref structuralChanged);
                        break;
                }
            }
            return condition;
        }

        private string DrawConditionKey(string label, string semanticKey, string current)
        {
            string value = EditorGUILayout.TextField(label, current ?? string.Empty);
            return DrawKnownIdPicker(semanticKey, value);
        }

        private void DrawConditionChildren(FlowCondition condition, int depth, ref bool structuralChanged)
        {
            if (condition.Children == null) condition.Children = new List<FlowCondition>();
            string title = condition.Kind == FlowConditionKind.All ? "全部满足（AND）" : "任意满足（OR）";
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            for (int i = 0; i < condition.Children.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("条件 " + (i + 1), GUILayout.Width(70f));
                using (new EditorGUI.DisabledScope(condition.Children.Count <= 1))
                {
                    if (GUILayout.Button("移除", GUILayout.Width(52f)))
                    {
                        condition.Children.RemoveAt(i);
                        structuralChanged = true;
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();
                condition.Children[i] = DrawConditionEditor(condition.Children[i], depth + 1, ref structuralChanged);
            }
            if (GUILayout.Button("+ 添加子条件"))
            {
                condition.Children.Add(FlowCondition.FromBool(false));
                structuralChanged = true;
            }
        }

        private static FlowConditionKind DrawConditionKindPopup(string label, FlowConditionKind current)
        {
            FlowConditionKind[] values = (FlowConditionKind[])Enum.GetValues(typeof(FlowConditionKind));
            string[] names = new string[values.Length];
            int index = 0;
            for (int i = 0; i < values.Length; i++)
            {
                names[i] = FlowKitEditorLocalization.ConditionKind(values[i]);
                if (values[i] == current) index = i;
            }
            return values[EditorGUILayout.Popup(label, index, names)];
        }

        private static FlowComparisonOperator DrawComparisonOperatorPopup(string label, FlowComparisonOperator current)
        {
            FlowComparisonOperator[] values = (FlowComparisonOperator[])Enum.GetValues(typeof(FlowComparisonOperator));
            string[] names = new string[values.Length];
            int index = 0;
            for (int i = 0; i < values.Length; i++)
            {
                names[i] = FlowKitEditorLocalization.ComparisonOperator(values[i]);
                if (values[i] == current) index = i;
            }
            return values[EditorGUILayout.Popup(label, index, names)];
        }

        private static FlowCondition CreateDefaultCondition(FlowConditionKind kind)
        {
            switch (kind)
            {
                case FlowConditionKind.Constant:
                    return FlowCondition.FromBool(false);
                case FlowConditionKind.Blackboard:
                    return FlowCondition.BlackboardValue(string.Empty);
                case FlowConditionKind.State:
                    return FlowCondition.StateValue(string.Empty);
                case FlowConditionKind.Compare:
                    return FlowCondition.Compare(
                        FlowCondition.BlackboardValue(string.Empty),
                        FlowComparisonOperator.Equal,
                        FlowCondition.FromBool(true));
                case FlowConditionKind.Not:
                    return new FlowCondition { Kind = FlowConditionKind.Not, Left = FlowCondition.FromBool(false) };
                case FlowConditionKind.All:
                case FlowConditionKind.Any:
                    return new FlowCondition
                    {
                        Kind = kind,
                        Children = new List<FlowCondition> { FlowCondition.FromBool(false) }
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static FlowCondition CloneCondition(FlowCondition source)
        {
            if (source == null) return null;
            var clone = new FlowCondition
            {
                Kind = source.Kind,
                Constant = source.Constant,
                Key = source.Key,
                Operator = source.Operator,
                Left = CloneCondition(source.Left),
                Right = CloneCondition(source.Right)
            };
            if (source.Children != null)
                for (int i = 0; i < source.Children.Count; i++)
                    clone.Children.Add(CloneCondition(source.Children[i]));
            return clone;
        }

        private void DrawProperty(FlowNodeData node, FlowPropertyDescriptor property)
        {
            bool exists = node.Parameters.TryGet(property.Key, out FlowValue current);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            string displayName = FlowKitEditorLocalization.PropertyName(property);
            EditorGUILayout.LabelField(displayName + (property.Required ? " *" : string.Empty), EditorStyles.boldLabel);
            if (exists && !property.Required && GUILayout.Button("移除", GUILayout.Width(58f)))
            {
                string key = property.Key;
                Mutate("移除 Flow 参数", () => _document.RemoveParameter(node.Id, key));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (!exists)
            {
                EditorGUILayout.LabelField("参数键", property.Key, EditorStyles.miniLabel);
                if (GUILayout.Button("添加参数"))
                {
                    FlowValue initial = CreateDefaultValue(property.ValueKind, property.AllowedStringValues);
                    Mutate("添加 Flow 参数", () => _document.SetParameter(node.Id, property.Key, initial));
                }
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField($"{property.Key} · {FlowKitEditorLocalization.ValueKind(property.ValueKind)}", EditorStyles.miniLabel);
            FlowValue edited = DrawValueEditor(displayName, current, property.ValueKind, property.AllowedStringValues, property.Key);
            if (!edited.Equals(current))
            {
                string key = property.Key;
                Mutate("修改 Flow 参数", () => _document.SetParameter(node.Id, key, edited));
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawAdditionalArguments(FlowNodeData node, FlowNodeDescriptor descriptor)
        {
            if (!descriptor.AllowAdditionalProperties) return;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("外部操作参数", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("除 operation ID 外，其余参数会原样通过 FlowOperationRequest 交给 Adapter。", MessageType.None);
            if (node.Parameters.Entries != null)
            {
                for (int i = 0; i < node.Parameters.Entries.Count; i++)
                {
                    FlowPropertyEntry entry = node.Parameters.Entries[i];
                    if (descriptor.TryGetProperty(entry.Key, out _)) continue;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.Key, EditorStyles.boldLabel);
                    if (GUILayout.Button("移除", GUILayout.Width(58f)))
                    {
                        string key = entry.Key;
                        Mutate("移除外部操作参数", () => _document.RemoveParameter(node.Id, key));
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        return;
                    }
                    EditorGUILayout.EndHorizontal();
                    FlowValue edited = DrawValueEditor(entry.Key, entry.Value, entry.Value.Kind, null, entry.Key);
                    if (!edited.Equals(entry.Value))
                    {
                        string key = entry.Key;
                        Mutate("修改外部操作参数", () => _document.SetParameter(node.Id, key, edited));
                    }
                    EditorGUILayout.EndVertical();
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("新增参数", EditorStyles.boldLabel);
                _newArgumentKey = EditorGUILayout.TextField("参数键", _newArgumentKey);
                _newArgumentKind = DrawValueKindPopup("值类型", _newArgumentKind);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newArgumentKey)))
                {
                    if (GUILayout.Button("添加"))
                    {
                        string key = _newArgumentKey.Trim();
                        if (descriptor.TryGetProperty(key, out _) || node.Parameters.TryGet(key, out _))
                        {
                            ShowNotification("参数键已存在：" + key);
                        }
                        else
                        {
                            FlowValue initial = CreateDefaultValue(_newArgumentKind, null);
                            Mutate("添加外部操作参数", () => _document.SetParameter(node.Id, key, initial));
                            _newArgumentKey = string.Empty;
                        }
                    }
                }
            }
        }

        private FlowValue DrawValueEditor(
            string label,
            FlowValue value,
            FlowValueKind schemaKind,
            IReadOnlyList<string> allowed,
            string semanticKey)
        {
            FlowValueKind kind = schemaKind == FlowValueKind.Any ? value.Kind : schemaKind;
            if (schemaKind == FlowValueKind.Any)
            {
                if (!IsAuthorableKind(kind))
                {
                    EditorGUILayout.HelpBox("当前值来自旧版/运行时类型，不能直接写回 Graph。请先移除该参数再重新添加。", MessageType.Warning);
                    return value;
                }
                FlowValueKind selected = DrawValueKindPopup("值类型", kind);
                if (selected != kind) return CreateDefaultValue(selected, null);
                kind = selected;
            }

            switch (kind)
            {
                case FlowValueKind.Bool:
                    return FlowValue.FromBool(EditorGUILayout.Toggle("值", value.BoolValue));
                case FlowValueKind.Int:
                    return FlowValue.FromInt(EditorGUILayout.IntField("值", value.IntValue));
                case FlowValueKind.Long:
                {
                    string text = EditorGUILayout.TextField("值", value.LongValue.ToString());
                    return long.TryParse(text, out long parsed) ? FlowValue.FromLong(parsed) : value;
                }
                case FlowValueKind.Float:
                    return FlowValue.FromFloat(EditorGUILayout.FloatField("值", value.FloatValue));
                case FlowValueKind.Double:
                {
                    string text = EditorGUILayout.TextField("值", value.DoubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    return double.TryParse(text, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double parsed)
                        ? FlowValue.FromDouble(parsed)
                        : value;
                }
                case FlowValueKind.String:
                {
                    if (allowed != null && allowed.Count > 0)
                        return FlowValue.FromString(DrawAllowedStringPopup(value.StringValue, allowed));
                    string text = EditorGUILayout.TextField("值", value.StringValue ?? string.Empty);
                    return FlowValue.FromString(DrawKnownIdPicker(semanticKey, text));
                }
                case FlowValueKind.Vector2:
                {
                    Vector2 v = EditorGUILayout.Vector2Field("值", new Vector2(value.Vector2Value.X, value.Vector2Value.Y));
                    return FlowValue.FromVector2(new FlowVector2(v.x, v.y));
                }
                case FlowValueKind.Vector3:
                {
                    Vector3 v = EditorGUILayout.Vector3Field("值", new Vector3(value.Vector3Value.X, value.Vector3Value.Y, value.Vector3Value.Z));
                    return FlowValue.FromVector3(new FlowVector3(v.x, v.y, v.z));
                }
                case FlowValueKind.BindingReference:
                    return FlowValue.FromBindingReference(DrawBindingReference(value.BindingReferenceValue.Id));
                case FlowValueKind.Asset:
                    return FlowValue.FromAsset(DrawAssetReference(value.AssetValue.Id));
                case FlowValueKind.Enum:
                    return FlowValue.FromEnum(EditorGUILayout.TextField("枚举 ID", value.EnumValue.Id ?? string.Empty));
                case FlowValueKind.None:
                    EditorGUILayout.LabelField("值", "无");
                    return FlowValue.None;
                default:
                    EditorGUILayout.HelpBox("当前值类型不支持在 Graph 中编辑：" + kind, MessageType.Warning);
                    return value;
            }
        }

        private static FlowValueKind DrawValueKindPopup(string label, FlowValueKind current)
        {
            int index = 0;
            string[] names = new string[AuthorableKinds.Length];
            for (int i = 0; i < AuthorableKinds.Length; i++)
            {
                names[i] = FlowKitEditorLocalization.ValueKind(AuthorableKinds[i]);
                if (AuthorableKinds[i] == current) index = i;
            }
            return AuthorableKinds[EditorGUILayout.Popup(label, index, names)];
        }

        private static bool IsAuthorableKind(FlowValueKind kind)
        {
            for (int i = 0; i < AuthorableKinds.Length; i++)
                if (AuthorableKinds[i] == kind) return true;
            return false;
        }

        private static string DrawAllowedStringPopup(string current, IReadOnlyList<string> allowed)
        {
            int index = 0;
            string[] labels = new string[allowed.Count];
            for (int i = 0; i < allowed.Count; i++)
            {
                labels[i] = FlowKitEditorLocalization.EnumValue(allowed[i]);
                if (string.Equals(allowed[i], current, StringComparison.Ordinal)) index = i;
            }
            return allowed[EditorGUILayout.Popup("值", index, labels)];
        }

        private string DrawKnownIdPicker(string semanticKey, string current)
        {
            List<string> known = CollectKnownStringIds(semanticKey);
            if (known.Count == 0) return current;
            string[] options = new string[known.Count + 1];
            options[0] = "— 从当前流程已使用 ID 中选择 —";
            for (int i = 0; i < known.Count; i++) options[i + 1] = known[i];
            int selected = EditorGUILayout.Popup("快速选择", 0, options);
            return selected > 0 ? known[selected - 1] : current;
        }

        private List<string> CollectKnownStringIds(string semanticKey)
        {
            return FlowAuthoringCatalogIndex.Collect(semanticKey, _document?.Graph);
        }


        private string DrawBindingReference(string current)
        {
            string result = EditorGUILayout.TextField("绑定 ID", current ?? string.Empty);
            var ids = new SortedSet<string>(FlowAuthoringCatalogIndex.CollectBindings(), StringComparer.Ordinal);
            FlowBinding[] bindings = Resources.FindObjectsOfTypeAll<FlowBinding>();
            for (int i = 0; i < bindings.Length; i++)
            {
                FlowBinding binding = bindings[i];
                if (binding == null || EditorUtility.IsPersistent(binding) || !binding.gameObject.scene.IsValid()) continue;
                if (binding.BindingId.IsValid) ids.Add(binding.BindingId.Value);
            }
            if (ids.Count > 0)
            {
                string[] options = new string[ids.Count + 1];
                options[0] = "— 从当前场景 FlowBinding 选择 —";
                int index = 1;
                foreach (string id in ids) options[index++] = id;
                int selected = EditorGUILayout.Popup("场景绑定", 0, options);
                if (selected > 0) result = options[selected];
            }
            if (!string.IsNullOrEmpty(result) && GUILayout.Button("在 Hierarchy 中定位绑定"))
            {
                for (int i = 0; i < bindings.Length; i++)
                {
                    FlowBinding binding = bindings[i];
                    if (binding != null && binding.BindingId.Value == result && !EditorUtility.IsPersistent(binding))
                    {
                        Selection.activeObject = binding.gameObject;
                        EditorGUIUtility.PingObject(binding.gameObject);
                        break;
                    }
                }
            }
            return result;
        }

        private static string DrawAssetReference(string current)
        {
            string result = EditorGUILayout.TextField("资源 ID", current ?? string.Empty);
            UnityEngine.Object selected = Selection.activeObject;
            string selectedPath = selected == null ? string.Empty : AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(selectedPath) && GUILayout.Button("使用 Project 当前选中资源的 GUID"))
                result = AssetDatabase.AssetPathToGUID(selectedPath);
            if (!string.IsNullOrEmpty(result))
            {
                string path = AssetDatabase.GUIDToAssetPath(result);
                if (!string.IsNullOrEmpty(path) && GUILayout.Button("在 Project 中定位该 GUID"))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                }
            }
            return result;
        }

        private static FlowValue CreateDefaultValue(FlowValueKind kind, IReadOnlyList<string> allowed)
        {
            switch (kind)
            {
                case FlowValueKind.Bool: return FlowValue.FromBool(false);
                case FlowValueKind.Int: return FlowValue.FromInt(0);
                case FlowValueKind.Long: return FlowValue.FromLong(0L);
                case FlowValueKind.Float: return FlowValue.FromFloat(0f);
                case FlowValueKind.Double: return FlowValue.FromDouble(0d);
                case FlowValueKind.String: return FlowValue.FromString(allowed != null && allowed.Count > 0 ? allowed[0] : string.Empty);
                case FlowValueKind.Vector2: return FlowValue.FromVector2(default(FlowVector2));
                case FlowValueKind.Vector3: return FlowValue.FromVector3(default(FlowVector3));
                case FlowValueKind.BindingReference: return FlowValue.FromBindingReference(string.Empty);
                case FlowValueKind.Asset: return FlowValue.FromAsset(string.Empty);
                case FlowValueKind.Enum: return FlowValue.FromEnum(string.Empty);
                case FlowValueKind.Any: return FlowValue.FromString(string.Empty);
                default: return FlowValue.None;
            }
        }

        private void Mutate(string undoName, Action mutation)
        {
            RecordUndo(undoName);
            mutation();
            SyncUndoState();
            UpdateHeader();
            _canvas.RefreshNodePresentation(_document, _selectedNodeId);
            _inspector.MarkDirtyRepaint();
            ScheduleValidation();
        }

        private void ResetUndoState()
        {
            if (_undoState != null) UnityEngine.Object.DestroyImmediate(_undoState);
            _undoState = ScriptableObject.CreateInstance<FlowEditorUndoState>();
            _undoState.hideFlags = HideFlags.HideAndDontSave;
            SyncUndoState();
            Undo.ClearUndo(_undoState);
        }

        private void RecordUndo(string name)
        {
            if (_undoState == null) ResetUndoState();
            SyncUndoState();
            Undo.RecordObject(_undoState, name);
        }

        private void SyncUndoState()
        {
            if (_undoState == null || _document == null) return;
            _undoState.GraphJson = _document.SerializeGraph();
            _undoState.MetadataJson = _document.SerializeMetadata();
            _undoState.RuntimePath = _document.RuntimePath;
            EditorUtility.SetDirty(_undoState);
        }

        private void OnUndoRedo()
        {
            if (_undoState == null || _document == null || string.IsNullOrEmpty(_undoState.GraphJson)) return;
            _document.RestoreSnapshots(_undoState.GraphJson, _undoState.MetadataJson);
            _document.SetRuntimePath(_undoState.RuntimePath);
            _selectedNodeId = null;
            ReloadCanvas();
            Validate();
        }

        private void UpdateHeader()
        {
            if (_document == null) return;
            string name = string.IsNullOrEmpty(_document.RuntimePath) ? "未命名流程" : Path.GetFileName(_document.RuntimePath);
            _fileLabel.text = (_document.IsDirty ? "● 未保存  " : string.Empty) + name;
            _fileLabel.style.color = _document.IsDirty ? FlowKitEditorTheme.Warning : FlowKitEditorTheme.TextPrimary;
            if (_lastResult == null)
            {
                _statusLabel.text = $"节点 {_document.Graph.Nodes.Count}  ·  连线 {_document.Graph.Edges.Count}";
                _statusLabel.style.color = FlowKitEditorTheme.TextSecondary;
            }
            else if (_lastResult.Succeeded)
            {
                _statusLabel.text = $"✓ 已通过  ·  0x{_lastResult.Plan.PlanHash:X16}";
                _statusLabel.style.color = FlowKitEditorTheme.Success;
            }
            else
            {
                _statusLabel.text = $"✕ 未通过  ·  {_lastResult.Issues.Count} 项问题";
                _statusLabel.style.color = FlowKitEditorTheme.Error;
            }
        }

        private static void ShowNotification(string message)
        {
            EditorWindow.focusedWindow?.ShowNotification(new GUIContent(message));
        }
    }
}
