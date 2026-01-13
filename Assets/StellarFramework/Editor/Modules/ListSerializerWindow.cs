using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json; // 依赖 Newtonsoft.Json

namespace StellarFramework.Editor
{
    public class ListSerializerWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<ListSerializerWindow>("List Serializer");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        private MonoBehaviour _targetMono;
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;

        private FieldInfo _selectedField;
        private object _selectedListObject;
        private bool _isTargetArray;

        private int _currentPage = 0;
        private int _itemsPerPage = 50;

        private string _searchText = "";
        private List<int> _filteredIndices = new List<int>();
        private bool _hasPerformedSearch = false;

        // 展开状态缓存
        private Dictionary<int, bool> _foldoutCache = new Dictionary<int, bool>();
        private List<FieldInfo> _cachedListFields = new List<FieldInfo>();

        private void OnEnable()
        {
            if (Selection.activeGameObject != null)
            {
                _targetMono = Selection.activeGameObject.GetComponent<MonoBehaviour>();
                RefreshFieldCache();
            }
        }

        private void OnGUI()
        {
            DrawHeader();

            if (_targetMono == null)
            {
                EditorGUILayout.HelpBox("请选择目标组件", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            DrawContentArea();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _targetMono = (MonoBehaviour)EditorGUILayout.ObjectField("目标组件", _targetMono, typeof(MonoBehaviour), true);
                if (EditorGUI.EndChangeCheck())
                {
                    RefreshFieldCache();
                    _selectedField = null;
                    _selectedListObject = null;
                }

                if (GUILayout.Button("刷新字段缓存", GUILayout.Width(150)))
                {
                    RefreshFieldCache();
                }
            }
        }

        private void RefreshFieldCache()
        {
            _cachedListFields.Clear();
            if (_targetMono == null) return;

            Type currentType = _targetMono.GetType();

            while (currentType != null && currentType != typeof(MonoBehaviour) && currentType != typeof(object))
            {
                var fields = currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                foreach (var f in fields)
                {
                    bool isList = f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>);
                    bool isArray = f.FieldType.IsArray;
                    bool isSerializable = f.IsPublic || f.GetCustomAttribute<SerializeField>() != null;

                    // 过滤 JsonIgnore
                    if (f.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                    if ((isList || isArray) && isSerializable)
                    {
                        _cachedListFields.Add(f);
                    }
                }

                currentType = currentType.BaseType;
            }
        }

        private void DrawSidebar()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(250), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("可用集合字段", EditorStyles.boldLabel);
                _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

                foreach (var field in _cachedListFields)
                {
                    GUI.backgroundColor = (_selectedField == field) ? Color.cyan : Color.white;
                    string displayName = $"{field.Name} ({GetTypeName(field.FieldType)})";
                    if (GUILayout.Button(displayName, EditorStyles.miniButton, GUILayout.Height(24)))
                    {
                        SelectField(field);
                    }

                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private string GetTypeName(Type t)
        {
            if (t.IsArray) return $"{t.GetElementType().Name}[]";
            if (t.IsGenericType) return $"List<{t.GetGenericArguments()[0].Name}>";
            return t.Name;
        }

        private void SelectField(FieldInfo field)
        {
            _selectedField = field;
            _currentPage = 0;
            _searchText = "";
            _filteredIndices.Clear();
            _hasPerformedSearch = false;
            _foldoutCache.Clear();

            _selectedListObject = field.GetValue(_targetMono);
            _isTargetArray = field.FieldType.IsArray;

            if (_selectedListObject == null && !_isTargetArray)
            {
                _selectedListObject = Activator.CreateInstance(field.FieldType);
                field.SetValue(_targetMono, _selectedListObject);
                EditorUtility.SetDirty(_targetMono);
            }
        }

        private void DrawContentArea()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
            {
                if (_selectedField == null || _selectedListObject == null) return;

                _selectedListObject = _selectedField.GetValue(_targetMono);
                if (_selectedListObject == null) return;

                IList list = (IList)_selectedListObject;
                Type elemType = _isTargetArray ? _selectedField.FieldType.GetElementType() : _selectedField.FieldType.GetGenericArguments()[0];

                DrawToolbar(list, elemType);

                int currentTotalCount = _hasPerformedSearch ? _filteredIndices.Count : list.Count;
                int totalPages = Mathf.CeilToInt(currentTotalCount / (float)_itemsPerPage);
                if (_currentPage >= totalPages && totalPages > 0) _currentPage = totalPages - 1;

                int startIndex = _currentPage * _itemsPerPage;
                int endIndex = Mathf.Min(startIndex + _itemsPerPage, currentTotalCount);

                _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
                for (int i = startIndex; i < endIndex; i++)
                {
                    int realIndex = _hasPerformedSearch ? _filteredIndices[i] : i;
                    if (realIndex >= list.Count) continue;

                    DrawElement(list, realIndex, elemType);
                }

                EditorGUILayout.EndScrollView();

                DrawPaginationControls(totalPages);
            }
        }

        private void DrawToolbar(IList list, Type elemType)
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUI.SetNextControlName("SearchField");
                _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(200));

