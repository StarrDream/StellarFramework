using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor
{
    public class FolderContentCopyTool : EditorWindow
    {
        // 仅保留 Hub 调用入口，不再挂菜单
        public static void ShowWindow()
        {
            var wnd = GetWindow<FolderContentCopyTool>("Folder Content Copy");
            wnd.minSize = new Vector2(760, 520);
            wnd.Show();
        }

        private string _rootFolder = "";
        private Vector2 _scroll;
        private readonly List<string> _subFolders = new List<string>(128);
        private readonly HashSet<string> _selectedFolders = new HashSet<string>();
        private bool _includeCs = true;
        private bool _includeShader = true;
        private bool _includeTxt = true;
        private bool _includeJson = true;
        private bool _includeAsmdef = true;
        private bool _includeMeta = false;

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("根目录（物理路径）", EditorStyles.boldLabel);

            using (new GUILayout.HorizontalScope())
            {
                _rootFolder = EditorGUILayout.TextField(_rootFolder);
                if (GUILayout.Button("选择", GUILayout.Width(80)))
                {
                    var path = EditorUtility.OpenFolderPanel("选择根目录", _rootFolder, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _rootFolder = path;
                        RefreshSubFolders();
                        Debug.Log($"[FolderContentCopyTool] 选择根目录: {_rootFolder}");
                    }
                }

                if (GUILayout.Button("刷新", GUILayout.Width(80)))
                {
                    RefreshSubFolders();
                    Debug.Log("[FolderContentCopyTool] 刷新子目录列表");
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("包含后缀", EditorStyles.boldLabel);
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    _includeCs = EditorGUILayout.ToggleLeft(".cs", _includeCs, GUILayout.Width(80));
                    _includeAsmdef = EditorGUILayout.ToggleLeft(".asmdef", _includeAsmdef, GUILayout.Width(80));
                    _includeShader = EditorGUILayout.ToggleLeft(".shader/.cginc", _includeShader, GUILayout.Width(130));
                    _includeJson = EditorGUILayout.ToggleLeft(".json", _includeJson, GUILayout.Width(80));
                    _includeTxt = EditorGUILayout.ToggleLeft(".txt/.md", _includeTxt, GUILayout.Width(100));
                    _includeMeta = EditorGUILayout.ToggleLeft(".meta", _includeMeta, GUILayout.Width(80));
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("子目录选择（一级目录）", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _subFolders.Count; i++)
            {
                string folder = _subFolders[i];
                bool selected = _selectedFolders.Contains(folder);

                bool newSelected = EditorGUILayout.ToggleLeft(folder, selected);
                if (newSelected != selected)
                {
                    if (newSelected) _selectedFolders.Add(folder);
                    else _selectedFolders.Remove(folder);
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全选", GUILayout.Height(30)))
                {
                    _selectedFolders.Clear();
                    for (int i = 0; i < _subFolders.Count; i++)
                        _selectedFolders.Add(_subFolders[i]);
                    Debug.Log("[FolderContentCopyTool] 全选子目录");
                }

                if (GUILayout.Button("全不选", GUILayout.Height(30)))
                {
                    _selectedFolders.Clear();
                    Debug.Log("[FolderContentCopyTool] 取消所有选择");
                }

                GUI.enabled = _selectedFolders.Count > 0 && Directory.Exists(_rootFolder);
                if (GUILayout.Button("复制选中目录内容到剪贴板", GUILayout.Height(30)))
                {
                    CopySelectedFoldersToClipboard();
                }

                GUI.enabled = true;
            }
        }

        private void RefreshSubFolders()
        {
            _subFolders.Clear();
            _selectedFolders.Clear();

            if (!Directory.Exists(_rootFolder))
            {
                Debug.LogError($"[FolderContentCopyTool] 根目录不存在: {_rootFolder}");
                return;
            }

            var dirs = Directory.GetDirectories(_rootFolder);
            for (int i = 0; i < dirs.Length; i++)
            {
                _subFolders.Add(dirs[i]);
            }

            _subFolders.Sort();
            Debug.Log($"[FolderContentCopyTool] 子目录数量: {_subFolders.Count}");
        }

        private void CopySelectedFoldersToClipboard()
        {
            var exts = BuildExtensions();

            StringBuilder sb = new StringBuilder(1024 * 256);
            int fileCount = 0;

            try
            {
                int index = 0;
                foreach (var folder in _selectedFolders)
                {
                    index++;
                    EditorUtility.DisplayProgressBar("复制脚本内容", $"扫描: {folder}", (float)index / _selectedFolders.Count);

                    var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++)
                    {
                        var f = files[i];
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (!exts.Contains(ext)) continue;

                        string content = File.ReadAllText(f);
                        sb.AppendLine("=================================================");
                        sb.AppendLine(f);
                        sb.AppendLine("-------------------------------------------------");
                        sb.AppendLine(content);
                        sb.AppendLine();
                        fileCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"[FolderContentCopyTool] 复制完成：{fileCount} 个文件，已写入剪贴板");
            ShowNotification(new GUIContent($"已复制 {fileCount} 个文件"));
        }

        private HashSet<string> BuildExtensions()
        {
            var set = new HashSet<string>();
            if (_includeCs) set.Add(".cs");
            if (_includeAsmdef) set.Add(".asmdef");
            if (_includeJson) set.Add(".json");
            if (_includeMeta) set.Add(".meta");

            if (_includeTxt)
            {
                set.Add(".txt");
                set.Add(".md");
            }

            if (_includeShader)
            {
                set.Add(".shader");
                set.Add(".cginc");
                set.Add(".hlsl");
            }

            return set;
        }
    }
}