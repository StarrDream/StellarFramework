using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using StellarFramework.Event;
using Object = UnityEngine.Object;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// EventKit 运行时事件链路追踪工具
    /// 职责：在 Editor 环境下通过反射穿透泛型静态类的物理隔离，可视化当前内存中活跃的事件与监听者。
    /// </summary>
    [StellarTool("EventKit 链路追踪", "框架核心", 2)]
    public class EventKitTrackerHubModule : ToolModule
    {
        public override string Icon => "d_EventSystem Icon";
        public override string Description => "实时监控 EventKit 中所有活跃的结构体事件与枚举事件，定位事件流转与监听者泄漏。";

        private bool _autoRefresh = true;
        private Vector2 _scrollPosition;
        private double _lastRefreshTime;
        private const double RefreshInterval = 1.0;

        private readonly List<EventSnapshot> _snapshots = new List<EventSnapshot>();
        private readonly Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();

        // 反射缓存
        private Type _typeEventBoxGeneric;
        private Type _enumEventBoxGeneric;
        private List<Type> _allEnumTypesCache;
        private bool _reflectionInitialized;

        private class EventSnapshot
        {
            public string EventName;
            public string EventCategory; // "TypeEvent" 或 "EnumEvent"
            public int ListenerCount;
            public List<ListenerSnapshot> Listeners = new List<ListenerSnapshot>();
        }

        private class ListenerSnapshot
        {
            public string TargetName;
            public string MethodName;
            public Object TargetObject; // 用于在 Editor 中 Ping
        }

        public override void OnEnable()
        {
            InitializeReflection();
            RefreshSnapshots();
        }

        private void InitializeReflection()
        {
            if (_reflectionInitialized) return;

            try
            {
                // 1. 获取 TypeEvent 的 EventBox<T>
                _typeEventBoxGeneric = typeof(GlobalTypeEvent).GetNestedType("EventBox`1", BindingFlags.NonPublic);

                // 2. 获取 EnumEvent 的 EventBox<T>
                _enumEventBoxGeneric = typeof(GlobalEnumEvent).GetNestedType("EventBox`1", BindingFlags.NonPublic);

                // 3. 缓存所有用户程序集中的 Enum 类型 (排除 Unity 和 System 底层)
                _allEnumTypesCache = new List<Type>();
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    string name = assembly.GetName().Name;
                    if (name.StartsWith("System") || name.StartsWith("Unity") || name.StartsWith("mscorlib") ||
                        name.StartsWith("Mono"))
                        continue;

                    var types = assembly.GetTypes();
                    foreach (var t in types)
                    {
                        if (t.IsEnum) _allEnumTypesCache.Add(t);
                    }
                }

                _reflectionInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventKitTracker] 反射初始化失败: {e.Message}");
            }
        }

        public override void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("事件链路追踪仅在游戏运行 (Play Mode) 时提供实时数据。", MessageType.Info);
                return;
            }

            if (!_reflectionInitialized)
            {
                EditorGUILayout.HelpBox("反射初始化失败，无法读取事件缓存。", MessageType.Error);
                return;
            }

            DrawToolbar();
            HandleAutoRefresh();
            DrawEventList();
        }

        private void DrawToolbar()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("手动刷新", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    RefreshSnapshots();
                }

                _autoRefresh = GUILayout.Toggle(_autoRefresh, "自动刷新 (1s)", EditorStyles.toolbarButton,
                    GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                int totalListeners = _snapshots.Sum(s => s.ListenerCount);
                GUILayout.Label($"活跃事件类: {_snapshots.Count} | 监听者总数: {totalListeners}", EditorStyles.miniLabel);
            }
        }

        private void HandleAutoRefresh()
        {
            if (!_autoRefresh) return;

            if (EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                RefreshSnapshots();
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Window.Repaint();
            }
        }

        private void RefreshSnapshots()
        {
            _snapshots.Clear();
            ScanTypeEvents();
            ScanEnumEvents();

            // 排序：按监听者数量降序，再按名称升序
            _snapshots.Sort((a, b) =>
            {
                int countCompare = b.ListenerCount.CompareTo(a.ListenerCount);
                return countCompare != 0
                    ? countCompare
                    : string.Compare(a.EventName, b.EventName, StringComparison.Ordinal);
            });
        }

        private void ScanTypeEvents()
        {
            if (_typeEventBoxGeneric == null) return;

            // TypeCache 极速获取所有实现了 ITypeEvent 的结构体
            var eventTypes = TypeCache.GetTypesDerivedFrom<ITypeEvent>();

            foreach (var type in eventTypes)
            {
                if (type.IsAbstract || type.IsInterface) continue;

                try
                {
                    Type boxType = _typeEventBoxGeneric.MakeGenericType(type);
                    FieldInfo subField = boxType.GetField("Subscribers", BindingFlags.Public | BindingFlags.Static);
                    if (subField == null) continue;

                    Delegate del = subField.GetValue(null) as Delegate;
                    if (del != null)
                    {
                        var invList = del.GetInvocationList();
                        if (invList.Length > 0)
                        {
                            var snapshot = new EventSnapshot
                            {
                                EventName = type.Name,
                                EventCategory = "TypeEvent",
                                ListenerCount = invList.Length
                            };

                            foreach (var d in invList)
                            {
                                snapshot.Listeners.Add(CreateListenerSnapshot(d));
                            }

                            _snapshots.Add(snapshot);
                        }
                    }
                }
                catch (Exception)
                {
                    // 忽略泛型实例化失败的异常（例如某些泛型约束不匹配的边缘情况）
                }
            }
        }

        private void ScanEnumEvents()
        {
            if (_enumEventBoxGeneric == null || _allEnumTypesCache == null) return;

            foreach (var enumType in _allEnumTypesCache)
            {
                try
                {
                    Type boxType = _enumEventBoxGeneric.MakeGenericType(enumType);
                    FieldInfo tableField = boxType.GetField("EventTable", BindingFlags.Public | BindingFlags.Static);
                    if (tableField == null) continue;

                    IDictionary table = tableField.GetValue(null) as IDictionary;
                    if (table != null && table.Count > 0)
                    {
                        foreach (DictionaryEntry kvp in table)
                        {
                            Delegate del = kvp.Value as Delegate;
                            if (del != null)
                            {
                                var invList = del.GetInvocationList();
                                if (invList.Length > 0)
                                {
                                    var snapshot = new EventSnapshot
                                    {
                                        EventName = $"{enumType.Name}.{kvp.Key}",
                                        EventCategory = "EnumEvent",
                                        ListenerCount = invList.Length
                                    };

                                    foreach (var d in invList)
                                    {
                                        snapshot.Listeners.Add(CreateListenerSnapshot(d));
                                    }

                                    _snapshots.Add(snapshot);
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // 忽略异常
                }
            }
        }

        private ListenerSnapshot CreateListenerSnapshot(Delegate d)
        {
            var ls = new ListenerSnapshot();
            ls.MethodName = d.Method.Name;

            if (d.Target != null)
            {
                ls.TargetName = d.Target.GetType().Name;
                if (d.Target is Object unityObj)
                {
                    ls.TargetObject = unityObj;
                    ls.TargetName = unityObj.name + $" ({ls.TargetName})";
                }
            }
            else
            {
                ls.TargetName = "Static Method";
            }

            // 识别编译器生成的闭包 (Lambda)
            if (ls.MethodName.Contains("<") && ls.MethodName.Contains(">"))
            {
                ls.MethodName = "[Lambda/Closure] " + ls.MethodName;
            }

            return ls;
        }

        private void DrawEventList()
        {
            if (_snapshots.Count == 0)
            {
                GUILayout.Space(20);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("当前无活跃事件", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var snap in _snapshots)
            {
                using (new GUILayout.VerticalScope("box"))
                {
                    // 1. 将变量声明提前到 using 块外部，扩大其作用域
                    bool isExp = false;

                    using (new GUILayout.HorizontalScope())
                    {
                        // 2. 这里不再使用 out bool，而是直接使用外部声明的 isExp
                        if (!_foldoutStates.TryGetValue(snap.EventName, out isExp))
                        {
                            isExp = false;
                        }

                        isExp = EditorGUILayout.Foldout(isExp, snap.EventName, true, EditorStyles.foldoutHeader);
                        _foldoutStates[snap.EventName] = isExp;

                        GUILayout.FlexibleSpace();

                        GUI.contentColor = snap.EventCategory == "TypeEvent"
                            ? new Color(0.4f, 0.8f, 1f)
                            : new Color(1f, 0.8f, 0.4f);
                        GUILayout.Label($"[{snap.EventCategory}]", EditorStyles.miniLabel, GUILayout.Width(80));
                        GUI.contentColor = Color.white;

                        GUILayout.Label($"Listeners: {snap.ListenerCount}", EditorStyles.boldLabel,
                            GUILayout.Width(80));
                    }

                    // 3. 现在这里可以正常访问到 isExp 了
                    if (isExp)
                    {
                        EditorGUI.indentLevel++;
                        foreach (var listener in snap.Listeners)
                        {
                            using (new GUILayout.HorizontalScope())
                            {
                                GUILayout.Label("->", GUILayout.Width(20));

                                if (listener.TargetObject != null)
                                {
                                    if (GUILayout.Button(listener.TargetName, EditorStyles.linkLabel,
                                            GUILayout.Width(200)))
                                    {
                                        EditorGUIUtility.PingObject(listener.TargetObject);
                                        Selection.activeObject = listener.TargetObject;
                                    }
                                }
                                else
                                {
                                    GUILayout.Label(listener.TargetName, GUILayout.Width(200));
                                }

                                GUILayout.Label($" :: {listener.MethodName}", EditorStyles.miniLabel);
                            }
                        }

                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}