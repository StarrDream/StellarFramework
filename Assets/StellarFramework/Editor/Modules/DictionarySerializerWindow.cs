using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor
{
    public class DictionarySerializerWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<DictionarySerializerWindow>("Dictionary Serializer");
            window.minSize = new Vector2(820, 560);
            window.Show();
        }

        private const int MAX_RECURSION_DEPTH = 8;
        private MonoBehaviour _targetMono;
        private Vector2 _scroll;

        // --- 缓存结构 ---
        private class FieldCache
        {
            public FieldInfo Info;
            public bool IsDict;
            public bool IsList;
            public bool IsArray;
            public bool IsPlain;
            public Type KeyType; // 仅 Dict 用
            public Type ValType; // 仅 Dict 用
            public Type ElemType; // List/Array 用
        }

        // 核心缓存：避免 OnGUI 频繁反射
        private readonly Dictionary<Type, List<FieldCache>> _typeFieldCache = new Dictionary<Type, List<FieldCache>>();

        // UI 状态缓存
        private readonly Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();

        // 数据编辑缓存：Key=FieldPath, Value=List<DictionaryEntry> (用于编辑字典)
        private readonly Dictionary<string, object> _editDataCache = new Dictionary<string, object>();

        private void OnEnable()
        {
            var go = Selection.activeGameObject;
            if (go) _targetMono = go.GetComponent<MonoBehaviour>();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Dictionary / List / Array 增强编辑器", EditorStyles.boldLabel);

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _targetMono = (MonoBehaviour)EditorGUILayout.ObjectField("目标组件", _targetMono, typeof(MonoBehaviour), true);
                if (EditorGUI.EndChangeCheck())
                {
                    ClearCache();
                }

                if (_targetMono == null)
                {
                    EditorGUILayout.HelpBox("请将带有 Dictionary 的 MonoBehaviour 拖入此处。", MessageType.Info);
                    return;
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("刷新字段 / 重置缓存", GUILayout.Height(28)))
                    {
                        ClearCache();
                        Debug.Log("[DictionarySerializer] 缓存已重置");
                    }

                    if (GUILayout.Button("应用修改 (Save)", GUILayout.Height(28)))
                    {
                        ApplyChanges();
                    }
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawFields(_targetMono, _targetMono.GetType(), 0);
            EditorGUILayout.Space(20);
            EditorGUILayout.EndScrollView();
        }

        private void ClearCache()
        {
            _typeFieldCache.Clear();
            _editDataCache.Clear();
            _foldoutStates.Clear();
        }

        private void DrawFields(object owner, Type ownerType, int depth)
        {
            if (depth > MAX_RECURSION_DEPTH) return;

            // 1. 获取或构建缓存
            if (!_typeFieldCache.TryGetValue(ownerType, out var cachedFields))
            {
                cachedFields = new List<FieldCache>();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var fields = ownerType.GetFields(flags);

                foreach (var f in fields)
                {
                    // 过滤不可序列化字段
                    if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;

                    var ft = f.FieldType;
                    var cache = new FieldCache
                    {
                        Info = f,
                        IsDict = IsDictionary(ft),
                        IsList = IsList(ft),
                        IsArray = ft.IsArray,
                        IsPlain = IsPlain(ft)
                    };

                    if (cache.IsDict)
                    {
                        var args = ft.GetGenericArguments();
                        cache.KeyType = args[0];
                        cache.ValType = args[1];
                    }
                    else if (cache.IsList)
                    {
                        cache.ElemType = ft.GetGenericArguments()[0];
                    }
                    else if (cache.IsArray)
                    {
                        cache.ElemType = ft.GetElementType();
                    }

                    cachedFields.Add(cache);
                }

                _typeFieldCache[ownerType] = cachedFields;
            }

            // 2. 遍历绘制
            foreach (var cache in cachedFields)
            {
                var f = cache.Info;
                object value = f.GetValue(owner);
                string key = ownerType.FullName + "." + f.Name + depth; // 唯一Key

                EditorGUILayout.Space(4);
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string label = $"{f.Name}  ({f.FieldType.Name})";

                    if (cache.IsDict)
                    {
                        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                        DrawDictionaryField(owner, cache, value, key);
                    }
                    else if (cache.IsList)
                    {
                        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                        DrawListField(owner, cache, value, key);
                    }
                    else if (cache.IsArray)
                    {
                        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                        DrawArrayField(owner, cache, value, key);
                    }
                    else if (cache.IsPlain)
                    {
                        DrawPlainField(owner, f, value);
                    }
                    else
                    {
                        // 递归对象
                        bool fold = GetFoldout(key);
                        bool newFold = EditorGUILayout.Foldout(fold, label, true);
                        SetFoldout(key, newFold);
                        if (newFold)
                        {
                            if (value == null)
                            {
                                EditorGUILayout.HelpBox("null", MessageType.None);
                            }
                            else
                            {
                                DrawFields(value, f.FieldType, depth + 1);
                            }
                        }
                    }
                }
            }
        }

        // --- 具体类型的绘制逻辑 ---

        private void DrawPlainField(object owner, FieldInfo f, object value)
        {
            var t = f.FieldType;
            object newVal = value;

            if (t == typeof(int)) newVal = EditorGUILayout.IntField(f.Name, (int)value);
            else if (t == typeof(float)) newVal = EditorGUILayout.FloatField(f.Name, (float)value);
            else if (t == typeof(bool)) newVal = EditorGUILayout.Toggle(f.Name, (bool)value);
            else if (t == typeof(string)) newVal = EditorGUILayout.TextField(f.Name, (string)value);
            else if (t == typeof(Vector2)) newVal = EditorGUILayout.Vector2Field(f.Name, (Vector2)value);
            else if (t == typeof(Vector3)) newVal = EditorGUILayout.Vector3Field(f.Name, (Vector3)value);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                newVal = EditorGUILayout.ObjectField(f.Name, (UnityEngine.Object)value, t, true);
            else
                EditorGUILayout.LabelField(f.Name, value?.ToString() ?? "null");

            if (!Equals(newVal, value))
            {
                f.SetValue(owner, newVal);
                EditorUtility.SetDirty(_targetMono);
            }
        }

        private void DrawDictionaryField(object owner, FieldCache cache, object value, string key)
        {
            if (value == null)
            {
                EditorGUILayout.HelpBox("Dictionary is null", MessageType.Warning);
                return;
            }

            bool fold = GetFoldout(key);
            bool newFold = EditorGUILayout.Foldout(fold, "展开内容", true);
            SetFoldout(key, newFold);
            if (!newFold) return;

            // 获取或创建编辑缓存
            string cacheKey = key + ".__dict_cache";
            if (!_editDataCache.TryGetValue(cacheKey, out var editObj))
            {
                var dict = (IDictionary)value;
                var list = new List<DictionaryEntry>(dict.Count);
                foreach (DictionaryEntry e in dict) list.Add(e);
                _editDataCache[cacheKey] = list;
                editObj = list;
            }

            var entries = (List<DictionaryEntry>)editObj;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Count: {entries.Count}", EditorStyles.miniLabel);
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                entries.Add(new DictionaryEntry { Key = GetDefault(cache.KeyType), Value = GetDefault(cache.ValType) });
            }

            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < entries.Count; i++)
            {
                using (new GUILayout.HorizontalScope())
                {
                    var e = entries[i];

                    // Key
                    object newK = DrawValueInline(cache.KeyType, e.Key, GUILayout.Width(120));
                    GUILayout.Label(":", GUILayout.Width(10));
                    // Value
                    object newV = DrawValueInline(cache.ValType, e.Value);

                    entries[i] = new DictionaryEntry { Key = newK, Value = newV };

                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        entries.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void DrawListField(object owner, FieldCache cache, object value, string key)
        {
            if (value == null)
            {
                EditorGUILayout.HelpBox("List is null", MessageType.Warning);
                return;
            }

            bool fold = GetFoldout(key);
            bool newFold = EditorGUILayout.Foldout(fold, "展开内容", true);
            SetFoldout(key, newFold);
            if (!newFold) return;

            IList list = (IList)value;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Count: {list.Count}", EditorStyles.miniLabel);
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
            {
                list.Add(GetDefault(cache.ElemType));
            }

            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < list.Count; i++)
            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(30));
                    object oldVal = list[i];
                    object newVal = DrawValueInline(cache.ElemType, oldVal);

                    // 只有值改变时才写入，防止 GUI 循环刷新
                    if (!Equals(newVal, oldVal)) list[i] = newVal;

                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void DrawArrayField(object owner, FieldCache cache, object value, string key)
        {
            if (value == null)
            {
                EditorGUILayout.HelpBox("Array is null", MessageType.Warning);
                return;
            }

            bool fold = GetFoldout(key);
            bool newFold = EditorGUILayout.Foldout(fold, "展开内容", true);
            SetFoldout(key, newFold);
            if (!newFold) return;

            Array arr = (Array)value;
            EditorGUILayout.LabelField($"Length: {arr.Length} (Array 不支持动态增删，请使用 List)", EditorStyles.miniLabel);

            for (int i = 0; i < arr.Length; i++)
            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(30));
                    object oldVal = arr.GetValue(i);
                    object newVal = DrawValueInline(cache.ElemType, oldVal);
                    if (!Equals(newVal, oldVal)) arr.SetValue(newVal, i);
                }
            }
        }

        // --- 辅助方法 ---

        private object DrawValueInline(Type t, object v, params GUILayoutOption[] options)
        {
            if (t == typeof(int)) return EditorGUILayout.IntField((int)(v ?? 0), options);
            if (t == typeof(float)) return EditorGUILayout.FloatField((float)(v ?? 0f), options);
            if (t == typeof(string)) return EditorGUILayout.TextField((string)(v ?? ""), options);
            if (t == typeof(bool)) return EditorGUILayout.Toggle((bool)(v ?? false), options);
            if (t == typeof(Vector2)) return EditorGUILayout.Vector2Field("", (Vector2)(v ?? Vector2.zero), options);
            if (t == typeof(Vector3)) return EditorGUILayout.Vector3Field("", (Vector3)(v ?? Vector3.zero), options);
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return EditorGUILayout.ObjectField((UnityEngine.Object)v, t, true, options);

            // 简单对象尝试显示 ToString，暂不支持递归编辑复杂对象
            EditorGUILayout.LabelField(v?.ToString() ?? "null", options);
            return v;
        }

        private object GetDefault(Type t)
        {
            if (t == typeof(string)) return "";
            if (t.IsValueType) return Activator.CreateInstance(t);
            return null;
        }

        private bool GetFoldout(string key) => _foldoutStates.TryGetValue(key, out var v) && v;
        private void SetFoldout(string key, bool v) => _foldoutStates[key] = v;

        private bool IsDictionary(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
        private bool IsList(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
        private bool IsPlain(Type t) => t.IsPrimitive || t == typeof(string) || t == typeof(Vector2) || t == typeof(Vector3) || typeof(UnityEngine.Object).IsAssignableFrom(t);

        private void ApplyChanges()
        {
            if (_targetMono == null) return;

            Undo.RecordObject(_targetMono, "Apply Dictionary Changes");

            // 遍历所有缓存的字典编辑数据
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var kvp in _editDataCache)
            {
                string cacheKey = kvp.Key;
                if (!cacheKey.EndsWith(".__dict_cache")) continue;

                // 解析 FieldName (非常简陋的解析，实际项目建议在 Cache 中存 FieldInfo)
                // 这里我们重新遍历字段来匹配
                foreach (var field in _targetMono.GetType().GetFields(flags))
                {
                    if (cacheKey.Contains(field.Name))
                    {
                        var list = kvp.Value as List<DictionaryEntry>;
                        if (list == null) continue;

                        object fieldVal = field.GetValue(_targetMono);
                        if (fieldVal is IDictionary dict)
                        {
                            dict.Clear();
                            foreach (var entry in list)
                            {
                                try
                                {
                                    dict.Add(entry.Key, entry.Value);
                                }
                                catch (ArgumentException)
                                {
                                    Debug.LogWarning($"[DictionarySerializer] 忽略重复 Key: {entry.Key}");
                                }
                            }
                        }
                    }
                }
            }

            EditorUtility.SetDirty(_targetMono);
            AssetDatabase.SaveAssets();
            Debug.Log("[DictionarySerializer] 修改已应用！");
        }
    }
}