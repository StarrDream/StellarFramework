using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using StellarFramework.Res;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// ResKit 运行时资源审计工具
    /// 职责：在 Editor 环境下通过反射读取 ResMgr 的内部缓存，提供实时的资源驻留与泄漏排查视图。
    /// </summary>
    [StellarTool("ResKit 资源审计", "框架核心", 1)]
    public class ResKitAuditHubModule : ToolModule
    {
        public override string Icon => "d_SettingsIcon";
        public override string Description => "实时监控 ResKit 内存中的资源驻留状态、引用计数与具体持有者，用于排查内存泄漏。";

        private bool _autoRefresh = true;
        private Vector2 _scrollPosition;
        private object _sharedCacheInstance;
        private FieldInfo _sharedCacheField;
        private MethodInfo _gcMethod;

        // 缓存反射获取的数据，避免每帧高频反射产生严重卡顿
        private readonly List<ResDataSnapshot> _snapshotList = new List<ResDataSnapshot>();
        private double _lastRefreshTime;
        private const double RefreshInterval = 1.0; // 自动刷新间隔（秒）

        private class ResDataSnapshot
        {
            public string Key;
            public string Path;
            public string LoaderName;
            public int RefCount;
            public List<string> Owners = new List<string>();
            public bool IsExpanded;
        }

        public override void OnEnable()
        {
            InitializeReflection();
            RefreshSnapshot();
        }

        private void InitializeReflection()
        {
            Type resMgrType = typeof(ResKit).Assembly.GetType("StellarFramework.Res.ResMgr");
            if (resMgrType == null)
            {
                Debug.LogError("[ResKitAuditHubModule] 初始化失败: 无法通过反射获取 StellarFramework.Res.ResMgr 类型，请检查命名空间或类名是否变更。");
                return;
            }

            _sharedCacheField = resMgrType.GetField("_sharedCache", BindingFlags.NonPublic | BindingFlags.Static);
            if (_sharedCacheField == null)
            {
                Debug.LogError("[ResKitAuditHubModule] 初始化失败: 无法获取 _sharedCache 字段。");
                return;
            }

            _gcMethod = resMgrType.GetMethod("GarbageCollect", BindingFlags.Public | BindingFlags.Static);
        }

        public override void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("资源审计功能仅在游戏运行 (Play Mode) 时提供实时数据。", MessageType.Info);
                return;
            }

            if (_sharedCacheField == null)
            {
                EditorGUILayout.HelpBox("反射初始化失败，无法读取资源缓存。", MessageType.Error);
                return;
            }

            DrawToolbar();
            HandleAutoRefresh();
            DrawResourceList();
        }

        private void DrawToolbar()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("手动刷新", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    RefreshSnapshot();
                }

                _autoRefresh = GUILayout.Toggle(_autoRefresh, "自动刷新 (1s)", EditorStyles.toolbarButton,
                    GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                GUILayout.Label($"驻留总数: {_snapshotList.Count}", EditorStyles.miniLabel);

                if (GUILayout.Button("强制 GC 与卸载", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    ExecuteGarbageCollect();
                }
            }
        }

        private void HandleAutoRefresh()
        {
            if (!_autoRefresh) return;

            if (EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                RefreshSnapshot();
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Window.Repaint();
            }
        }

        private void RefreshSnapshot()
        {
            if (_sharedCacheField == null) return;

            _sharedCacheInstance = _sharedCacheField.GetValue(null);
            if (_sharedCacheInstance is IDictionary dict)
            {
                // 记录旧的展开状态
                Dictionary<string, bool> expandStates = new Dictionary<string, bool>();
                foreach (var snap in _snapshotList)
                {
                    expandStates[snap.Key] = snap.IsExpanded;
                }

                _snapshotList.Clear();

                foreach (DictionaryEntry kvp in dict)
                {
                    ResData resData = kvp.Value as ResData;
                    if (resData == null) continue;

                    var snapshot = new ResDataSnapshot
                    {
                        Key = kvp.Key.ToString(),
                        Path = resData.Path,
                        LoaderName = resData.LoaderName,
                        RefCount = resData.RefCount
                    };

                    if (expandStates.TryGetValue(snapshot.Key, out bool isExp))
                    {
                        snapshot.IsExpanded = isExp;
                    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (resData.Owners != null)
                    {
                        foreach (var owner in resData.Owners)
                        {
                            snapshot.Owners.Add(owner);
                        }
                    }
#endif
                    _snapshotList.Add(snapshot);
                }

                // 按照引用计数降序，再按路径升序排序
                _snapshotList.Sort((a, b) =>
                {
                    int refCompare = b.RefCount.CompareTo(a.RefCount);
                    return refCompare != 0 ? refCompare : string.Compare(a.Path, b.Path, StringComparison.Ordinal);
                });
            }
        }

        private void DrawResourceList()
        {
            if (_snapshotList.Count == 0)
            {
                GUILayout.Space(20);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("当前无驻留资源", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var snap in _snapshotList)
            {
                using (new GUILayout.VerticalScope("box"))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        snap.IsExpanded = EditorGUILayout.Foldout(snap.IsExpanded, snap.Path, true,
                            EditorStyles.foldoutHeader);

                        GUILayout.FlexibleSpace();

                        GUILayout.Label($"[{snap.LoaderName}]", EditorStyles.miniLabel, GUILayout.Width(100));

                        // 引用计数颜色高亮
                        Color defaultColor = GUI.contentColor;
                        if (snap.RefCount == 0) GUI.contentColor = Color.red;
                        else if (snap.RefCount > 5) GUI.contentColor = Color.cyan;

                        GUILayout.Label($"Ref: {snap.RefCount}", EditorStyles.boldLabel, GUILayout.Width(60));

                        GUI.contentColor = defaultColor;
                    }

                    if (snap.IsExpanded)
                    {
                        EditorGUI.indentLevel++;
                        if (snap.Owners.Count == 0)
                        {
                            EditorGUILayout.LabelField("无明确持有者 (可能存在泄漏或处于对象池游离态)", Window.DangerButtonStyle);
                        }
                        else
                        {
                            foreach (var owner in snap.Owners)
                            {
                                EditorGUILayout.LabelField($"-> {owner}", EditorStyles.miniLabel);
                            }
                        }

                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ExecuteGarbageCollect()
        {
            if (_gcMethod != null)
            {
                _gcMethod.Invoke(null, null);
                RefreshSnapshot();
                Window.ShowNotification(new GUIContent("已触发强制 GC 与资源卸载"));
            }
            else
            {
                Debug.LogError("[ResKitAuditHubModule] 无法调用 GarbageCollect 方法。");
            }
        }
    }
}