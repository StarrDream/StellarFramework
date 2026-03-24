using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor
{
    [InitializeOnLoad]
    public static class ConfigKitWindowInjector
    {
        static ConfigKitWindowInjector()
        {
            EditorApplication.update += InjectDelegates;
        }

        private static void InjectDelegates()
        {
            if (!EditorWindow.HasOpenInstances<ConfigKitWindow>()) return;
            var window = EditorWindow.GetWindow<ConfigKitWindow>("", false);
            if (window == null) return;

            if (window.OnDrawNormalConfigPanel == null)
            {
                window.OnDrawNormalConfigPanel = ConfigKitDataEditor.DrawNormalConfig;
            }

            if (window.OnDrawNetConfigPanel == null)
            {
                window.OnDrawNetConfigPanel = ConfigKitDataEditor.DrawNetConfig;
            }
        }
    }

    /// <summary>
    /// 配置数据渲染器 (双工作区适配版)
    /// 职责: 解析 JSON 数据并绘制对应的 IMGUI 面板，支持递归树状编辑与 Raw 文本编辑
    /// </summary>
    public static class ConfigKitDataEditor
    {
        private static string _currentPath;
        private static JObject _currentJson;

        // 模式控制
        private static bool _isRawMode = false;
        private static string _rawJsonText = "";

        // 树状视图折叠状态缓存
        private static readonly Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();

        // NormalConfig 新增节点状态
        private static string _newNodeKey = "";
        private static int _newNodeType = 0; // 0:String, 1:Int, 2:Float, 3:Bool, 4:Object, 5:Array
        private static readonly string[] _nodeTypeNames = { "String", "Int", "Float", "Bool", "Object", "Array" };

        // NetConfig 新增节点状态
        private static string _newServiceName = "";
        private static string _newEndpointName = "";

        private static void EnsureLoaded(string path)
        {
            if (_currentPath == path && _currentJson != null) return;

            _currentPath = path;
            _currentJson = null;
            _rawJsonText = "";
            _foldoutStates.Clear();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError($"[ConfigKitDataEditor] 加载失败: 路径为空或文件不存在 ({path})");
                return;
            }

            try
            {
                _rawJsonText = File.ReadAllText(path, Encoding.UTF8);
                _currentJson = JObject.Parse(_rawJsonText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigKitDataEditor] JSON 解析异常: {path}\n{ex.Message}");
                _currentJson = new JObject();
                _rawJsonText = "{}";
            }
        }

        private static void SaveCurrent()
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                Debug.LogError("[ConfigKitDataEditor] 保存失败: 当前路径丢失");
                return;
            }

            try
            {
                if (_isRawMode)
                {
                    // Raw 模式下先校验 JSON 格式
                    _currentJson = JObject.Parse(_rawJsonText);
                }
                else
                {
                    // 树状模式下，将当前 JObject 同步回 raw 文本
                    _rawJsonText = _currentJson.ToString(Newtonsoft.Json.Formatting.Indented);
                }

                File.WriteAllText(_currentPath, _rawJsonText, new UTF8Encoding(false));

                // 核心适配：只有在 StreamingAssets (项目内) 时才调用 Refresh
                // PersistentDataPath 通常在 C 盘 AppData 下，调用 Refresh 无效且可能报错
                if (_currentPath.Replace("\\", "/").Contains("Assets/StreamingAssets"))
                {
                    AssetDatabase.Refresh();
                }

                Debug.Log($"[ConfigKitDataEditor] 保存成功: {_currentPath}");
            }
            catch (Newtonsoft.Json.JsonReaderException ex)
            {
                Debug.LogError($"[ConfigKitDataEditor] JSON 格式错误，保存被拦截: \n{ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfigKitDataEditor] 保存异常: {_currentPath}\n{ex.Message}");
            }
        }

        private static bool GetFoldout(string path)
        {
            if (_foldoutStates.TryGetValue(path, out bool val)) return val;
            return false; // 默认折叠
        }

        private static void SetFoldout(string path, bool val)
        {
            _foldoutStates[path] = val;
        }

        #region Normal Config 渲染逻辑

        public static void DrawNormalConfig(string path)
        {
            EnsureLoaded(path);
            if (_currentJson == null) return;

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label($"编辑普通配置: {Path.GetFileName(path)}", EditorStyles.largeLabel);
                GUILayout.FlexibleSpace();
                _isRawMode = GUILayout.Toggle(_isRawMode, _isRawMode ? "切换到树状视图" : "切换到文本视图", "Button",
                    GUILayout.Width(120), GUILayout.Height(24));
            }

            // 显示当前文件的绝对路径，方便开发者确认位置
            GUILayout.Label($"路径: {path}", EditorStyles.miniLabel);
            GUILayout.Space(10);

            if (_isRawMode)
            {
                DrawRawMode();
            }
            else
            {
                DrawTreeMode();
            }

            GUILayout.Space(20);
            GUI.backgroundColor = new Color(0.22f, 0.52f, 0.88f); // AccentDark
            if (GUILayout.Button("保存配置 (Save)", GUILayout.Height(34)))
            {
                SaveCurrent();
            }

            GUI.backgroundColor = Color.white;
        }

        private static void DrawRawMode()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Raw JSON 编辑 (支持直接复制粘贴)", EditorStyles.boldLabel);
            _rawJsonText = EditorGUILayout.TextArea(_rawJsonText, GUILayout.MinHeight(400));
            GUILayout.EndVertical();
        }

        private static void DrawTreeMode()
        {
            GUILayout.BeginVertical("box");
            DrawJTokenTree("Root", _currentJson, "root");
            GUILayout.EndVertical();

            GUILayout.Space(10);
            DrawAddNodeUI(_currentJson);
        }

        /// <summary>
        /// 递归绘制 JSON 树状结构
        /// </summary>
        private static void DrawJTokenTree(string key, JToken token, string path)
        {
            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                bool fold = GetFoldout(path);

                using (new GUILayout.HorizontalScope())
                {
                    fold = EditorGUILayout.Foldout(fold, $"{key} {{}}", true, EditorStyles.foldoutHeader);
                    SetFoldout(path, fold);
                }

                if (fold)
                {
                    EditorGUI.indentLevel++;
                    List<string> keysToRemove = new List<string>();
                    foreach (var prop in obj.Properties())
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            if (prop.Value.Type == JTokenType.Object || prop.Value.Type == JTokenType.Array)
                            {
                                GUILayout.BeginVertical();
                                DrawJTokenTree(prop.Name, prop.Value, path + "." + prop.Name);
                                GUILayout.EndVertical();

                                GUI.backgroundColor = new Color(0.9f, 0.25f, 0.25f);
                                if (GUILayout.Button("X", GUILayout.Width(20))) keysToRemove.Add(prop.Name);
                                GUI.backgroundColor = Color.white;
                            }
                            else
                            {
                                GUILayout.Label(prop.Name, GUILayout.Width(150));
                                DrawJValue((JValue)prop.Value);

                                GUI.backgroundColor = new Color(0.9f, 0.25f, 0.25f);
                                if (GUILayout.Button("X", GUILayout.Width(20))) keysToRemove.Add(prop.Name);
                                GUI.backgroundColor = Color.white;
                            }
                        }
                    }

                    foreach (var k in keysToRemove) obj.Remove(k);
                    EditorGUI.indentLevel--;
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                var arr = (JArray)token;
                bool fold = GetFoldout(path);

                using (new GUILayout.HorizontalScope())
                {
                    fold = EditorGUILayout.Foldout(fold, $"{key} [{arr.Count}]", true, EditorStyles.foldoutHeader);
                    SetFoldout(path, fold);
                }

                if (fold)
                {
                    EditorGUI.indentLevel++;
                    List<int> indicesToRemove = new List<int>();
                    for (int i = 0; i < arr.Count; i++)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            if (arr[i].Type == JTokenType.Object || arr[i].Type == JTokenType.Array)
                            {
                                GUILayout.BeginVertical();
                                DrawJTokenTree($"[{i}]", arr[i], path + $"[{i}]");
                                GUILayout.EndVertical();

                                GUI.backgroundColor = new Color(0.9f, 0.25f, 0.25f);
                                if (GUILayout.Button("X", GUILayout.Width(20))) indicesToRemove.Add(i);
                                GUI.backgroundColor = Color.white;
                            }
                            else
                            {
                                GUILayout.Label($"[{i}]", GUILayout.Width(150));
                                DrawJValue((JValue)arr[i]);

                                GUI.backgroundColor = new Color(0.9f, 0.25f, 0.25f);
                                if (GUILayout.Button("X", GUILayout.Width(20))) indicesToRemove.Add(i);
                                GUI.backgroundColor = Color.white;
                            }
                        }
                    }

                    for (int i = indicesToRemove.Count - 1; i >= 0; i--) arr.RemoveAt(indicesToRemove[i]);

                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(15);
                        if (GUILayout.Button("+ Add Element", EditorStyles.miniButton, GUILayout.Width(100)))
                        {
                            arr.Add("");
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        private static void DrawJValue(JValue jval)
        {
            if (jval.Type == JTokenType.Integer)
            {
                long val = EditorGUILayout.LongField(jval.Value<long>());
                if (val != jval.Value<long>()) jval.Value = val;
            }
            else if (jval.Type == JTokenType.Float)
            {
                double val = EditorGUILayout.DoubleField(jval.Value<double>());
                if (Math.Abs(val - jval.Value<double>()) > 0.000001) jval.Value = val;
            }
            else if (jval.Type == JTokenType.Boolean)
            {
                bool val = EditorGUILayout.Toggle(jval.Value<bool>());
                if (val != jval.Value<bool>()) jval.Value = val;
            }
            else
            {
                string val = EditorGUILayout.TextField(jval.ToString());
                if (val != jval.ToString()) jval.Value = val;
            }
        }

        private static void DrawAddNodeUI(JObject root)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("在根节点新增字段", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Key:", GUILayout.Width(40));
            _newNodeKey = EditorGUILayout.TextField(_newNodeKey, GUILayout.Width(120));
            GUILayout.Label("Type:", GUILayout.Width(40));
            _newNodeType = EditorGUILayout.Popup(_newNodeType, _nodeTypeNames, GUILayout.Width(80));

            if (GUILayout.Button("添加", GUILayout.Width(60)))
            {
                if (string.IsNullOrWhiteSpace(_newNodeKey))
                {
                    Debug.LogError("[ConfigKit] Key 不能为空");
                    return;
                }

                if (root.Property(_newNodeKey) != null)
                {
                    Debug.LogError($"[ConfigKit] Key '{_newNodeKey}' 已存在");
                    return;
                }

                switch (_newNodeType)
                {
                    case 0: root[_newNodeKey] = ""; break;
                    case 1: root[_newNodeKey] = 0; break;
                    case 2: root[_newNodeKey] = 0f; break;
                    case 3: root[_newNodeKey] = false; break;
                    case 4: root[_newNodeKey] = new JObject(); break;
                    case 5: root[_newNodeKey] = new JArray(); break;
                }

                _newNodeKey = "";
                GUI.FocusControl(null);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        #endregion

        #region Net Config 渲染逻辑

        public static void DrawNetConfig(string path)
        {
            EnsureLoaded(path);
            if (_currentJson == null) return;

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label($"编辑网络配置: {Path.GetFileName(path)}", EditorStyles.largeLabel);
                GUILayout.FlexibleSpace();
                _isRawMode = GUILayout.Toggle(_isRawMode, _isRawMode ? "切换到视图模式" : "切换到文本模式", "Button",
                    GUILayout.Width(120), GUILayout.Height(24));
            }

            GUILayout.Label($"路径: {path}", EditorStyles.miniLabel);
            GUILayout.Space(10);

            if (_isRawMode)
            {
                DrawRawMode();
            }
            else
            {
                DrawGlobalEnvSelector();
                GUILayout.Space(10);
                DrawEnvironmentsBlock();
                GUILayout.Space(10);
                DrawEndpointsBlock();
            }

            GUILayout.Space(20);
            GUI.backgroundColor = new Color(0.22f, 0.52f, 0.88f);
            if (GUILayout.Button("保存配置 (Save)", GUILayout.Height(34)))
            {
                SaveCurrent();
            }

            GUI.backgroundColor = Color.white;
        }

        private static void DrawGlobalEnvSelector()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("全局环境控制 (影响所有 NetConfig)", EditorStyles.boldLabel);
            int currentEnvInt = EditorPrefs.GetInt("ConfigKit_Global_Env", (int)UrlEnvironment.Dev);
            UrlEnvironment currentEnv = (UrlEnvironment)currentEnvInt;
            UrlEnvironment newEnv = (UrlEnvironment)EditorGUILayout.EnumPopup("当前测试环境", currentEnv);
            if (newEnv != currentEnv)
            {
                EditorPrefs.SetInt("ConfigKit_Global_Env", (int)newEnv);
                Debug.Log($"[ConfigKitDataEditor] 全局环境已切换为: {newEnv}");
            }

            GUILayout.EndVertical();
        }

        private static void DrawEnvironmentsBlock()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("环境与服务节点 (Environments)", EditorStyles.boldLabel);

            JObject envs = _currentJson["Environments"] as JObject;
            if (envs == null)
            {
                envs = new JObject();
                _currentJson["Environments"] = envs;
            }

            if (envs["Dev"] == null) envs["Dev"] = new JObject();
            if (envs["Release"] == null) envs["Release"] = new JObject();

            DrawSingleEnvBlock("Dev", envs["Dev"] as JObject);
            GUILayout.Space(5);
            DrawSingleEnvBlock("Release", envs["Release"] as JObject);

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            _newServiceName = EditorGUILayout.TextField("新增 Service 名称:", _newServiceName);
            if (GUILayout.Button("添加至所有环境", GUILayout.Width(120)))
            {
                if (string.IsNullOrWhiteSpace(_newServiceName)) return;
                foreach (var prop in envs.Properties())
                {
                    JObject node = prop.Value as JObject;
                    if (node != null && node.Property(_newServiceName) == null)
                    {
                        node[_newServiceName] = "http://127.0.0.1";
                    }
                }

                _newServiceName = "";
                GUI.FocusControl(null);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static void DrawSingleEnvBlock(string envName, JObject envNode)
        {
            if (envNode == null) return;
            GUILayout.Label($"[{envName}]", EditorStyles.miniBoldLabel);

            List<string> keysToRemove = new List<string>();
            foreach (var prop in envNode.Properties())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(prop.Name, GUILayout.Width(150));
                string val = EditorGUILayout.TextField(prop.Value.ToString());
                if (val != prop.Value.ToString()) envNode[prop.Name] = val;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    keysToRemove.Add(prop.Name);
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            foreach (var k in keysToRemove)
            {
                envNode.Remove(k);
            }
        }

        private static void DrawEndpointsBlock()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("路由接口 (Endpoints)", EditorStyles.boldLabel);

            JObject endpoints = _currentJson["Endpoints"] as JObject;
            if (endpoints == null)
            {
                endpoints = new JObject();
                _currentJson["Endpoints"] = endpoints;
            }

            List<string> keysToRemove = new List<string>();

            GUILayout.BeginHorizontal();
            GUILayout.Label("接口名 (Key)", EditorStyles.miniBoldLabel, GUILayout.Width(150));
            GUILayout.Label("绑定 Service", EditorStyles.miniBoldLabel, GUILayout.Width(120));
            GUILayout.Label("路由路径 (Path)", EditorStyles.miniBoldLabel);
            GUILayout.EndHorizontal();

            foreach (var prop in endpoints.Properties())
            {
                JObject epNode = prop.Value as JObject;
                if (epNode == null) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label(prop.Name, GUILayout.Width(150));
                string svc = epNode["Service"]?.ToString() ?? "";
                string pth = epNode["Path"]?.ToString() ?? "";

                string newSvc = EditorGUILayout.TextField(svc, GUILayout.Width(120));
                string newPth = EditorGUILayout.TextField(pth);

                if (newSvc != svc) epNode["Service"] = newSvc;
                if (newPth != pth) epNode["Path"] = newPth;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    keysToRemove.Add(prop.Name);
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            foreach (var k in keysToRemove)
            {
                endpoints.Remove(k);
            }

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            _newEndpointName = EditorGUILayout.TextField("新增 Endpoint 名称:", _newEndpointName);
            if (GUILayout.Button("添加路由", GUILayout.Width(120)))
            {
                if (string.IsNullOrWhiteSpace(_newEndpointName)) return;
                if (endpoints.Property(_newEndpointName) != null)
                {
                    Debug.LogError($"[ConfigKitDataEditor] 添加失败: 路由 '{_newEndpointName}' 已存在");
                    return;
                }

                endpoints[_newEndpointName] = new JObject
                {
                    ["Service"] = "",
                    ["Path"] = "/"
                };
                _newEndpointName = "";
                GUI.FocusControl(null);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        #endregion
    }
}