using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using StellarFramework.ActionEngine;

namespace StellarFramework.Editor
{
    public class ActionEngineEditorWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<ActionEngineEditorWindow>("Action Engine");
            window.minSize = new Vector2(1200, 700);
            window.Show();
        }

        private ActionEngineAsset _activeAsset;
        private GameObject _rootTarget;

        private Vector2 _panOffset = new Vector2(50, 50);
        private float _zoom = 1.0f;
        private const float MIN_ZOOM = 0.2f;
        private const float MAX_ZOOM = 3.0f;

        private ActionNodeData _selectedNode;
        private ActionNodeData _selectedNodeParent;

        private ActionNodeData _draggingNode;
        private Vector2 _dragOffset;
        private ActionNodeData _relinkingNode;

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private CancellationTokenSource _playbackCts;

        private bool _stylesInitialized;
        private GUIStyle _headerStyle;
        private GUIStyle _nodeStyle;
        private GUIStyle _nodeSelectedStyle;
        private GUIStyle _dropBoxStyle;

        private int _maxFlowDepth = 1;
        private Dictionary<ActionNodeData, string> _nodePaths = new Dictionary<ActionNodeData, string>();

        #region 组件驱动注册表

        private struct ActionUIConfig
        {
            public Type StrategyType;
            public bool ShowVector;
            public bool ShowColor;
            public bool ShowFloat;
            public bool ShowBool;
            public bool ShowAxis;
            public bool IsInstant;
        }

        private readonly Dictionary<string, Dictionary<string, ActionUIConfig>> _actionRegistry =
            new Dictionary<string, Dictionary<string, ActionUIConfig>>()
            {
                {
                    "GameObject", new Dictionary<string, ActionUIConfig>()
                    {
                        {
                            "IsActive",
                            new ActionUIConfig
                                { StrategyType = typeof(GameObjectActiveStrategy), ShowBool = true, IsInstant = true }
                        }
                    }
                },
                {
                    "Transform", new Dictionary<string, ActionUIConfig>()
                    {
                        {
                            "LocalPosition",
                            new ActionUIConfig
                                { StrategyType = typeof(LocalMoveStrategy), ShowVector = true, ShowAxis = true }
                        },
                        {
                            "LocalRotation",
                            new ActionUIConfig { StrategyType = typeof(LocalRotateStrategy), ShowVector = true }
                        },
                        {
                            "LocalScale",
                            new ActionUIConfig
                                { StrategyType = typeof(ScaleStrategy), ShowVector = true, ShowAxis = true }
                        }
                    }
                },
                {
                    "CanvasGroup", new Dictionary<string, ActionUIConfig>()
                    {
                        { "Alpha", new ActionUIConfig { StrategyType = typeof(CanvasFadeStrategy), ShowFloat = true } }
                    }
                },
                {
                    "Image", new Dictionary<string, ActionUIConfig>()
                    {
                        { "Color", new ActionUIConfig { StrategyType = typeof(ImageColorStrategy), ShowColor = true } }
                    }
                }
            };

        private string[] _componentNames;

        #endregion

        private void OnEnable()
        {
            _componentNames = new[] { "无 (纯容器)" }.Concat(_actionRegistry.Keys).ToArray();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            StopPlayback();
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (EditorWindow.focusedWindow == this || EditorWindow.mouseOverWindow == this) Repaint();
            if (_playbackCts != null && !Application.isPlaying) SceneView.RepaintAll();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 12, normal = { textColor = new Color(0.4f, 0.8f, 1f) } };
            _nodeStyle = new GUIStyle("window")
            {
                padding = new RectOffset(10, 10, 10, 10), alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold
            };
            _nodeSelectedStyle = new GUIStyle("window")
            {
                normal = { textColor = Color.cyan }, padding = new RectOffset(10, 10, 10, 10),
                alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold
            };
            _dropBoxStyle = new GUIStyle("HelpBox")
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = new Color(0.6f, 0.8f, 1f) }
            };
            _stylesInitialized = true;
        }

        private void BuildNodePaths(ActionNodeData node, string path, int depth = 0)
        {
            if (node == null || depth > 100) return;
            _nodePaths[node] = path;
            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                    BuildNodePaths(node.Children[i], $"{path}.Children.Array.data[{i}]", depth + 1);
            }
        }

        private void OnGUI()
        {
            InitStyles();
            DrawTopBar();

            if (_activeAsset == null)
            {
                DrawEmptyState();
                return;
            }

            if (_activeAsset.RootNode == null) _activeAsset.RootNode = new ActionNodeData { NodeName = "Root" };
            if (_activeAsset.RootNode.Children == null) _activeAsset.RootNode.Children = new List<ActionNodeData>();

            _nodePaths.Clear();
            BuildNodePaths(_activeAsset.RootNode, "RootNode");

            EditorGUILayout.BeginHorizontal();
            {
                DrawLeftHierarchy();
                DrawCenterNodeGraph();
                DrawRightInspector();
            }
            EditorGUILayout.EndHorizontal();

            DrawBottomBar();
        }

        #region 顶部与左侧

        private void DrawTopBar()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(25)))
            {
                GUILayout.Label("当前资产:", EditorStyles.miniLabel, GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                _activeAsset = (ActionEngineAsset)EditorGUILayout.ObjectField(_activeAsset, typeof(ActionEngineAsset),
                    false, GUILayout.Width(250));
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedNode = null;
                    _selectedNodeParent = null;
                    _panOffset = new Vector2(50, 50);
                    _zoom = 1.0f;
                    AutoBindTarget();
                    ActionEngineRunner.InitSnapshot(_rootTarget, _activeAsset, true);
                }

                if (GUILayout.Button("新建资产", EditorStyles.toolbarButton, GUILayout.Width(80))) CreateNewAsset();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"缩放: {(_zoom * 100):F0}%", EditorStyles.miniLabel);
                if (GUILayout.Button("重置视图", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    _panOffset = new Vector2(50, 50);
                    _zoom = 1.0f;
                }

                if (GUILayout.Button("保存修改", EditorStyles.toolbarButton, GUILayout.Width(80))) SaveAsset();
            }
        }

        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建动作资产", "NewActionAsset", "asset", "");
            if (string.IsNullOrEmpty(path)) return;
            var asset = ScriptableObject.CreateInstance<ActionEngineAsset>();
            asset.RootNode = new ActionNodeData { NodeName = "Root", Children = new List<ActionNodeData>() };
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _activeAsset = asset;
        }

        private void SaveAsset()
        {
            if (_activeAsset != null)
            {
                EditorUtility.SetDirty(_activeAsset);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("资产已保存"));
            }
        }

        private void DrawLeftHierarchy()
        {
            using (new GUILayout.VerticalScope("box", GUILayout.Width(220), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label("大纲 (Hierarchy)", _headerStyle);
                GUILayout.Space(5);
                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                DrawHierarchyNode(_activeAsset.RootNode, null, 0);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawHierarchyNode(ActionNodeData node, ActionNodeData parentNode, int indentLevel, int depth = 0)
        {
            if (node == null || depth > 100) return;

            EditorGUI.indentLevel = indentLevel;
            bool isSelected = _selectedNode == node;

            string icon = node.Children != null && node.Children.Count > 0 ? (node.IsExpanded ? "▼ " : "▶ ") : "  - ";
            string label = $"{icon}{node.NodeName}";

            Rect rect = EditorGUILayout.GetControlRect(false, 18);
            if (isSelected) EditorGUI.DrawRect(rect, new Color(0.2f, 0.5f, 0.8f, 0.5f));

            if (GUI.Button(rect, label, EditorStyles.label))
            {
                _selectedNode = node;
                _selectedNodeParent = parentNode;
                // 双击折叠/展开
                if (UnityEngine.Event.current.clickCount == 2)
                {
                    node.IsExpanded = !node.IsExpanded;
                    EditorUtility.SetDirty(_activeAsset);
                }

                GUI.FocusControl(null);
            }

            if (node.IsExpanded && node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (child != null) DrawHierarchyNode(child, node, indentLevel + 1, depth + 1);
                }
            }
        }

        #endregion

        #region 中间节点连线图

        private void DrawCenterNodeGraph()
        {
            Rect graphRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.Box(graphRect, "", "CurveEditorBackground");

            UnityEngine.Event e = UnityEngine.Event.current;

            if (graphRect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
            {
                Vector2 mousePos = e.mousePosition;
                float zoomDelta = -e.delta.y * 0.05f;
                float newZoom = Mathf.Clamp(_zoom + zoomDelta, MIN_ZOOM, MAX_ZOOM);

                Vector2 mouseInWorld = (mousePos - graphRect.position) / _zoom - _panOffset;
                _panOffset = (mousePos - graphRect.position) / newZoom - mouseInWorld;

                _zoom = newZoom;
                e.Use();
                Repaint();
            }

            GUI.BeginClip(graphRect);

            Matrix4x4 oldMatrix = GUI.matrix;
            Matrix4x4 oldHandlesMatrix = Handles.matrix;

            Matrix4x4 scaleMatrix = Matrix4x4.Scale(new Vector3(_zoom, _zoom, 1f));
            GUI.matrix = scaleMatrix * GUI.matrix;
            Handles.matrix = GUI.matrix;

            Vector2 scaledMousePos = e.mousePosition / _zoom;

            Rect visibleRect = new Rect(0, 0, graphRect.width / _zoom, graphRect.height / _zoom);
            DrawGrid(visibleRect);

            _maxFlowDepth = 1;

            DrawConnections(_activeAsset.RootNode, 0);
            DrawGraphNode(_activeAsset.RootNode, null, scaledMousePos);

            if (_relinkingNode != null)
            {
                Rect sourcePort = GetPortRect(_relinkingNode);
                Vector3 startPos = sourcePort.center;
                Vector3 endPos = scaledMousePos;

                Handles.DrawDottedLine(startPos, endPos, 4f);

                if (FindHoveredNode(_activeAsset.RootNode, null, endPos, out var targetNode, out var targetParent))
                {
                    if (targetNode != _relinkingNode && !IsAncestor(_relinkingNode, targetNode))
                    {
                        Rect r = GetNodeRect(targetNode);
                        Handles.DrawSolidRectangleWithOutline(r, new Color(0.2f, 1f, 0.2f, 0.2f), Color.green);
                        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                            { normal = { textColor = Color.green } };
                        GUI.Label(new Rect(r.x, r.y - 20, 300, 20), $"将作为子节点添加至 [{targetNode.NodeName}]", labelStyle);
                    }
                    else
                    {
                        Rect r = GetNodeRect(targetNode);
                        Handles.DrawSolidRectangleWithOutline(r, new Color(1f, 0.2f, 0.2f, 0.2f), Color.red);
                    }
                }
            }

            HandleGraphEvents(graphRect, scaledMousePos);

            GUI.matrix = oldMatrix;
            Handles.matrix = oldHandlesMatrix;
            GUI.EndClip();
        }

        private void DrawGrid(Rect visibleRect)
        {
            float gridSpacing = 50f;
            Handles.color = new Color(1, 1, 1, 0.05f);
            Vector3 offset = new Vector3(_panOffset.x % gridSpacing, _panOffset.y % gridSpacing, 0);

            int xCount = Mathf.CeilToInt(visibleRect.width / gridSpacing) + 2;
            int yCount = Mathf.CeilToInt(visibleRect.height / gridSpacing) + 2;

            for (int i = -1; i < xCount; i++)
            {
                float x = gridSpacing * i + offset.x;
                Handles.DrawLine(new Vector3(x, -gridSpacing, 0), new Vector3(x, visibleRect.height + gridSpacing, 0));
            }

            for (int j = -1; j < yCount; j++)
            {
                float y = gridSpacing * j + offset.y;
                Handles.DrawLine(new Vector3(-gridSpacing, y, 0), new Vector3(visibleRect.width + gridSpacing, y, 0));
            }

            Handles.color = Color.white;
        }

        private int DrawConnections(ActionNodeData node, int currentDepth, int recursiveDepth = 0)
        {
            if (node == null || !node.IsExpanded || node.Children == null || node.Children.Count == 0 ||
                recursiveDepth > 100) return currentDepth;

            Rect parentRect = GetNodeRect(node);
            Rect portRect = GetPortRect(node);
            Vector3 parentOut = portRect.center;

            int localDepth = currentDepth + 1;
            if (localDepth > _maxFlowDepth) _maxFlowDepth = localDepth;

            foreach (var child in node.Children)
            {
                if (child == null) continue;
                Rect childRect = GetNodeRect(child);
                Vector3 targetIn = new Vector3(childRect.xMin, childRect.center.y, 0);

                DrawAnimatedBezier(parentOut, targetIn, new Color(0.4f, 0.8f, 1f), localDepth - 1);

                int subDepth = DrawConnections(child, localDepth, recursiveDepth + 1);
                if (subDepth > _maxFlowDepth) _maxFlowDepth = subDepth;
            }

            return localDepth;
        }

        private void DrawAnimatedBezier(Vector3 p0, Vector3 p3, Color color, int depth)
        {
            float distance = Vector3.Distance(p0, p3);
            Vector3 p1 = p0 + Vector3.right * (distance * 0.4f);
            Vector3 p2 = p3 + Vector3.left * (distance * 0.4f);

            Handles.DrawBezier(p0, p3, p1, p2, color, null, 2f);

            float globalTime = (float)(EditorApplication.timeSinceStartup * 1.5f);
            float cycleLength = _maxFlowDepth + 1f;
            float localTime = (globalTime % cycleLength) - depth;

            if (localTime >= 0f && localTime <= 1f)
            {
                // 绘制流动箭头
                float t = localTime;
                float u = 1f - t;
                float tt = t * t;
                float uu = u * u;
                float uuu = uu * u;
                float ttt = tt * t;

                Vector3 p = uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
                Vector3 tangent = 3 * uu * (p1 - p0) + 6 * u * t * p2 - 6 * u * t * p1 + 3 * tt * (p3 - p2);

                if (tangent.sqrMagnitude > 0.001f)
                {
                    tangent.Normalize();
                    Vector3 right = new Vector3(-tangent.y, tangent.x, 0) * 5f;
                    Vector3 back = -tangent * 10f;
                    Vector3 tip = p + tangent * 5f;
                    Vector3 leftCorner = p + back + right;
                    Vector3 rightCorner = p + back - right;

                    Color oldColor = Handles.color;
                    Handles.color = color * 1.8f;
                    Handles.DrawAAConvexPolygon(tip, leftCorner, rightCorner);
                    Handles.color = oldColor;
                }
            }
        }

        private void DrawGraphNode(ActionNodeData node, ActionNodeData parent, Vector2 scaledMousePos, int depth = 0)
        {
            if (node == null || depth > 100) return;

            Rect nodeRect = GetNodeRect(node);
            bool isSelected = _selectedNode == node;

            GUIStyle style = isSelected ? _nodeSelectedStyle : _nodeStyle;
            GUI.Box(nodeRect, "", style);

            GUILayout.BeginArea(nodeRect);
            GUILayout.Label(node.NodeName, EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(node.ComponentName) && !string.IsNullOrEmpty(node.PropertyName))
            {
                GUILayout.Label($"{node.ComponentName}.{node.PropertyName}", EditorStyles.miniLabel);
                GUILayout.Label($"{node.Duration}s", EditorStyles.miniLabel);
            }
            else
            {
                GUI.contentColor = new Color(0.6f, 0.8f, 1f);
                GUILayout.Label("容器 (Container)", EditorStyles.miniLabel);
                GUI.contentColor = Color.white;
            }

            GUILayout.FlexibleSpace();
            if (node.Children != null && node.Children.Count > 0)
            {
                if (GUILayout.Button(node.IsExpanded ? "折叠" : "展开", EditorStyles.miniButton))
                {
                    node.IsExpanded = !node.IsExpanded;
                    EditorUtility.SetDirty(_activeAsset);
                }
            }

            GUILayout.EndArea();

            // 核心修复：将连线端口移到节点外部右侧，彻底解决误触
            Rect portRect = GetPortRect(node);
            bool isPortHovered = portRect.Contains(scaledMousePos);

            Handles.color = isPortHovered ? Color.cyan : new Color(0.7f, 0.7f, 0.7f, 1f);
            Handles.DrawSolidDisc(portRect.center, Vector3.forward, isPortHovered ? 7f : 5f);
            Handles.color = Color.black;
            Handles.DrawWireDisc(portRect.center, Vector3.forward, isPortHovered ? 7f : 5f);
            Handles.color = Color.white;

            ProcessNodeEvents(nodeRect, portRect, node, parent, scaledMousePos);

            if (node.IsExpanded && node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (child != null) DrawGraphNode(child, node, scaledMousePos, depth + 1);
                }
            }
        }

        private Rect GetNodeRect(ActionNodeData node)
        {
            if (node == null) return Rect.zero;
            return new Rect(node.EditorPosition.x + _panOffset.x, node.EditorPosition.y + _panOffset.y, 140, 70);
        }

        private Rect GetPortRect(ActionNodeData node)
        {
            Rect nodeRect = GetNodeRect(node);
            // 端口移至节点右侧外部，热区加大
            return new Rect(nodeRect.xMax + 2, nodeRect.center.y - 8, 16, 16);
        }

        private void ProcessNodeEvents(Rect nodeRect, Rect portRect, ActionNodeData node, ActionNodeData parent,
            Vector2 scaledMousePos)
        {
            UnityEngine.Event e = UnityEngine.Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // 严格区分端口点击与节点点击
                if (portRect.Contains(scaledMousePos))
                {
                    _relinkingNode = node;
                    e.Use();
                }
                else if (nodeRect.Contains(scaledMousePos))
                {
                    _selectedNode = node;
                    _selectedNodeParent = parent;

                    if (node != _activeAsset.RootNode)
                    {
                        _draggingNode = node;
                        _dragOffset = scaledMousePos - (node.EditorPosition + _panOffset);
                    }

                    e.Use();
                }
            }
        }

        private void HandleGraphEvents(Rect graphRect, Vector2 scaledMousePos)
        {
            UnityEngine.Event e = UnityEngine.Event.current;

            if (_draggingNode != null && e.type == EventType.MouseDrag && e.button == 0)
            {
                Vector2 rawPos = scaledMousePos - _panOffset - _dragOffset;
                // 核心修复：增加网格吸附 (Grid Snapping)，步长 10px，让节点排列更整齐
                _draggingNode.EditorPosition =
                    new Vector2(Mathf.Round(rawPos.x / 10f) * 10f, Mathf.Round(rawPos.y / 10f) * 10f);
                EditorUtility.SetDirty(_activeAsset);
                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _draggingNode = null;
            }

            if (_relinkingNode != null && e.type == EventType.MouseUp && e.button == 0)
            {
                if (FindHoveredNode(_activeAsset.RootNode, null, scaledMousePos, out var targetNode,
                        out var targetParent))
                {
                    if (targetNode != _relinkingNode && !IsAncestor(_relinkingNode, targetNode))
                    {
                        RemoveNodeFromAllParents(_activeAsset.RootNode, _relinkingNode);
                        if (targetNode.Children == null) targetNode.Children = new List<ActionNodeData>();
                        targetNode.Children.Add(_relinkingNode);
                        targetNode.IsExpanded = true;

                        _relinkingNode.EditorPosition = targetNode.EditorPosition + new Vector2(180, 0);
                        EditorUtility.SetDirty(_activeAsset);
                        GUIUtility.ExitGUI();
                    }
                }

                _relinkingNode = null;
                e.Use();
            }

            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _panOffset += e.delta / _zoom;
            }

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                FindHoveredNode(_activeAsset.RootNode, null, scaledMousePos, out var hoveredNode, out _);
                ShowContextMenu(scaledMousePos, hoveredNode);
                e.Use();
            }
        }

        private void RemoveNodeFromAllParents(ActionNodeData currentNode, ActionNodeData targetNode, int depth = 0)
        {
            if (currentNode == null || currentNode.Children == null || depth > 100) return;

            for (int i = currentNode.Children.Count - 1; i >= 0; i--)
            {
                if (currentNode.Children[i] == targetNode) currentNode.Children.RemoveAt(i);
                else RemoveNodeFromAllParents(currentNode.Children[i], targetNode, depth + 1);
            }
        }

        private bool FindHoveredNode(ActionNodeData node, ActionNodeData parent, Vector2 mousePos,
            out ActionNodeData foundNode, out ActionNodeData foundParent, int depth = 0)
        {
            foundNode = null;
            foundParent = null;
            if (node == null || depth > 100) return false;

            if (node.IsExpanded && node.Children != null)
            {
                for (int i = node.Children.Count - 1; i >= 0; i--)
                {
                    if (FindHoveredNode(node.Children[i], node, mousePos, out foundNode, out foundParent, depth + 1))
                        return true;
                }
            }

            if (GetNodeRect(node).Contains(mousePos))
            {
                foundNode = node;
                foundParent = parent;
                return true;
            }

            return false;
        }

        private bool IsAncestor(ActionNodeData potentialAncestor, ActionNodeData targetNode, int depth = 0)
        {
            if (potentialAncestor == targetNode || depth > 100) return true;
            if (potentialAncestor.Children != null)
            {
                foreach (var child in potentialAncestor.Children)
                {
                    if (IsAncestor(child, targetNode, depth + 1)) return true;
                }
            }

            return false;
        }

        private void ShowContextMenu(Vector2 mousePos, ActionNodeData hoveredNode)
        {
            GenericMenu menu = new GenericMenu();
            Vector2 spawnPos = mousePos - _panOffset;

            if (hoveredNode != null)
            {
                menu.AddItem(new GUIContent($"在 [{hoveredNode.NodeName}] 中添加子节点"), false, () =>
                {
                    if (hoveredNode.Children == null) hoveredNode.Children = new List<ActionNodeData>();
                    hoveredNode.Children.Add(new ActionNodeData { NodeName = "New Node", EditorPosition = spawnPos });
                    hoveredNode.IsExpanded = true;
                    EditorUtility.SetDirty(_activeAsset);
                });

                if (hoveredNode != _activeAsset.RootNode)
                {
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("删除此节点 (Delete)"), false, () =>
                    {
                        RemoveNodeFromAllParents(_activeAsset.RootNode, hoveredNode);
                        if (_selectedNode == hoveredNode) _selectedNode = null;
                        EditorUtility.SetDirty(_activeAsset);
                    });
                }
            }
            else
            {
                // 空白处右键，直接添加到 Root
                menu.AddItem(new GUIContent("在根节点下添加新节点"), false, () =>
                {
                    if (_activeAsset.RootNode.Children == null)
                        _activeAsset.RootNode.Children = new List<ActionNodeData>();
                    _activeAsset.RootNode.Children.Add(new ActionNodeData
                        { NodeName = "New Node", EditorPosition = spawnPos });
                    _activeAsset.RootNode.IsExpanded = true;
                    EditorUtility.SetDirty(_activeAsset);
                });
            }

            menu.ShowAsContext();
        }

        #endregion

        #region 右侧属性面板与底部控制台 (保持原有逻辑)

        private void DrawRightInspector()
        {
            using (new GUILayout.VerticalScope("box", GUILayout.Width(320), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label("属性检视 (Inspector)", _headerStyle);
                GUILayout.Space(10);

                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

                if (_selectedNode == null)
                {
                    GUILayout.Label("请在左侧或图中选择一个节点", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    Undo.RecordObject(_activeAsset, "Edit Node");

                    if (_selectedNode == _activeAsset.RootNode)
                    {
                        GUI.enabled = false;
                        EditorGUILayout.TextField("节点名称", _selectedNode.NodeName);
                        GUI.enabled = true;
                        EditorGUILayout.Space(5);
                        EditorGUILayout.HelpBox("Root 节点是动作树的绝对基石，不可修改名称、不可删除。", MessageType.Info);
                    }
                    else
                    {
                        _selectedNode.NodeName = EditorGUILayout.TextField("节点名称", _selectedNode.NodeName);
                    }

                    EditorGUILayout.Space(15);
                    DrawNodeInspector(_selectedNode);
                    DrawNodeEvents();

                    EditorGUILayout.Space(30);
                    if (_selectedNode != _activeAsset.RootNode)
                    {
                        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                        if (GUILayout.Button("删除此节点", GUILayout.Height(30)))
                        {
                            RemoveNodeFromAllParents(_activeAsset.RootNode, _selectedNode);
                            _selectedNode = null;
                            EditorUtility.SetDirty(_activeAsset);
                            GUIUtility.ExitGUI();
                        }

                        GUI.backgroundColor = Color.white;
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawNodeInspector(ActionNodeData node)
        {
            GUILayout.Label("组件与属性映射", EditorStyles.boldLabel);

            int currentCompIdx = Mathf.Max(0, Array.IndexOf(_componentNames, node.ComponentName));
            if (string.IsNullOrEmpty(node.ComponentName)) currentCompIdx = 0;

            int newCompIdx = EditorGUILayout.Popup("目标组件", currentCompIdx, _componentNames);
            if (newCompIdx != currentCompIdx)
            {
                if (newCompIdx == 0)
                {
                    node.ComponentName = "";
                    node.PropertyName = "";
                    node.Strategy = null;
                }
                else
                {
                    node.ComponentName = _componentNames[newCompIdx];
                    node.PropertyName = _actionRegistry[node.ComponentName].Keys.First();
                    AssignStrategy(node);
                }
            }

            if (!string.IsNullOrEmpty(node.ComponentName) && _actionRegistry.ContainsKey(node.ComponentName))
            {
                var props = _actionRegistry[node.ComponentName];
                string[] propNames = props.Keys.ToArray();
                int currentPropIdx = Mathf.Max(0, Array.IndexOf(propNames, node.PropertyName));

                int newPropIdx = EditorGUILayout.Popup("控制属性", currentPropIdx, propNames);
                if (newPropIdx != currentPropIdx)
                {
                    node.PropertyName = propNames[newPropIdx];
                    AssignStrategy(node);
                }

                EditorGUILayout.Space(10);
                GUILayout.Label("目标寻址", EditorStyles.boldLabel);
                node.TargetPath = EditorGUILayout.TextField("相对路径", node.TargetPath);
                DrawDragAndDropBox(node);

                EditorGUILayout.Space(10);
                GUILayout.Label("动画参数", EditorStyles.boldLabel);

                // 根据注册表动态渲染参数面板
                ActionUIConfig config = props[node.PropertyName];

                if (config.ShowAxis)
                {
                    node.AxisControl = (AxisFlags)EditorGUILayout.EnumFlagsField("通道控制 (Axis)", node.AxisControl);
                }

                if (config.ShowVector)
                {
                    if (config.ShowAxis)
                    {
                        node.TargetVector =
                            DrawVector3WithAxisControl("目标值 (Vector3)", node.TargetVector, node.AxisControl);
                    }
                    else
                    {
                        // 即使没有通道控制，也采用折行渲染，保证输入框足够大
                        node.TargetVector =
                            DrawVector3WithAxisControl("目标值 (Vector3)", node.TargetVector, AxisFlags.All);
                    }
                }

                if (config.ShowColor)
                {
                    node.TargetColor = EditorGUILayout.ColorField("目标颜色", node.TargetColor);
                }

                if (config.ShowFloat)
                {
                    node.TargetFloat = EditorGUILayout.FloatField("目标数值 (Float)", node.TargetFloat);
                }

                if (config.ShowBool)
                {
                    node.TargetBool = EditorGUILayout.Toggle("目标状态 (Bool)", node.TargetBool);
                }

                EditorGUILayout.Space(5);

                // 瞬发动作隐藏耗时与缓动曲线
                if (!config.IsInstant)
                {
                    node.Duration = EditorGUILayout.FloatField("耗时 (秒)", node.Duration);
                    node.Ease = (Ease)EditorGUILayout.EnumPopup("缓动曲线 (Ease)", node.Ease);
                }
            }

            EditorGUILayout.Space(10);
            node.Delay = EditorGUILayout.FloatField("延迟启动 (秒)", node.Delay);
        }

        /// <summary>
        /// 采用折行渲染 (Vertical Layout)。
        /// 第一行显示标题，第二行让 X/Y/Z 独占 100% 宽度，彻底解决输入框被挤压的问题。
        /// </summary>
        private Vector3 DrawVector3WithAxisControl(string label, Vector3 value, AxisFlags axisFlags)
        {
            // 第一行：单独绘制标题
            EditorGUILayout.LabelField(label);

            // 第二行：绘制输入框
            EditorGUILayout.BeginHorizontal();

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 15f; // 极简 Label，仅显示 X/Y/Z

            GUI.enabled = (axisFlags & AxisFlags.X) != 0;
            value.x = EditorGUILayout.FloatField("X", value.x, GUILayout.ExpandWidth(true));

            GUI.enabled = (axisFlags & AxisFlags.Y) != 0;
            value.y = EditorGUILayout.FloatField("Y", value.y, GUILayout.ExpandWidth(true));

            GUI.enabled = (axisFlags & AxisFlags.Z) != 0;
            value.z = EditorGUILayout.FloatField("Z", value.z, GUILayout.ExpandWidth(true));

            GUI.enabled = true; // 恢复 GUI 状态
            EditorGUIUtility.labelWidth = originalLabelWidth; // 恢复 Label 宽度

            EditorGUILayout.EndHorizontal();

            return value;
        }

        private void AssignStrategy(ActionNodeData node)
        {
            if (_actionRegistry.TryGetValue(node.ComponentName, out var props) &&
                props.TryGetValue(node.PropertyName, out var config))
            {
                node.Strategy = (IActionStrategy)Activator.CreateInstance(config.StrategyType);
            }
            else
            {
                node.Strategy = null;
            }
        }

        private void DrawDragAndDropBox(ActionNodeData node)
        {
            Rect dropRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "将 Hierarchy 中的子物体拖拽至此\n自动获取相对路径", _dropBoxStyle);

            UnityEngine.Event e = UnityEngine.Event.current;
            if (dropRect.Contains(e.mousePosition))
            {
                if (e.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    e.Use();
                }
                else if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    if (DragAndDrop.objectReferences.Length > 0 &&
                        DragAndDrop.objectReferences[0] is GameObject draggedObj)
                    {
                        if (_rootTarget == null)
                        {
                            Debug.LogWarning("[ActionEngine] 请先在底部绑定场景实例 (RootTarget)！");
                        }
                        else
                        {
                            string path = GetRelativePath(_rootTarget.transform, draggedObj.transform);
                            if (path != null) node.TargetPath = path;
                        }
                    }

                    e.Use();
                }
            }
        }

        private void DrawNodeEvents()
        {
            if (_nodePaths.TryGetValue(_selectedNode, out string path))
            {
                SerializedObject so = new SerializedObject(_activeAsset);
                so.Update();

                SerializedProperty nodeProp = so.FindProperty(path);
                if (nodeProp != null)
                {
                    EditorGUILayout.Space(15);
                    GUILayout.Label("生命周期回调 (Events)", EditorStyles.boldLabel);

                    SerializedProperty onStartProp = nodeProp.FindPropertyRelative("OnStartEvent");
                    SerializedProperty onUpdateProp = nodeProp.FindPropertyRelative("OnUpdateEvent");
                    SerializedProperty onCompleteProp = nodeProp.FindPropertyRelative("OnCompleteEvent");

                    if (onStartProp != null) EditorGUILayout.PropertyField(onStartProp, new GUIContent("On Start"));
                    if (onUpdateProp != null)
                        EditorGUILayout.PropertyField(onUpdateProp, new GUIContent("On Update (Progress)"));
                    if (onCompleteProp != null)
                        EditorGUILayout.PropertyField(onCompleteProp, new GUIContent("On Complete"));
                }

                so.ApplyModifiedProperties();
            }
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            if (root == target) return "";
            List<string> parts = new List<string>();
            Transform curr = target;
            while (curr != null && curr != root)
            {
                parts.Add(curr.name);
                curr = curr.parent;
            }

            if (curr == null)
            {
                Debug.LogError("[ActionEngine] 拖拽的物体不在当前绑定的 Root 层级下！");
                return null;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        #endregion

        #region 底部控制台

        private void DrawBottomBar()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(35)))
            {
                GUILayout.Label("运行环境:", EditorStyles.miniBoldLabel, GUILayout.Width(60));

                EditorGUI.BeginChangeCheck();
                var prefab = (GameObject)EditorGUILayout.ObjectField(_activeAsset.TargetPrefab, typeof(GameObject),
                    false, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                {
                    _activeAsset.TargetPrefab = prefab;
                    EditorUtility.SetDirty(_activeAsset);
                }

                GUILayout.Label("=>", GUILayout.Width(20));

                EditorGUI.BeginChangeCheck();
                _rootTarget =
                    (GameObject)EditorGUILayout.ObjectField(_rootTarget, typeof(GameObject), true,
                        GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                {
                    // 【核心改动】：手动更换目标时，立刻重新捕获初始状态
                    ActionEngineRunner.InitSnapshot(_rootTarget, _activeAsset, true);
                }

                if (_rootTarget == null && _activeAsset.TargetPrefab != null)
                {
                    if (GUILayout.Button("自动生成实例", EditorStyles.toolbarButton, GUILayout.Width(90))) AutoBindTarget();
                }

                // 【新增功能】：手动刷新基准快照
                if (_rootTarget != null && GUILayout.Button("刷新基准", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    ActionEngineRunner.InitSnapshot(_rootTarget, _activeAsset, true);
                    ShowNotification(new GUIContent("已更新物体的初始状态快照"));
                }

                GUILayout.FlexibleSpace();

                bool canPlay = _rootTarget != null && _activeAsset != null;
                GUI.enabled = canPlay;

                GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
                if (GUILayout.Button("↺ 重置状态", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    StopPlayback();
                    // 【核心改动】：只还原，不清理
                    ActionEngineRunner.RestoreSnapshot(_rootTarget);
                }

                GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
                if (GUILayout.Button("◀ 倒放", EditorStyles.toolbarButton, GUILayout.Width(80))) StartPlayback(true);

                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                if (GUILayout.Button("▶ 正放", EditorStyles.toolbarButton, GUILayout.Width(80))) StartPlayback(false);

                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("■ 停止", EditorStyles.toolbarButton, GUILayout.Width(60))) StopPlayback();

                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
            }
        }

        private void StartPlayback(bool isReverse)
        {
            StopPlayback();
            _playbackCts = new CancellationTokenSource();
            ActionEngineRunner.Play(_rootTarget, _activeAsset, isReverse, _playbackCts.Token).Forget();
        }

        private void StopPlayback()
        {
            if (_playbackCts != null)
            {
                _playbackCts.Cancel();
                _playbackCts.Dispose();
                _playbackCts = null;

                if (!Application.isPlaying && _rootTarget != null)
                {
                    ActionEngineRunner.RestoreSnapshot(_rootTarget);
                }
            }
        }

        private void AutoBindTarget()
        {
            if (_activeAsset == null || _activeAsset.TargetPrefab == null) return;
            var existing = GameObject.FindObjectsOfType<GameObject>().FirstOrDefault(go =>
                PrefabUtility.GetCorrespondingObjectFromSource(go) == _activeAsset.TargetPrefab);

            if (existing != null) _rootTarget = existing;
            else if (!Application.isPlaying)
            {
                _rootTarget = (GameObject)PrefabUtility.InstantiatePrefab(_activeAsset.TargetPrefab);
                Undo.RegisterCreatedObjectUndo(_rootTarget, "Auto Instantiate");
            }

            // 【核心改动】：自动生成实例后，立刻捕获初始状态
            ActionEngineRunner.InitSnapshot(_rootTarget, _activeAsset, true);
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("请在顶部选择或新建一个 ActionEngineAsset", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        #endregion
    }
}