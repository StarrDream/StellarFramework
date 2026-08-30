using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace StellarFramework.Editor.Modules
{
    /// <summary>
    /// 文档中心模块 (UI Toolkit 版)
    /// 自动扫描框架目录下的所有 Markdown 文件，提供稳定的集成文档阅读体验。
    /// </summary>
    [StellarTool("文档中心 (Docs)", "框架核心", -999)]
    public class DocumentationHubModule : ToolModule
    {
        private sealed class DocEntry
        {
            public string Path;
            public string RelativePath;
            public string DisplayName;
            public string Category;
            public int SortOrder;
        }

        private enum BlockType
        {
            Paragraph,
            Header1,
            Header2,
            Header3,
            Code,
            Quote,
            List,
            Table,
            HR
        }

        private sealed class MarkdownBlock
        {
            public BlockType Type;
            public string Content;
        }

        public override string Icon => "d_TextAsset Icon";
        public override string Description => "统一管理与查阅框架内所有 Markdown 文档 (支持文本排版与代码块高亮)。";

        private readonly List<DocEntry> _docs = new List<DocEntry>();
        private readonly List<MarkdownBlock> _parsedBlocks = new List<MarkdownBlock>();

        private string _selectedDocPath = string.Empty;
        private string _docContent = string.Empty;

        private Label _docCountLabel;
        private ScrollView _docListView;
        private Label _selectedDocTitleLabel;
        private Label _selectedDocPathLabel;
        private ScrollView _docContentView;

        public override void OnEnable()
        {
            RefreshDocs();
        }

        public override VisualElement CreateView()
        {
            TwoPaneSplitView splitView = new TwoPaneSplitView(0, 340, TwoPaneSplitViewOrientation.Horizontal)
            {
                style =
                {
                    flexGrow = 1f
                }
            };

            VisualElement leftPane = new VisualElement
            {
                style =
                {
                    flexGrow = 1f,
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 12,
                    paddingBottom = 12
                }
            };

            Button refreshButton = new Button(() =>
            {
                RefreshDocs();
                if (!string.IsNullOrEmpty(_selectedDocPath) && File.Exists(_selectedDocPath))
                {
                    _docContent = File.ReadAllText(_selectedDocPath);
                    ParseMarkdown(_docContent);
                }

                RefreshDocListView();
                RefreshDocContentView();
            })
            {
                text = "刷新文档列表"
            };
            refreshButton.style.height = 28;
            leftPane.Add(refreshButton);

            _docCountLabel = new Label
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    marginTop = 10
                }
            };
            leftPane.Add(_docCountLabel);

            _docListView = new ScrollView
            {
                style =
                {
                    flexGrow = 1f,
                    marginTop = 8
                }
            };
            leftPane.Add(_docListView);

            VisualElement rightPane = new VisualElement
            {
                style =
                {
                    flexGrow = 1f,
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 12,
                    paddingBottom = 12
                }
            };

            VisualElement docHeader = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            VisualElement titleColumn = new VisualElement
            {
                style =
                {
                    flexGrow = 1f
                }
            };

            _selectedDocTitleLabel = new Label("请在左侧选择要查阅的文档")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16
                }
            };
            titleColumn.Add(_selectedDocTitleLabel);

            _selectedDocPathLabel = new Label
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.73f, 0.77f, 0.83f),
                    marginTop = 2
                }
            };
            titleColumn.Add(_selectedDocPathLabel);
            docHeader.Add(titleColumn);

            Button openExternalButton = new Button(() =>
            {
                if (!string.IsNullOrEmpty(_selectedDocPath) && File.Exists(_selectedDocPath))
                {
                    EditorUtility.OpenWithDefaultApp(_selectedDocPath);
                }
            })
            {
                text = "在外部编辑器打开"
            };
            openExternalButton.style.height = 28;
            docHeader.Add(openExternalButton);
            rightPane.Add(docHeader);

            _docContentView = new ScrollView
            {
                style =
                {
                    flexGrow = 1f,
                    marginTop = 10
                }
            };
            rightPane.Add(_docContentView);

            splitView.Add(leftPane);
            splitView.Add(rightPane);

            RefreshDocListView();
            RefreshDocContentView();
            return splitView;
        }

        public override void OnGUI()
        {
            EditorGUILayout.HelpBox("文档中心已迁移到 UI Toolkit 视图入口。", MessageType.Info);
        }

        private void RefreshDocs()
        {
            _docs.Clear();
            string rootPath = Application.dataPath + "/StellarFramework";
            if (!Directory.Exists(rootPath))
            {
                return;
            }

            string[] files = Directory.GetFiles(rootPath, "*.md", SearchOption.AllDirectories);
            string normalizedRoot = rootPath.Replace("\\", "/").TrimEnd('/');
            foreach (string file in files)
            {
                string normalizedPath = file.Replace("\\", "/");
                string relativePath = normalizedPath.Replace(normalizedRoot + "/", string.Empty);
                if (!ShouldIncludeDoc(normalizedPath, relativePath))
                {
                    continue;
                }

                _docs.Add(new DocEntry
                {
                    Path = normalizedPath,
                    RelativePath = relativePath,
                    DisplayName = BuildDisplayName(normalizedPath, relativePath),
                    Category = BuildCategory(relativePath),
                    SortOrder = BuildSortOrder(relativePath)
                });
            }

            _docs.Sort((left, right) =>
            {
                int categoryCompare = GetCategoryOrder(left.Category).CompareTo(GetCategoryOrder(right.Category));
                if (categoryCompare != 0)
                {
                    return categoryCompare;
                }

                int orderCompare = left.SortOrder.CompareTo(right.SortOrder);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                int titleCompare = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
                return titleCompare != 0
                    ? titleCompare
                    : string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase);
            });

            if (!string.IsNullOrEmpty(_selectedDocPath) && !File.Exists(_selectedDocPath))
            {
                _selectedDocPath = string.Empty;
                _docContent = string.Empty;
                _parsedBlocks.Clear();
            }
        }

        private static bool ShouldIncludeDoc(string normalizedPath, string relativePath)
        {
            if (!File.Exists(normalizedPath))
            {
                return false;
            }

            if (string.Equals(
                    relativePath,
                    "Editor/StellarToolsHub/StellarToolsHub-工具中心-Guide.md",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private void RefreshDocListView()
        {
            if (_docCountLabel != null)
            {
                _docCountLabel.text = $"文档列表 ({_docs.Count})";
            }

            if (_docListView == null)
            {
                return;
            }

            _docListView.Clear();

            string currentCategory = null;
            foreach (DocEntry doc in _docs)
            {
                if (!string.Equals(currentCategory, doc.Category, StringComparison.Ordinal))
                {
                    currentCategory = doc.Category;
                    _docListView.Add(new Label(currentCategory)
                    {
                        style =
                        {
                            unityFontStyleAndWeight = FontStyle.Bold,
                            fontSize = 11,
                            color = new Color(0.73f, 0.77f, 0.83f),
                            marginTop = 10,
                            marginBottom = 6
                        }
                    });
                }

                Button button = new Button(() =>
                {
                    if (_selectedDocPath == doc.Path)
                    {
                        return;
                    }

                    _selectedDocPath = doc.Path;
                    _docContent = File.ReadAllText(doc.Path);
                    ParseMarkdown(_docContent);
                    RefreshDocListView();
                    RefreshDocContentView();
                })
                {
                    text = doc.DisplayName,
                    tooltip = doc.RelativePath
                };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.height = 34;
                button.style.marginBottom = 4;
                button.style.paddingLeft = 12;
                button.style.backgroundColor = _selectedDocPath == doc.Path
                    ? new Color(0.16f, 0.42f, 0.80f)
                    : new Color(0.16f, 0.18f, 0.21f);
                button.style.color = Color.white;
                button.style.borderTopLeftRadius = 8;
                button.style.borderTopRightRadius = 8;
                button.style.borderBottomLeftRadius = 8;
                button.style.borderBottomRightRadius = 8;
                _docListView.Add(button);
            }
        }

        private void RefreshDocContentView()
        {
            if (_docContentView == null)
            {
                return;
            }

            _docContentView.Clear();

            if (string.IsNullOrEmpty(_selectedDocPath) || !File.Exists(_selectedDocPath))
            {
                if (_selectedDocTitleLabel != null) _selectedDocTitleLabel.text = "请在左侧选择要查阅的文档";
                if (_selectedDocPathLabel != null) _selectedDocPathLabel.text = string.Empty;
                _docContentView.Add(new HelpBox("当前未选择文档。", HelpBoxMessageType.Info));
                return;
            }

            DocEntry selectedDoc = GetSelectedDoc();
            if (_selectedDocTitleLabel != null)
            {
                _selectedDocTitleLabel.text = selectedDoc != null ? selectedDoc.DisplayName : Path.GetFileName(_selectedDocPath);
            }

            if (_selectedDocPathLabel != null)
            {
                _selectedDocPathLabel.text = selectedDoc != null ? selectedDoc.RelativePath : _selectedDocPath;
            }

            if (_parsedBlocks.Count == 0 && !string.IsNullOrEmpty(_docContent))
            {
                ParseMarkdown(_docContent);
            }

            foreach (MarkdownBlock block in _parsedBlocks)
            {
                _docContentView.Add(BuildBlockView(block));
            }
        }

        private VisualElement BuildBlockView(MarkdownBlock block)
        {
            switch (block.Type)
            {
                case BlockType.Header1:
                    return CreateLabelBlock(block.Content, 22, FontStyle.Bold, 12, 8, true);
                case BlockType.Header2:
                    return CreateLabelBlock(block.Content, 18, FontStyle.Bold, 10, 6, true);
                case BlockType.Header3:
                    return CreateLabelBlock(block.Content, 15, FontStyle.Bold, 8, 5, true);
                case BlockType.Paragraph:
                    if (string.IsNullOrEmpty(block.Content))
                    {
                        return new VisualElement { style = { height = 8 } };
                    }

                    return CreateLabelBlock(block.Content, 13, FontStyle.Normal, 4, 4, true);
                case BlockType.Quote:
                    VisualElement quote = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            marginBottom = 8
                        }
                    };
                    quote.Add(new VisualElement
                    {
                        style =
                        {
                            width = 4,
                            marginRight = 8,
                            backgroundColor = new Color(0.35f, 0.68f, 1.00f)
                        }
                    });
                    quote.Add(CreateLabelBlock(block.Content, 13, FontStyle.Italic, 0, 0, true));
                    return quote;
                case BlockType.List:
                    return CreateLabelBlock(block.Content, 13, FontStyle.Normal, 3, 3, true, 14);
                case BlockType.Code:
                case BlockType.Table:
                    TextField codeField = new TextField
                    {
                        value = block.Content,
                        multiline = true,
                        isReadOnly = true
                    };
                    codeField.style.marginBottom = 10;
                    return codeField;
                case BlockType.HR:
                    return new VisualElement
                    {
                        style =
                        {
                            height = 2,
                            marginTop = 10,
                            marginBottom = 10,
                            backgroundColor = new Color(0.27f, 0.31f, 0.37f)
                        }
                    };
                default:
                    return new VisualElement();
            }
        }

        private static Label CreateLabelBlock(string text, int fontSize, FontStyle fontStyle, int marginTop, int marginBottom,
            bool wrap = false, int paddingLeft = 0)
        {
            Label label = new Label(text)
            {
                style =
                {
                    fontSize = fontSize,
                    unityFontStyleAndWeight = fontStyle,
                    marginTop = marginTop,
                    marginBottom = marginBottom,
                    whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap,
                    paddingLeft = paddingLeft
                }
            };
            label.enableRichText = true;
            return label;
        }

        private void ParseMarkdown(string rawText)
        {
            _parsedBlocks.Clear();
            if (string.IsNullOrEmpty(rawText))
            {
                return;
            }

            string[] lines = rawText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool inCodeBlock = false;
            StringBuilder codeBuilder = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

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

                if (inCodeBlock)
                {
                    codeBuilder.AppendLine(line);
                    continue;
                }

                if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.HR, Content = string.Empty });
                    continue;
                }

                if (line.StartsWith("# "))
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Header1, Content = ParseInline(line.Substring(2)) });
                    continue;
                }

                if (line.StartsWith("## "))
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Header2, Content = ParseInline(line.Substring(3)) });
                    continue;
                }

                if (line.StartsWith("### "))
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Header3, Content = ParseInline(line.Substring(4)) });
                    continue;
                }

                if (line.StartsWith("#### "))
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Header3, Content = ParseInline(line.Substring(5)) });
                    continue;
                }

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

                if (string.IsNullOrEmpty(trimmed))
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Paragraph, Content = string.Empty });
                    continue;
                }

                if (_parsedBlocks.Count > 0 &&
                    _parsedBlocks[_parsedBlocks.Count - 1].Type == BlockType.Paragraph &&
                    !string.IsNullOrEmpty(_parsedBlocks[_parsedBlocks.Count - 1].Content))
                {
                    _parsedBlocks[_parsedBlocks.Count - 1].Content += " " + ParseInline(trimmed);
                }
                else
                {
                    _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Paragraph, Content = ParseInline(trimmed) });
                }
            }

            if (inCodeBlock)
            {
                _parsedBlocks.Add(new MarkdownBlock { Type = BlockType.Code, Content = codeBuilder.ToString().TrimEnd() });
            }
        }

        private DocEntry GetSelectedDoc()
        {
            return _docs.FirstOrDefault(doc => doc.Path == _selectedDocPath);
        }

        private static string BuildDisplayName(string path, string relativePath)
        {
            string headerTitle = ExtractFirstHeader(path);
            if (!string.IsNullOrEmpty(headerTitle))
            {
                return headerTitle;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, "README", StringComparison.OrdinalIgnoreCase))
            {
                string folderName = Path.GetFileName(Path.GetDirectoryName(path));
                if (string.Equals(relativePath, "README.md", StringComparison.OrdinalIgnoreCase))
                {
                    return "StellarFramework / 框架总览";
                }

                return $"{folderName} / 目录索引";
            }

            return fileName;
        }

        private static string BuildCategory(string relativePath)
        {
            string normalized = relativePath.Replace("\\", "/");
            string fileName = Path.GetFileName(normalized);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(normalized);

            if (string.Equals(normalized, "README.md", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "快速开始.md", StringComparison.OrdinalIgnoreCase))
            {
                return "快速开始和 README";
            }

            if (normalized.StartsWith("Runtime/Kits/", StringComparison.OrdinalIgnoreCase)
                && fileNameWithoutExtension.Contains("说明文档"))
            {
                return "Kit 说明文档";
            }

            if (normalized.StartsWith("Runtime/Kits/", StringComparison.OrdinalIgnoreCase)
                && fileNameWithoutExtension.Contains("源码文档"))
            {
                return "Kit 源码文档";
            }

            if ((normalized.StartsWith("Runtime/Core/", StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith("Runtime/Extensions/", StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith("Runtime/Tools/", StringComparison.OrdinalIgnoreCase))
                && fileNameWithoutExtension.Contains("源码文档"))
            {
                return "架构/Runtime 源码文档";
            }

            if (normalized.StartsWith("Editor/StellarToolsHub/", StringComparison.OrdinalIgnoreCase))
            {
                return "ToolsHub 文档";
            }

            if (normalized.StartsWith("Samples/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Tests/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Generated/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
            {
                return "Samples/Tests/Generated/Resources 文档";
            }

            if (normalized.StartsWith("Runtime/Core/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Runtime/Tools/", StringComparison.OrdinalIgnoreCase))
            {
                return "架构/Runtime 源码文档";
            }

            return "其他专题文档";
        }

        private static int GetCategoryOrder(string category)
        {
            switch (category)
            {
                case "快速开始和 README":
                    return 0;
                case "Kit 说明文档":
                    return 10;
                case "Kit 源码文档":
                    return 20;
                case "架构/Runtime 源码文档":
                    return 30;
                case "ToolsHub 文档":
                    return 40;
                case "Samples/Tests/Generated/Resources 文档":
                    return 50;
                default:
                    return 100;
            }
        }

        private static int BuildSortOrder(string relativePath)
        {
            string normalized = relativePath.Replace("\\", "/");
            string fileName = Path.GetFileName(normalized);

            if (string.Equals(normalized, "README.md", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(fileName, "快速开始.md", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (fileName.Contains("StellarToolsHub-使用手册"))
            {
                return 2;
            }

            if (fileName.Contains("Architecture-MSV-架构源码文档"))
            {
                return 3;
            }

            if (fileName.Contains("ResKit-统一资源"))
            {
                return 4;
            }

            if (fileName.Contains("HotUpdateKit-热更新"))
            {
                return 5;
            }

            if (fileName.Contains("说明文档"))
            {
                return 20;
            }

            if (fileName.Contains("源码文档"))
            {
                return 30;
            }

            return 80;
        }

        private static string ExtractFirstHeader(string path)
        {
            try
            {
                foreach (string line in File.ReadLines(path))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("# "))
                    {
                        return trimmed.Substring(2).Trim();
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DocumentationHubModule] 读取标题失败: Path={path}, Error={exception.Message}");
            }

            return string.Empty;
        }

        private string ParseInline(string text)
        {
            text = text.Replace("<", "<\u200B");
            text = Regex.Replace(text, @"\!\[(.*?)\]\((.*?)\)", "<color=#4ec9b0>[图片: $1]</color>");
            text = Regex.Replace(text, @"\[(.*?)\]\((.*?)\)", "<color=#569cd6>$1</color>");
            text = Regex.Replace(text, @"\*\*(.*?)\*\*", "<b>$1</b>");
            text = Regex.Replace(text, @"\*(.*?)\*", "<i>$1</i>");
            text = Regex.Replace(text, @"\`(.*?)\`", "<color=#dcdcaa>$1</color>");
            return text;
        }
    }
}
