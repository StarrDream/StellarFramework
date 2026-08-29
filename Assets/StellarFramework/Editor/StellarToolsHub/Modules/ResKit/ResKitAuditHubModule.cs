using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using StellarFramework.Res;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// ResKit 运行时资源审计工具。
    /// </summary>
    [StellarTool("ResKit 资源审计", "资源管理", 2,
        RequiredAssemblyNames = new[] { "StellarFramework.ResKit" })]
    public class ResKitAuditHubModule : ToolModule
    {
        public override string Icon => "d_SettingsIcon";
        public override string Description => "实时监控 ResKit 资源驻留状态、引用计数与持有者，用于排查资源泄漏。";

        private const double RefreshInterval = 1.0;

        private readonly List<ResDataSnapshot> _snapshotList = new List<ResDataSnapshot>();

        private bool _autoRefresh = true;
        private Vector2 _scrollPosition;
        private object _sharedCacheInstance;
        private FieldInfo _sharedCacheField;
        private MethodInfo _gcMethod;
        private double _lastRefreshTime;

        private sealed class ResDataSnapshot
        {
            public string Key;
            public string Path;
            public string LoaderName;
            public int RefCount;
            public readonly List<string> Owners = new List<string>();
            public bool IsExpanded;
        }

        public override void OnEnable()
        {
            InitializeReflection();
            RefreshSnapshot();
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

        private void InitializeReflection()
        {
            Type resMgrType = typeof(ResKit).Assembly.GetType("StellarFramework.Res.ResMgr");
            if (resMgrType == null)
            {
                Debug.LogError("[ResKitAuditHubModule] 初始化失败：无法通过反射获取 StellarFramework.Res.ResMgr 类型，请检查命名空间或类名是否变更。");
                return;
            }

            _sharedCacheField = resMgrType.GetField("_sharedCache", BindingFlags.NonPublic | BindingFlags.Static);
            if (_sharedCacheField == null)
            {
                Debug.LogError("[ResKitAuditHubModule] 初始化失败：无法获取 _sharedCache 字段。");
                return;
            }

            _gcMethod = resMgrType.GetMethod("GarbageCollect", BindingFlags.Public | BindingFlags.Static);
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

                if (GUILayout.Button("强制 GC 与卸载", EditorStyles.toolbarButton, GUILayout.Width(110)))
                {
                    ExecuteGarbageCollect();
                }
            }
        }

        private void HandleAutoRefresh()
        {
            if (!_autoRefresh)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - _lastRefreshTime <= RefreshInterval)
            {
                return;
            }

            RefreshSnapshot();
            _lastRefreshTime = EditorApplication.timeSinceStartup;
            Window.Repaint();
        }

        private void RefreshSnapshot()
        {
            if (_sharedCacheField == null)
            {
                return;
            }

            _sharedCacheInstance = _sharedCacheField.GetValue(null);
            if (!(_sharedCacheInstance is IDictionary dict))
            {
                return;
            }

            Dictionary<string, bool> expandStates = new Dictionary<string, bool>();
            foreach (ResDataSnapshot snap in _snapshotList)
            {
                expandStates[snap.Key] = snap.IsExpanded;
            }

            _snapshotList.Clear();

            foreach (DictionaryEntry kvp in dict)
            {
                if (!(kvp.Value is ResData resData))
                {
                    continue;
                }

                ResDataSnapshot snapshot = new ResDataSnapshot
                {
                    Key = kvp.Key.ToString(),
                    Path = resData.Path,
                    LoaderName = resData.LoaderName,
                    RefCount = resData.RefCount
                };

                if (expandStates.TryGetValue(snapshot.Key, out bool isExpanded))
                {
                    snapshot.IsExpanded = isExpanded;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (resData.Owners != null)
                {
                    foreach (string owner in resData.Owners)
                    {
                        snapshot.Owners.Add(owner);
                    }
                }
#endif
                _snapshotList.Add(snapshot);
            }

            _snapshotList.Sort((a, b) =>
            {
                int refCompare = b.RefCount.CompareTo(a.RefCount);
                return refCompare != 0
                    ? refCompare
                    : string.Compare(a.Path, b.Path, StringComparison.Ordinal);
            });
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

            foreach (ResDataSnapshot snap in _snapshotList)
            {
                using (new GUILayout.VerticalScope("box"))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        snap.IsExpanded = EditorGUILayout.Foldout(snap.IsExpanded, snap.Path, true,
                            EditorStyles.foldoutHeader);

                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"[{snap.LoaderName}]", EditorStyles.miniLabel, GUILayout.Width(100));

                        Color defaultColor = GUI.contentColor;
                        if (snap.RefCount == 0)
                        {
                            GUI.contentColor = Color.red;
                        }
                        else if (snap.RefCount > 5)
                        {
                            GUI.contentColor = Color.cyan;
                        }

                        GUILayout.Label($"Ref: {snap.RefCount}", EditorStyles.boldLabel, GUILayout.Width(60));
                        GUI.contentColor = defaultColor;
                    }

                    if (!snap.IsExpanded)
                    {
                        continue;
                    }

                    EditorGUI.indentLevel++;
                    if (snap.Owners.Count == 0)
                    {
                        EditorGUILayout.LabelField("无明确持有者（可能存在泄漏或处于对象池游离状态）", Window.DangerButtonStyle);
                    }
                    else
                    {
                        foreach (string owner in snap.Owners)
                        {
                            EditorGUILayout.LabelField($"-> {owner}", EditorStyles.miniLabel);
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ExecuteGarbageCollect()
        {
            if (_gcMethod == null)
            {
                Debug.LogError("[ResKitAuditHubModule] 无法调用 GarbageCollect 方法。");
                return;
            }

            _gcMethod.Invoke(null, null);
            RefreshSnapshot();
            Window.ShowNotification(new GUIContent("已触发强制 GC 与资源卸载"));
        }
    }
}
