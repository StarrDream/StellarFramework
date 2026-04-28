using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellarFramework.Settings
{
    [DisallowMultipleComponent]
    public sealed class SettingsMenuOverlay : MonoBehaviour
    {
        [Header("Window")]
        public string title = "Settings";
        public bool visibleOnStart = true;
        public KeyCode toggleKey = KeyCode.F10;
        public Rect windowRect = new Rect(48f, 48f, 860f, 620f);

        [Header("Layout")]
        public float sidebarWidth = 220f;
        public float rowHeight = 32f;

        private string _selectedPageId;
        private Vector2 _pageScroll;
        private Vector2 _sidebarScroll;

        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _pageButtonStyle;
        private GUIStyle _rowLabelStyle;
        private GUIStyle _hintStyle;

        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        private bool _isVisible;

        private void Start()
        {
            _isVisible = visibleOnStart;
            SettingsKit.Init();
            EnsureSelectedPage();
        }

        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                _isVisible = !_isVisible;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible)
            {
                return;
            }

            EnsureStyles();
            EnsureSelectedPage();
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, title, _windowStyle);
        }

        private void DrawWindow(int windowId)
        {
            IReadOnlyList<SettingsPageDefinition> pages = SettingsKit.GetPages();
            if (pages.Count == 0)
            {
                GUILayout.Label(
                    "No settings pages are registered yet. Call SettingsKit.RegisterProvider or SettingsKit.InstallDefaultProviders first.",
                    _hintStyle);
                GUILayout.Space(12f);

                if (GUILayout.Button("Close", GUILayout.Height(28f)))
                {
                    _isVisible = false;
                }

                GUI.DragWindow();
                return;
            }

            using (new GUILayout.HorizontalScope())
            {
                DrawSidebar(pages);
                DrawContent();
            }

            GUILayout.Space(8f);
            DrawFooter();
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
        }

        private void DrawSidebar(IReadOnlyList<SettingsPageDefinition> pages)
        {
            using (new GUILayout.VerticalScope(GUILayout.Width(sidebarWidth)))
            {
                GUILayout.Label("Pages", _titleStyle);
                _sidebarScroll = GUILayout.BeginScrollView(_sidebarScroll, GUI.skin.box, GUILayout.ExpandHeight(true));

                for (int i = 0; i < pages.Count; i++)
                {
                    SettingsPageDefinition page = pages[i];
                    bool isSelected = string.Equals(_selectedPageId, page.Id, StringComparison.Ordinal);

                    GUI.backgroundColor = isSelected ? new Color(0.35f, 0.60f, 0.92f) : Color.white;
                    if (GUILayout.Button(page.DisplayName, _pageButtonStyle, GUILayout.Height(34f)))
                    {
                        _selectedPageId = page.Id;
                        _pageScroll = Vector2.zero;
                    }

                    GUI.backgroundColor = Color.white;

                    if (!string.IsNullOrWhiteSpace(page.Description))
                    {
                        GUILayout.Label(page.Description, _hintStyle);
                        GUILayout.Space(6f);
                    }
                }

                GUILayout.EndScrollView();
            }
        }

        private void DrawContent()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                if (string.IsNullOrEmpty(_selectedPageId))
                {
                    GUILayout.Label("Select a page from the left.", _hintStyle);
                    return;
                }

                IReadOnlyList<SettingEntry> entries = SettingsKit.GetEntriesForPage(_selectedPageId);
                _pageScroll = GUILayout.BeginScrollView(_pageScroll, GUILayout.ExpandHeight(true));

                for (int i = 0; i < entries.Count; i++)
                {
                    DrawEntry(entries[i]);
                    GUILayout.Space(4f);
                }

                GUILayout.EndScrollView();
            }
        }

        private void DrawEntry(SettingEntry entry)
        {
            SettingDefinition definition = entry.Definition;
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                using (new GUILayout.HorizontalScope(GUILayout.Height(rowHeight)))
                {
                    GUILayout.Label(definition.DisplayName, _rowLabelStyle, GUILayout.Width(220f));

                    switch (definition)
                    {
                        case BoolSettingDefinition _:
                            bool boolValue = (bool)entry.CurrentValue;
                            bool nextBoolValue = GUILayout.Toggle(boolValue, boolValue ? "On" : "Off", GUILayout.Width(80f));
                            if (nextBoolValue != boolValue)
                            {
                                TryApplyUiValue(definition.Key, nextBoolValue);
                            }

                            break;

                        case FloatSettingDefinition floatDefinition:
                            float floatValue = (float)entry.CurrentValue;
                            float nextFloatValue = GUILayout.HorizontalSlider(
                                floatValue,
                                floatDefinition.MinValue,
                                floatDefinition.MaxValue,
                                GUILayout.Width(220f));
                            GUILayout.Label(floatDefinition.FormatValue(nextFloatValue), GUILayout.Width(60f));
                            if (!Mathf.Approximately(nextFloatValue, floatValue))
                            {
                                TryApplyUiValue(definition.Key, nextFloatValue);
                            }

                            break;

                        case IntSettingDefinition intDefinition:
                            int intValue = (int)entry.CurrentValue;
                            int nextIntValue = Mathf.RoundToInt(
                                GUILayout.HorizontalSlider(
                                    intValue,
                                    intDefinition.MinValue,
                                    intDefinition.MaxValue,
                                    GUILayout.Width(220f)));
                            GUILayout.Label(nextIntValue.ToString(), GUILayout.Width(60f));
                            if (nextIntValue != intValue)
                            {
                                TryApplyUiValue(definition.Key, nextIntValue);
                            }

                            break;

                        case ChoiceSettingDefinition choiceDefinition:
                            DrawChoiceField(definition.Key, entry, choiceDefinition);
                            break;

                        case StringSettingDefinition _:
                            string stringValue = entry.CurrentValue?.ToString() ?? string.Empty;
                            string nextStringValue = GUILayout.TextField(stringValue, GUILayout.Width(280f));
                            if (!string.Equals(nextStringValue, stringValue, StringComparison.Ordinal))
                            {
                                TryApplyUiValue(definition.Key, nextStringValue);
                            }

                            break;
                    }

                    GUILayout.FlexibleSpace();

                    if (definition.RequiresRestart)
                    {
                        GUILayout.Label("Restart Required", GUILayout.Width(108f));
                    }

                    if (entry.IsDirty)
                    {
                        GUILayout.Label("*", GUILayout.Width(16f));
                    }
                }

                if (!string.IsNullOrWhiteSpace(definition.Description))
                {
                    GUILayout.Label(definition.Description, _hintStyle);
                }

                if (!string.IsNullOrWhiteSpace(entry.LastError))
                {
                    Color oldColor = GUI.contentColor;
                    GUI.contentColor = new Color(0.88f, 0.25f, 0.25f);
                    GUILayout.Label(entry.LastError, _hintStyle);
                    GUI.contentColor = oldColor;
                }
            }
        }

        private void DrawChoiceField(string key, SettingEntry entry, ChoiceSettingDefinition definition)
        {
            IReadOnlyList<SettingChoiceOption> options = definition.Options;
            if (options.Count == 0)
            {
                GUILayout.Label("No Options", GUILayout.Width(120f));
                return;
            }

            string currentValue = entry.CurrentValue?.ToString() ?? string.Empty;
            int currentIndex = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Value, currentValue, StringComparison.Ordinal))
                {
                    currentIndex = i;
                    break;
                }
            }

            if (GUILayout.Button("<", GUILayout.Width(28f)))
            {
                int previousIndex = (currentIndex - 1 + options.Count) % options.Count;
                TryApplyUiValue(key, options[previousIndex].Value);
            }

            GUILayout.Label(options[currentIndex].Label, GUILayout.Width(220f));

            if (GUILayout.Button(">", GUILayout.Width(28f)))
            {
                int nextIndex = (currentIndex + 1) % options.Count;
                TryApplyUiValue(key, options[nextIndex].Value);
            }
        }

        private void DrawFooter()
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply", GUILayout.Height(32f)))
                {
                    if (!SettingsKit.ApplyPending(out string error))
                    {
                        Debug.LogError(error);
                    }
                }

                if (GUILayout.Button("Save", GUILayout.Height(32f)))
                {
                    if (!SettingsKit.Save(out string error))
                    {
                        Debug.LogError(error);
                    }
                }

                if (GUILayout.Button("Revert Pending", GUILayout.Height(32f)))
                {
                    if (!SettingsKit.RevertPending(out string error))
                    {
                        Debug.LogError(error);
                    }
                }

                if (GUILayout.Button("Reset Page", GUILayout.Height(32f)))
                {
                    SettingsKit.ResetPage(_selectedPageId);
                }

                if (GUILayout.Button("Reset All", GUILayout.Height(32f)))
                {
                    SettingsKit.ResetAll();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Close", GUILayout.Width(90f), GUILayout.Height(32f)))
                {
                    _isVisible = false;
                }
            }
        }

        private static void TryApplyUiValue(string key, object value)
        {
            if (!SettingsKit.TrySetValue(key, value, out string error))
            {
                Debug.LogError(error);
            }
        }

        private void EnsureSelectedPage()
        {
            if (!string.IsNullOrEmpty(_selectedPageId))
            {
                return;
            }

            IReadOnlyList<SettingsPageDefinition> pages = SettingsKit.GetPages();
            if (pages.Count > 0)
            {
                _selectedPageId = pages[0].Id;
            }
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null)
            {
                return;
            }

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(12, 12, 20, 12)
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };

            _pageButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };

            _rowLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true
            };
        }
    }
}
