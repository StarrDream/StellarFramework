using StellarFramework.UI.Adaptation;
using UnityEngine;

namespace StellarFramework.Demo
{
    /// <summary>
    /// Sample-only setup that connects each screen-space Canvas to UIAdaptationKit.
    /// </summary>
    internal static class DemoDisplayAdaptation
    {
        public static void ConfigureAllCanvases()
        {
            Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
            int configuredCount = 0;
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || canvas.rootCanvas != canvas || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                UIAdaptationController controller = canvas.GetComponent<UIAdaptationController>();
                RectTransform safeAreaRoot = controller != null ? controller.SafeAreaRoot : null;
                if (safeAreaRoot == null)
                {
                    safeAreaRoot = CreateSafeAreaRoot(canvas.transform);
                }

                var profile = ScriptableObject.CreateInstance<UIAdaptationProfile>();
                profile.hideFlags = HideFlags.HideAndDontSave;
                var breakpoints = new[]
                {
                    new UIAdaptationBreakpoint(),
                    new UIAdaptationBreakpoint()
                };
                breakpoints[0].Configure(
                    "Portrait",
                    1f,
                    4f,
                    UIAdaptationOrientation.Portrait,
                    1f);
                breakpoints[1].Configure(
                    "Landscape",
                    1f,
                    4f,
                    UIAdaptationOrientation.Landscape,
                    0f);
                profile.Configure(new Vector2(1920f, 1080f), 0.5f, true, breakpoints);

                if (controller == null)
                {
                    controller = canvas.gameObject.AddComponent<UIAdaptationController>();
                }

                controller.DisplayGeometryChanged += geometry =>
                    LogDisplayAdaptation(canvas.name, controller, geometry);
                controller.Configure(profile, safeAreaRoot);
                controller.ApplyCurrentScreen();
                configuredCount++;
            }

            if (configuredCount == 0)
            {
                Debug.LogError("[ArchitectureDemo][UIAdaptation] FAIL: no screen-space Canvas was found.");
            }
        }

        private static void LogDisplayAdaptation(
            string canvasName,
            UIAdaptationController controller,
            UIDisplayGeometry geometry)
        {
            Rect safeArea = geometry.SafeArea;
            float width = Mathf.Max(1, geometry.Width);
            float height = Mathf.Max(1, geometry.Height);
            Vector2 expectedMin = new Vector2(safeArea.xMin / width, safeArea.yMin / height);
            Vector2 expectedMax = new Vector2(safeArea.xMax / width, safeArea.yMax / height);
            RectTransform safeAreaRoot = controller.SafeAreaRoot;
            bool anchorsMatch = safeAreaRoot != null &&
                Vector2.Distance(safeAreaRoot.anchorMin, expectedMin) <= 0.005f &&
                Vector2.Distance(safeAreaRoot.anchorMax, expectedMax) <= 0.005f;
            string evidence =
                $"result={(anchorsMatch ? "PASS" : "FAIL")}, canvas={canvasName}, " +
                $"resolution={geometry.Width}x{geometry.Height}, safeArea={safeArea}, " +
                $"insets={geometry.HasSafeAreaInsets}, cutouts={geometry.CutoutCount}, " +
                $"breakpoint={controller.CurrentBreakpointId}";
            if (anchorsMatch)
            {
                Debug.Log($"[ArchitectureDemo][UIAdaptation] {evidence}");
            }
            else
            {
                Debug.LogError($"[ArchitectureDemo][UIAdaptation] {evidence}");
            }
        }

        private static RectTransform CreateSafeAreaRoot(Transform canvasRoot)
        {
            int childCount = canvasRoot.childCount;
            var safeAreaObject = new GameObject("SafeAreaRoot", typeof(RectTransform));
            RectTransform safeAreaRoot = (RectTransform)safeAreaObject.transform;
            safeAreaRoot.SetParent(canvasRoot, false);
            safeAreaRoot.anchorMin = Vector2.zero;
            safeAreaRoot.anchorMax = Vector2.one;
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            safeAreaRoot.pivot = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < childCount; i++)
            {
                Transform child = canvasRoot.GetChild(0);
                child.SetParent(safeAreaRoot, false);
            }

            return safeAreaRoot;
        }
    }
}
