using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// 为 Example Playable 场景提供统一的屏幕说明面板。
    /// </summary>
    public class ExampleSceneGuide : MonoBehaviour
    {
        [TextArea(1, 2)] public string title;
        [TextArea(2, 4)] public string summary;
        [TextArea(2, 8)] public string controls;
        [TextArea(2, 8)] public string setup;
        [TextArea(2, 8)] public string expected;
        [TextArea(2, 8)] public string notes;
        public bool anchorRight;

        public Rect area = new Rect(16f, 16f, 460f, 680f);

        private Vector2 _scroll;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _textStyle;
        private GUIStyle _boxStyle;

        private void OnGUI()
        {
            EnsureStyles();

            Rect drawArea = area;
            if (anchorRight)
            {
                drawArea.x = Screen.width - area.width - 16f;
            }

            GUILayout.BeginArea(drawArea, _boxStyle);
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (!string.IsNullOrWhiteSpace(title))
            {
                GUILayout.Label(title, _titleStyle);
            }

            DrawSection("案例定位 / Summary", summary);
            DrawSection("操作说明 / Controls", controls);
            DrawSection("运行准备 / Setup", setup);
            DrawSection("预期结果 / Expected", expected);
            DrawSection("补充说明 / Notes", notes);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSection(string header, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(header, _sectionStyle);
            GUILayout.Label(body, _textStyle);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = false
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 14, 14),
                alignment = TextAnchor.UpperLeft
            };
        }
    }
}
