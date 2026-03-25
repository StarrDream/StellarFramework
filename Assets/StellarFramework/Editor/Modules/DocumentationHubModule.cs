using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// 文档中心模块 (Markdown 增强渲染版)
    /// 自动扫描框架目录下的所有 Markdown 文件，提供集成的、原生级的富文本阅读体验
    /// </summary>
    [StellarTool("文档中心 (Docs)", "框架核心", -999)]
    public class DocumentationHubModule : ToolModule
    {
        public override string Icon => "d_TextAsset Icon";
        public override string Description => "统一管理与查阅框架内所有 Markdown 文档 (支持富文本排版与代码块高亮)。";

        private List<string> _docPaths = new List<string>();
        private string _selectedDocPath = "";
        private string _docContent = "";
        
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        // --- Markdown 解析缓存 ---
        private enum BlockType { Paragraph, Header1, Header2, Header3, Code, Quote, List, Table, HR }
        private class MarkdownBlock
        {
            public BlockType Type;
            public string Content;
        }
        private readonly List<MarkdownBlock> _parsedBlocks = new List<MarkdownBlock>();

        // --- GUI 样式缓存 ---
        private bool _mdStylesInitialized;
        private GUIStyle _h1Style;
        private GUIStyle _h2Style;
        private GUIStyle _h3Style;
        private GUIStyle _pStyle;
        private GUIStyle _codeBoxStyle;
        private GUIStyle _codeTextStyle;
        private GUIStyle _quoteStyle;
        private GUIStyle _listStyle;
        private GUIStyle _hrStyle;

        public override void OnEnable()
        {
            RefreshDocs();
        }

        private void RefreshDocs()
        {
            _docPaths.Clear();
            string rootPath = Application.dataPath + "/StellarFramework";
            if (Directory.Exists(rootPath))
            {
                string[] files = Directory.GetFiles(rootPath, "*.md", SearchOption.AllDirectories);
                _docPaths.AddRange(files.Select(f => f.Replace("\\", "/")).OrderBy(f => Path.GetFileName(f)));
            }
        }

        public override void OnGUI()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("刷新文档列表", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    RefreshDocs();
                    if (!string.IsNullOrEmpty(_selectedDocPath) && File.Exists(_selectedDocPath))
                    {
                        _docContent = File.ReadAllText(_selectedDocPath);
                        ParseMarkdown(_docContent);
                    }
                }
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            using (new GUILayout.VerticalScope("box", GUILayout.Width(260), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Label($"文档列表 ({_docPaths.Count})", EditorStyles.boldLabel);
                GUILayout.Space(5);
                
                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                foreach (var path in _docPaths)
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    bool isSelected = _selectedDocPath == path;
                    
                    var oldColor = GUI.backgroundColor;
                    if (isSelected) GUI.backgroundColor = new Color(0.22f, 0.52f, 0.88f);
                    
                    if (GUILayout.Button(fileName, Window.SidebarButtonStyle))
                    {
                        if (_selectedDocPath != path)
                        {
                            _selectedDocPath = path;
                            _docContent = File.ReadAllText(path);
                            ParseMarkdown(_docContent); // 选中时触发解析，缓存结构
                            _rightScroll = Vector2.zero;
                            GUI.FocusControl(null);
                        }
                    }
                    GUI.backgroundColor = oldColor;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRightPanel()
        {
            using (new GUILayout.VerticalScope("box", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
            {
                if (string.IsNullOrEmpty(_selectedDocPath))
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("请在左侧选择要查阅的文档", EditorStyles.largeLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                    return;
                }

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(Path.GetFileName(_selectedDocPath), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("在外部编辑器打开", GUILayout.Width(120), GUILayout.Height(24)))
                    {
                        EditorUtility.OpenWithDefaultApp(_selectedDocPath);
                    }
                }
                GUILayout.Space(10);

                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                
                EnsureStyles();

                // 遍历渲染解析好的 Markdown 块
                foreach (var block in _parsedBlocks)
                {
                    switch (block.Type)
                    {
                        case BlockType.Header1:
                            GUILayout.Label(block.Content, _h1Style);
                            break;
                        case BlockType.Header2:
                            GUILayout.Label(block.Content, _h2Style);
                            break;
                        case BlockType.Header3:
                            GUILayout.Label(block.Content, _h3Style);
                            break;
                        case BlockType.Paragraph:
                            if (!string.IsNullOrEmpty(block.Content))
                                GUILayout.Label(block.Content, _pStyle);
                            else
                                GUILayout.Space(8); // 空行作为段落间距
                            break;
                        case BlockType.Quote:
                            using (new GUILayout.HorizontalScope())
                            {
                                GUILayout.Box("", GUILayout.Width(4), GUILayout.ExpandHeight(true)); // 左侧竖线
                                GUILayout.Label(block.Content, _quoteStyle);
                            }
                            break;
                        case BlockType.List:
                            GUILayout.Label(block.Content, _listStyle);
                            break;
                        case BlockType.Code:
                        case BlockType.Table:
                            using (new GUILayout.VerticalScope(_codeBoxStyle))
                            {
                                // 使用 TextArea 保证代码可被选中和复制，同时禁用富文本以防尖括号被吞
                                EditorGUILayout.TextArea(block.Content, _codeTextStyle);
                            }
                            break;
                        case BlockType.HR:
                            GUILayout.Box("", _hrStyle, GUILayout.ExpandWidth(true), GUILayout.Height(2));
                            break;
                    }
                }

                GUILayout.Space(20);
                EditorGUILayout.EndScrollView();
            }
        }

        #region Markdown 解析核心逻辑

        private void ParseMarkdown(string rawText)
        {
            _parsedBlocks.Clear();
            if (string.IsNullOrEmpty(rawText)) return;

            string[] lines = rawText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool inCodeBlock = false;
            StringBuilder codeBuilder = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                // 1. 代码块开关
                if (trimmed.StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Code, Content = codeBuilder.ToString().TrimEnd() });
                        codeBuilder.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        inCodeBlock = true;
                    }
                    continue;
                }

                // 2. 代码块内容收集
                if (inCodeBlock)
                {
                    codeBuilder.AppendLine(line);
                    continue;
                }

                // 3. 分割线
                if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.HR, Content = "" });
                    continue;
                }

                // 4. 标题 (H1 ~ H3)
                if (line.StartsWith("# ")) { _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Header1, Content = ParseInline(line.Substring(2)) }); continue; }
                if (line.StartsWith("## ")) { _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Header2, Content = ParseInline(line.Substring(3)) }); continue; }
                if (line.StartsWith("### ")) { _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Header3, Content = ParseInline(line.Substring(4)) }); continue; }
                if (line.StartsWith("#### ")) { _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Header3, Content = ParseInline(line.Substring(5)) }); continue; } // H4 降级为 H3 显示

                // 5. 引用区块
                if (line.StartsWith("> "))
                {
                    string quoteContent = ParseInline(line.Substring(2));
                    if (_parsedBlocks.Count > 0 && _parsedBlocks[_parsedBlocks.Count - 1].Type == BlockType.Quote)
                    {
                        _parsedBlocks[_parsedBlocks.Count - 1].Content += "\n" + quoteContent;
                    }
                    else
                    {
                        _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Quote, Content = quoteContent });
                    }
                    continue;
                }

                // 6. 简易表格识别 (渲染为等宽文本块以保持对齐)
                if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
                {
                    if (_parsedBlocks.Count > 0 && _parsedBlocks[_parsedBlocks.Count - 1].Type == BlockType.Table)
                    {
                        _parsedBlocks[_parsedBlocks.Count - 1].Content += "\n" + trimmed;
                    }
                    else
                    {
                        _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Table, Content = trimmed });
                    }
                    continue;
                }

                // 7. 列表项
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.List, Content = "• " + ParseInline(trimmed.Substring(2)) });
                    continue;
                }
                if (Regex.IsMatch(trimmed, @"^\d+\.\s"))
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.List, Content = ParseInline(trimmed) });
                    continue;
                }

                // 8. 空行 (段落分隔)
                if (string.IsNullOrEmpty(trimmed))
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Paragraph, Content = "" });
                    continue;
                }

                // 9. 常规段落 (自动合并相邻行)
                if (_parsedBlocks.Count > 0 && _parsedBlocks[_parsedBlocks.Count - 1].Type == BlockType.Paragraph && !string.IsNullOrEmpty(_parsedBlocks[_parsedBlocks.Count - 1].Content))
                {
                    _parsedBlocks[_parsedBlocks.Count - 1].Content += " " + ParseInline(trimmed);
                }
                else
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Paragraph, Content = ParseInline(trimmed) });
                }
            }

            // 兜底：未闭合的代码块
            if (inCodeBlock)
            {
                _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Code, Content = codeBuilder.ToString().TrimEnd() });
            }
        }

        private string ParseInline(string text)
        {
            // 防御性转义：防止 C# 泛型 <T> 被 Unity 误认为富文本标签而导致文本丢失
            // 插入零宽字符 \u200B 破坏标签结构，但视觉上不可见
            text = text.Replace("<", "<\u200B");

            // 图片: ![alt](url) -> 降级显示为文字提示
            text = Regex.Replace(text, @"\!\[(.*?)\]\((.*?)\)", "<color=#4ec9b0>[图片: $1]</color>");
            
            // 链接: [text](url)
            text = Regex.Replace(text, @"\[(.*?)\]\((.*?)\)", "<color=#569cd6>$1</color>");
            
            // 粗体: **text**
            text = Regex.Replace(text, @"\*\*(.*?)\*\*", "<b>$1</b>");
            
            // 斜体: *text*
            text = Regex.Replace(text, @"\*(.*?)\*", "<i>$1</i>");
            
            // 行内代码: `text`
            text = Regex.Replace(text, @"\`(.*?)\`", "<color=#dcdcaa>$1</color>");
            
            return text;
        }

        #endregion

        #region GUI 样式初始化

        private void EnsureStyles()
        {
            if (_mdStylesInitialized) return;

            _h1Style = new GUIStyle(EditorStyles.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true, margin = new RectOffset(0, 0, 15, 10), richText = true };
            _h2Style = new GUIStyle(EditorStyles.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true, margin = new RectOffset(0, 0, 10, 8), richText = true };
            _h3Style = new GUIStyle(EditorStyles.label) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true, margin = new RectOffset(0, 0, 8, 5), richText = true };
            
            _pStyle = new GUIStyle(EditorStyles.label) { fontSize = 13, wordWrap = true, richText = true, margin = new RectOffset(0, 0, 4, 4) };
            
            _codeBoxStyle = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 8, 8), margin = new RectOffset(4, 4, 10, 10) };
            _codeTextStyle = new GUIStyle(EditorStyles.textArea) 
            { 
                fontSize = 13, 
                wordWrap = false, 
                richText = false, // 禁用富文本，确保代码中的 <T> 完美显示
                focused = { background = null },
                active = { background = null },
                hover = { background = null },
                normal = { background = null, textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            
            _quoteStyle = new GUIStyle(EditorStyles.label) { fontSize = 13, fontStyle = FontStyle.Italic, wordWrap = true, richText = true, padding = new RectOffset(10, 0, 0, 0), normal = { textColor = new Color(0.65f, 0.65f, 0.65f) } };
            
            _listStyle = new GUIStyle(EditorStyles.label) { fontSize = 13, wordWrap = true, richText = true, padding = new RectOffset(15, 0, 0, 0) };
            
            _hrStyle = new GUIStyle("box") { margin = new RectOffset(0, 0, 15, 15) };

            _mdStylesInitialized = true;
        }

        #endregion
    }
}