                if (GUILayout.Button("搜索", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    PerformDeepSearch(list);
                    GUI.FocusControl(null);
                }


                if (_hasPerformedSearch && GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    _searchText = "";
                    _hasPerformedSearch = false;
                    _filteredIndices.Clear();
                    GUI.FocusControl(null);
                }

                GUILayout.FlexibleSpace();

                string countInfo = _hasPerformedSearch ? $"{_filteredIndices.Count}/{list.Count}" : $"{list.Count}";
                EditorGUILayout.LabelField($"Count: {countInfo}", GUILayout.Width(80));

                if (!_isTargetArray)
                {
                    if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    {
                        Undo.RecordObject(_targetMono, "Add Item");
                        list.Add(CreateDefault(elemType));
                        EditorUtility.SetDirty(_targetMono);
                        if (_hasPerformedSearch) PerformDeepSearch(list);
                    }
                }
            }
        }

        private void DrawElement(IList list, int index, Type elemType)
        {
            using (new GUILayout.VerticalScope("box"))
            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"[{index}]", GUILayout.Width(40));

                    object oldVal = list[index];

                    if (IsSimpleType(elemType))
                    {
                        EditorGUI.BeginChangeCheck();
                        object newVal = DrawSimpleType(oldVal, elemType);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(_targetMono, "Edit Value");
                            list[index] = newVal;
                            EditorUtility.SetDirty(_targetMono);
                        }
                    }
                    else
                    {
                        int hash = oldVal != null ? oldVal.GetHashCode() : index;
                        if (!_foldoutCache.TryGetValue(hash, out bool isExpanded)) isExpanded = false;

                        string typeName = oldVal == null ? "null" : oldVal.GetType().Name;
                        bool newExpanded = EditorGUILayout.Foldout(isExpanded, typeName, true);
                        if (newExpanded != isExpanded) _foldoutCache[hash] = newExpanded;

                        if (GUILayout.Button("Copy JSON", EditorStyles.miniButton, GUILayout.Width(80)))
                        {
                            CopyJsonNewtonsoft(oldVal);
                        }
                    }

                    if (!_isTargetArray)
                    {
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            Undo.RecordObject(_targetMono, "Remove Item");
                            list.RemoveAt(index);
                            EditorUtility.SetDirty(_targetMono);
                            if (_hasPerformedSearch) PerformDeepSearch(list);
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                if (!IsSimpleType(elemType) && list[index] != null)
                {
                    int hash = list[index].GetHashCode();
                    if (_foldoutCache.TryGetValue(hash, out bool expanded) && expanded)
                    {
                        EditorGUI.indentLevel++;
                        DrawRecursiveObject(list[index], list[index].GetType(), 0);
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }

        // --- 核心：递归绘制对象 ---
        private void DrawRecursiveObject(object obj, Type type, int depth)
        {
            if (obj == null || depth > 6) return;

            Type currentType = type;
            bool hasDrawnAnyMember = false;

            while (currentType != null && currentType != typeof(object) && currentType != typeof(UnityEngine.Object))
            {
                // 1. 字段
                var fields = currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var f in fields)
                {
                    if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                    if (f.Name.Contains("k__BackingField")) continue;

                    // 过滤 JsonIgnore
                    if (f.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                    hasDrawnAnyMember = true;
                    object val = f.GetValue(obj);

                    if (IsListOrArray(f.FieldType))
                    {
                        DrawSubList(val as IList, f.Name, f.FieldType, depth);
                    }
                    else
                    {
                        DrawMember(obj, f.FieldType, f.Name, val, (v) => f.SetValue(obj, v), depth);
                    }
                }

                // 2. 属性
                var props = currentType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var p in props)
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;

                    // 过滤 JsonIgnore
                    if (p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                    hasDrawnAnyMember = true;
                    object val = null;
                    try
                    {
                        val = p.GetValue(obj);
                    }
                    catch
                    {
                        val = "Error";
                    }

                    Action<object> setter = null;
                    if (p.CanWrite && p.GetSetMethod(true) != null) setter = (v) => p.SetValue(obj, v);

                    if (IsListOrArray(p.PropertyType))
                    {
                        DrawSubList(val as IList, $"{p.Name} (Prop)", p.PropertyType, depth);
                    }
                    else
                    {
                        DrawMember(obj, p.PropertyType, $"{p.Name} (Prop)", val, setter, depth);
                    }
                }

                currentType = currentType.BaseType;
            }

            if (!hasDrawnAnyMember && depth == 0)
            {
                EditorGUILayout.HelpBox($"未检测到数据 (Type: {type.Name})", MessageType.Warning);
            }
        }

        // --- 修复后的 DrawSubList ---
        private void DrawSubList(IList list, string label, Type listType, int depth)
        {
            if (list == null)
            {
                EditorGUILayout.LabelField(label, "null");
                return;
            }

            Type elementType = listType.IsArray
                ? listType.GetElementType()
                : listType.GetGenericArguments()[0];

            int count = list.Count;
            int hash = list.GetHashCode();

            // 获取当前展开状态，默认为 false
            if (!_foldoutCache.TryGetValue(hash, out bool expanded)) expanded = false;

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    // 绘制 Foldout
                    bool newExpanded = EditorGUILayout.Foldout(expanded, $"{label} [{count}]", true);

                    // 修复点：直接更新缓存，不进行奇怪的逻辑判断
                    if (newExpanded != expanded)
                    {
                        _foldoutCache[hash] = newExpanded;
                        expanded = newExpanded;
                    }

                    if (!listType.IsArray && GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        Undo.RecordObject(_targetMono, "Add Sub Item");
                        list.Add(CreateDefault(elementType));
                        EditorUtility.SetDirty(_targetMono);
                    }
                }

                if (expanded)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < list.Count; i++)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"Element {i}", GUILayout.Width(70));
                            object item = list[i];

                            if (IsSimpleType(elementType))
                            {
                                EditorGUI.BeginChangeCheck();
                                object newVal = DrawSimpleType(item, elementType);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(_targetMono, "Edit Sub Value");
                                    list[i] = newVal;
                                    EditorUtility.SetDirty(_targetMono);
                                }
                            }
                            else
                            {
                                using (new GUILayout.VerticalScope("box"))
                                {
                                    DrawRecursiveObject(item, elementType, depth + 1);
                                }
                            }

                            if (!listType.IsArray && GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(20)))
                            {
                                Undo.RecordObject(_targetMono, "Remove Sub Item");
                                list.RemoveAt(i);
                                EditorUtility.SetDirty(_targetMono);
                                i--;
                            }
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawMember(object obj, Type type, string label, object val, Action<object> setValue, int depth)
        {
            if (IsSimpleType(type))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.BeginDisabledGroup(setValue == null);
                object newVal = DrawSimpleType(val, type, label);
                EditorGUI.EndDisabledGroup();

                if (EditorGUI.EndChangeCheck() && setValue != null)
                {
                    Undo.RecordObject(_targetMono, "Edit Member");
                    setValue(newVal);
                    EditorUtility.SetDirty(_targetMono);
                }
            }
            else
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                if (val == null)
                {
                    if (setValue != null && GUILayout.Button($"Create {type.Name}"))
                    {
                        try
                        {
                            setValue(Activator.CreateInstance(type));
                        }
                        catch
                        {
                            Debug.LogError($"无法实例化 {type.Name}");
                        }
                    }
                    else if (setValue == null)
                    {
                        EditorGUILayout.LabelField("null (Read Only)");
                    }
                }
                else
                {
                    DrawRecursiveObject(val, type, depth + 1);
                }

                EditorGUI.indentLevel--;
            }
        }

        // --- 辅助方法 ---

        private bool IsListOrArray(Type t)
        {
            return (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) || t.IsArray;
        }

        private bool IsSimpleType(Type t)
        {
            return t.IsPrimitive || t == typeof(string) || t.IsEnum ||
                   t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Color) ||
                   typeof(UnityEngine.Object).IsAssignableFrom(t);
        }

        private object DrawSimpleType(object val, Type t, string label = null)
        {
            if (label == null) label = "";

            if (t == typeof(int)) return EditorGUILayout.IntField(label, (int)(val ?? 0));
            if (t == typeof(float)) return EditorGUILayout.FloatField(label, (float)(val ?? 0f));
            if (t == typeof(string)) return EditorGUILayout.TextField(label, (string)(val ?? ""));
            if (t == typeof(bool)) return EditorGUILayout.Toggle(label, (bool)(val ?? false));
            if (t == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, (Vector2)(val ?? Vector2.zero));
            if (t == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, (Vector3)(val ?? Vector3.zero));
            if (t == typeof(Color)) return EditorGUILayout.ColorField(label, (Color)(val ?? Color.white));
            if (t.IsEnum) return EditorGUILayout.EnumPopup(label, (Enum)(val ?? Activator.CreateInstance(t)));
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                return EditorGUILayout.ObjectField(label, (UnityEngine.Object)val, t, true);

            EditorGUILayout.LabelField(label, val?.ToString());
            return val;
        }

        private object CreateDefault(Type t)
        {
            if (t == typeof(string)) return "";
            if (t.IsValueType) return Activator.CreateInstance(t);
            try
            {
                return Activator.CreateInstance(t);
            }
            catch
            {
                return null;
            }
        }

        // --- 深度搜索逻辑 ---
        private void PerformDeepSearch(IList list)
        {
            _filteredIndices.Clear();
            _currentPage = 0;
            if (string.IsNullOrEmpty(_searchText))
            {
                _hasPerformedSearch = false;
                return;
            }

            _hasPerformedSearch = true;
            string searchLower = _searchText.ToLower();

            for (int i = 0; i < list.Count; i++)
            {
                if (IsObjectMatchRecursive(list[i], searchLower, 0)) _filteredIndices.Add(i);
            }
        }

        private bool IsObjectMatchRecursive(object obj, string searchLower, int depth)
        {
            if (obj == null || depth > 5) return false;
            Type type = obj.GetType();

            if (IsSimpleType(type)) return obj.ToString().IndexOf(searchLower, StringComparison.OrdinalIgnoreCase) >= 0;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var unityObj = obj as UnityEngine.Object;
                return unityObj != null && unityObj.name.IndexOf(searchLower, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // 搜索字段
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var f in fields)
            {
                if (f.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                object val = f.GetValue(obj);
                if (val != null)
                {
                    if (IsListOrArray(f.FieldType))
                    {
                        IList list = val as IList;
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                if (IsObjectMatchRecursive(item, searchLower, depth + 1)) return true;
                            }
                        }
                    }
                    else if (IsObjectMatchRecursive(val, searchLower, depth + 1)) return true;
                }
            }

            // 搜索属性
            var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var p in props)
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                if (p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                try
                {
                    object val = p.GetValue(obj);
                    if (val != null)
                    {
                        if (IsListOrArray(p.PropertyType))
                        {
                            IList list = val as IList;
                            if (list != null)
                            {
                                foreach (var item in list)
                                {
                                    if (IsObjectMatchRecursive(item, searchLower, depth + 1)) return true;
                                }
                            }
                        }
                        else if (IsObjectMatchRecursive(val, searchLower, depth + 1)) return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private void CopyJsonNewtonsoft(object obj)
        {
            if (obj == null) return;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    TypeNameHandling = TypeNameHandling.Auto
                };
                string json = JsonConvert.SerializeObject(obj, settings);
                GUIUtility.systemCopyBuffer = json;
                Debug.Log($"[ListSerializer] 已复制 JSON");
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON Error: {e.Message}");
            }
        }

        private void DrawPaginationControls(int totalPages)
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("<<", EditorStyles.toolbarButton, GUILayout.Width(30))) _currentPage = 0;
                if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(30))) _currentPage--;
                EditorGUILayout.LabelField($"{_currentPage + 1}/{Mathf.Max(1, totalPages)}", EditorStyles.centeredGreyMiniLabel);
                if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(30))) _currentPage++;
                if (GUILayout.Button(">>", EditorStyles.toolbarButton, GUILayout.Width(30))) _currentPage = totalPages - 1;
                GUILayout.FlexibleSpace();
            }
        }
    }
}