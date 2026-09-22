using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using StellarFramework.UI;
using StellarFramework.UI.Adaptation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class UIKitAdaptationTests
    {
        [Test]
        public void ProfileResolvesAspectBreakpointDeterministically()
        {
            UIAdaptationProfile profile = ScriptableObject.CreateInstance<UIAdaptationProfile>();
            var tall = new UIAdaptationBreakpoint();
            tall.Configure("phone_tall", 2.0f, 2.4f, UIAdaptationOrientation.Landscape, 0.25f);
            var tablet = new UIAdaptationBreakpoint();
            tablet.Configure("tablet", 1.2f, 1.5f, UIAdaptationOrientation.Landscape, 0.75f);
            profile.Configure(
                new Vector2(1920f, 1080f),
                0.5f,
                true,
                new[] { tall, tablet });

            UIAdaptationBreakpoint tallResult = profile.ResolveBreakpoint(2400, 1080, out float tallMatch);
            UIAdaptationBreakpoint tabletResult = profile.ResolveBreakpoint(2048, 1536, out float tabletMatch);
            UIAdaptationBreakpoint defaultResult = profile.ResolveBreakpoint(1920, 1080, out float defaultMatch);

            Assert.That(tallResult, Is.SameAs(tall));
            Assert.That(tallMatch, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(tabletResult, Is.SameAs(tablet));
            Assert.That(tabletMatch, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(defaultResult, Is.Null);
            Assert.That(defaultMatch, Is.EqualTo(0.5f).Within(0.0001f));

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ShapeAspectIsOrientationIndependent()
        {
            float landscape = UIAdaptationProfile.CalculateShapeAspect(2400, 1080);
            float portrait = UIAdaptationProfile.CalculateShapeAspect(1080, 2400);

            Assert.That(landscape, Is.EqualTo(portrait).Within(0.0001f));
            Assert.That(landscape, Is.EqualTo(2.222222f).Within(0.0001f));
            Assert.That(
                UIAdaptationProfile.ResolveOrientation(2400, 1080),
                Is.EqualTo(UIAdaptationOrientation.Landscape));
            Assert.That(
                UIAdaptationProfile.ResolveOrientation(1080, 2400),
                Is.EqualTo(UIAdaptationOrientation.Portrait));

            UIAdaptationProfile profile = ScriptableObject.CreateInstance<UIAdaptationProfile>();
            try
            {
                var tall = new UIAdaptationBreakpoint();
                tall.Configure(
                    "phone_tall",
                    2.0f,
                    2.4f,
                    UIAdaptationOrientation.Any,
                    0.35f);
                profile.Configure(
                    new Vector2(1920f, 1080f),
                    0.5f,
                    true,
                    new[] { tall });

                Assert.That(profile.ResolveBreakpoint(2400, 1080, out _), Is.SameAs(tall));
                Assert.That(profile.ResolveBreakpoint(1080, 2400, out _), Is.SameAs(tall));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ControllerAppliesScalerAndSafeAreaAnchors()
        {
            GameObject root = null;
            UIAdaptationProfile profile = null;
            try
            {
                root = new GameObject(
                    "UIRoot",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(UIAdaptationController));
                GameObject safe = new GameObject("SafeAreaRoot", typeof(RectTransform));
                safe.transform.SetParent(root.transform, false);

                profile = ScriptableObject.CreateInstance<UIAdaptationProfile>();
                profile.Configure(new Vector2(1920f, 1080f), 0.4f, true, null);

                UIAdaptationController controller = root.GetComponent<UIAdaptationController>();
                RectTransform safeRect = safe.GetComponent<RectTransform>();
                controller.Configure(profile, safeRect);
                controller.Apply(
                    1000,
                    2000,
                    Rect.MinMaxRect(50f, 100f, 950f, 1900f));

                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(safeRect.anchorMin.x, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(safeRect.anchorMin.y, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(safeRect.anchorMax.x, Is.EqualTo(0.95f).Within(0.0001f));
                Assert.That(safeRect.anchorMax.y, Is.EqualTo(0.95f).Within(0.0001f));
                Assert.That(safeRect.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(safeRect.offsetMax, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
                if (profile != null)
                {
                    Object.DestroyImmediate(profile);
                }
            }
        }

        [Test]
        public void SafeAreaCanBeDisabledWithoutMutatingSafeRootAnchors()
        {
            GameObject root = null;
            UIAdaptationProfile profile = null;
            try
            {
                root = new GameObject(
                    "UIRoot",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(UIAdaptationController));
                GameObject safe = new GameObject("SafeAreaRoot", typeof(RectTransform));
                safe.transform.SetParent(root.transform, false);
                RectTransform safeRect = safe.GetComponent<RectTransform>();
                safeRect.anchorMin = new Vector2(0.2f, 0.3f);
                safeRect.anchorMax = new Vector2(0.8f, 0.7f);

                profile = ScriptableObject.CreateInstance<UIAdaptationProfile>();
                profile.Configure(new Vector2(1920f, 1080f), 0.5f, false, null);

                UIAdaptationController controller = root.GetComponent<UIAdaptationController>();
                controller.Configure(profile, safeRect);
                controller.Apply(
                    1000,
                    2000,
                    Rect.MinMaxRect(50f, 100f, 950f, 1900f));

                Assert.That(safeRect.anchorMin, Is.EqualTo(new Vector2(0.2f, 0.3f)));
                Assert.That(safeRect.anchorMax, Is.EqualTo(new Vector2(0.8f, 0.7f)));
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
                if (profile != null)
                {
                    Object.DestroyImmediate(profile);
                }
            }
        }

        [Test]
        public void ControllerAppliesSafeAreaToMultipleRoleRoots()
        {
            GameObject root = null;
            UIAdaptationProfile profile = null;
            try
            {
                root = new GameObject(
                    "UIRoot",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(UIAdaptationController));
                GameObject staticSafe = new GameObject("StaticSafeAreaRoot", typeof(RectTransform));
                staticSafe.transform.SetParent(root.transform, false);
                GameObject dynamicSafe = new GameObject("DynamicSafeAreaRoot", typeof(RectTransform));
                dynamicSafe.transform.SetParent(root.transform, false);

                profile = ScriptableObject.CreateInstance<UIAdaptationProfile>();
                profile.Configure(new Vector2(1920f, 1080f), 0.5f, true, null);

                UIAdaptationController controller = root.GetComponent<UIAdaptationController>();
                RectTransform staticRect = staticSafe.GetComponent<RectTransform>();
                RectTransform dynamicRect = dynamicSafe.GetComponent<RectTransform>();
                controller.ConfigureSafeAreaRoots(profile, new[] { staticRect, dynamicRect });
                controller.Apply(
                    1000,
                    2000,
                    Rect.MinMaxRect(50f, 100f, 950f, 1900f));

                foreach (RectTransform safeRect in new[] { staticRect, dynamicRect })
                {
                    Assert.That(safeRect.anchorMin.x, Is.EqualTo(0.05f).Within(0.0001f));
                    Assert.That(safeRect.anchorMin.y, Is.EqualTo(0.05f).Within(0.0001f));
                    Assert.That(safeRect.anchorMax.x, Is.EqualTo(0.95f).Within(0.0001f));
                    Assert.That(safeRect.anchorMax.y, Is.EqualTo(0.95f).Within(0.0001f));
                }
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
                if (profile != null)
                {
                    Object.DestroyImmediate(profile);
                }
            }
        }

        [Test]
        public void UIKitLayerRoutingSeparatesFullScreenAndSafeAreaRegions()
        {
            GameObject host = null;
            GameObject root = null;
            try
            {
                host = new GameObject("UIKit_TestHost");
                UIKit uiKit = host.AddComponent<UIKit>();
                root = new GameObject("UIRoot_Test");
                GameObject dynamicCanvas = new GameObject("DynamicCanvas");
                dynamicCanvas.transform.SetParent(root.transform, false);
                GameObject fullRoot = CreateRegionWithLayers(dynamicCanvas.transform, "FullScreenRoot");
                GameObject safeRoot = CreateRegionWithLayers(dynamicCanvas.transform, "SafeAreaRoot");

                MethodInfo buildLayerMap = typeof(UIKit).GetMethod(
                    "BuildLayerMap",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo buildSafeAreaLayerMap = typeof(UIKit).GetMethod(
                    "BuildSafeAreaLayerMap",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo tryGetLayer = typeof(UIKit).GetMethod(
                    "TryGetLayer",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(buildLayerMap, Is.Not.Null);
                Assert.That(buildSafeAreaLayerMap, Is.Not.Null);
                Assert.That(tryGetLayer, Is.Not.Null);

                object[] buildArgs =
                {
                    root.transform,
                    UIPanelBase.PanelCanvasRole.Dynamic,
                    null
                };
                Assert.That((bool)buildLayerMap.Invoke(uiKit, buildArgs), Is.True);
                var fullMap =
                    (Dictionary<UIPanelBase.PanelLayer, Transform>)buildArgs[2];
                var safeMap =
                    (Dictionary<UIPanelBase.PanelLayer, Transform>)buildSafeAreaLayerMap.Invoke(
                        uiKit,
                        new object[]
                        {
                            root.transform,
                            UIPanelBase.PanelCanvasRole.Dynamic,
                            fullMap
                        });

                FieldInfo roleLayersField = typeof(UIKit).GetField(
                    "_roleLayers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo roleRegionLayersField = typeof(UIKit).GetField(
                    "_roleRegionLayers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(roleLayersField, Is.Not.Null);
                Assert.That(roleRegionLayersField, Is.Not.Null);

                var roleLayers =
                    (Dictionary<UIPanelBase.PanelCanvasRole, Dictionary<UIPanelBase.PanelLayer, Transform>>)
                    roleLayersField.GetValue(uiKit);
                roleLayers[UIPanelBase.PanelCanvasRole.Dynamic] = fullMap;

                var roleRegionLayers =
                    (Dictionary<
                        UIPanelBase.PanelCanvasRole,
                        Dictionary<UIPanelBase.PanelLayoutRegion, Dictionary<UIPanelBase.PanelLayer, Transform>>>)
                    roleRegionLayersField.GetValue(uiKit);
                roleRegionLayers[UIPanelBase.PanelCanvasRole.Dynamic] =
                    new Dictionary<UIPanelBase.PanelLayoutRegion, Dictionary<UIPanelBase.PanelLayer, Transform>>
                    {
                        [UIPanelBase.PanelLayoutRegion.FullScreen] = fullMap,
                        [UIPanelBase.PanelLayoutRegion.SafeArea] = safeMap
                    };

                object[] fullArgs =
                {
                    UIPanelBase.PanelCanvasRole.Dynamic,
                    UIPanelBase.PanelLayoutRegion.FullScreen,
                    UIPanelBase.PanelLayer.Middle,
                    null
                };
                object[] safeArgs =
                {
                    UIPanelBase.PanelCanvasRole.Dynamic,
                    UIPanelBase.PanelLayoutRegion.SafeArea,
                    UIPanelBase.PanelLayer.Middle,
                    null
                };
                Assert.That((bool)tryGetLayer.Invoke(uiKit, fullArgs), Is.True);
                Assert.That((bool)tryGetLayer.Invoke(uiKit, safeArgs), Is.True);

                Transform fullLayer = (Transform)fullArgs[3];
                Transform safeLayer = (Transform)safeArgs[3];
                Assert.That(fullLayer.parent, Is.EqualTo(fullRoot.transform));
                Assert.That(safeLayer.parent, Is.EqualTo(safeRoot.transform));
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                }
            }
        }

        [Test]
        public void LayoutVariantRestoresCapturedLayoutWhenBreakpointChanges()
        {
            GameObject root = null;
            UIAdaptationProfile profile = null;
            try
            {
                root = new GameObject(
                    "UIRoot",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(UIAdaptationController));
                GameObject panel = new GameObject(
                    "PanelShop",
                    typeof(RectTransform),
                    typeof(UILayoutVariant));
                panel.transform.SetParent(root.transform, false);
                GameObject button = new GameObject("BuyButton", typeof(RectTransform));
                button.transform.SetParent(panel.transform, false);

                RectTransform panelRect = panel.GetComponent<RectTransform>();
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(1f, 0f);
                buttonRect.anchorMax = new Vector2(1f, 0f);
                buttonRect.pivot = new Vector2(1f, 0f);
                buttonRect.anchoredPosition = new Vector2(-32f, 48f);
                buttonRect.sizeDelta = new Vector2(220f, 80f);

                var tablet = new UIAdaptationBreakpoint();
                tablet.Configure(
                    "tablet",
                    1.2f,
                    1.5f,
                    UIAdaptationOrientation.Landscape,
                    0.75f);
                profile = ScriptableObject.CreateInstance<UIAdaptationProfile>();
                profile.Configure(
                    new Vector2(1920f, 1080f),
                    0.5f,
                    false,
                    new[] { tablet });

                UIAdaptationController controller =
                    root.GetComponent<UIAdaptationController>();
                controller.Configure(profile, null);

                UILayoutVariant layout = panel.GetComponent<UILayoutVariant>();
                layout.Configure(controller, Array.Empty<UILayoutVariantDefinition>());
                UILayoutTargetState[] captured =
                    layout.CaptureCurrentHierarchy(panelRect, true);
                layout.SetVariant("tablet", captured);

                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = new Vector2(500f, 500f);
                buttonRect.sizeDelta = new Vector2(20f, 20f);

                controller.Apply(
                    2048,
                    1536,
                    Rect.MinMaxRect(0f, 0f, 2048f, 1536f));

                Assert.That(controller.CurrentBreakpointId, Is.EqualTo("tablet"));
                Assert.That(buttonRect.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(buttonRect.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(buttonRect.pivot, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(buttonRect.anchoredPosition, Is.EqualTo(new Vector2(-32f, 48f)));
                Assert.That(buttonRect.sizeDelta, Is.EqualTo(new Vector2(220f, 80f)));
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
                if (profile != null)
                {
                    Object.DestroyImmediate(profile);
                }
            }
        }

        [Test]
        public void ControllerRaisesBreakpointChangedOnlyWhenBreakpointActuallyChanges()
        {
            GameObject root = null;
            UIAdaptationProfile profile = null;
            try
            {
                root = new GameObject(
                    "UIRoot",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(UIAdaptationController));
                var tablet = new UIAdaptationBreakpoint();
                tablet.Configure(
                    "tablet",
                    1.2f,
                    1.5f,
                    UIAdaptationOrientation.Landscape,
                    0.75f);
                profile = ScriptableObject.CreateInstance<UIAdaptationProfile>();
                profile.Configure(
                    new Vector2(1920f, 1080f),
                    0.5f,
                    false,
                    new[] { tablet });

                UIAdaptationController controller =
                    root.GetComponent<UIAdaptationController>();
                controller.Configure(profile, null);
                int changeCount = 0;
                string lastId = null;
                controller.BreakpointChanged += id =>
                {
                    changeCount++;
                    lastId = id;
                };

                controller.Apply(1920, 1080, new Rect(0, 0, 1920, 1080));
                controller.Apply(1600, 900, new Rect(0, 0, 1600, 900));
                Assert.That(changeCount, Is.EqualTo(1));
                Assert.That(lastId, Is.EqualTo("default"));

                controller.Apply(2048, 1536, new Rect(0, 0, 2048, 1536));
                controller.Apply(1600, 1200, new Rect(0, 0, 1600, 1200));
                Assert.That(changeCount, Is.EqualTo(2));
                Assert.That(lastId, Is.EqualTo("tablet"));
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
                if (profile != null)
                {
                    Object.DestroyImmediate(profile);
                }
            }
        }

        private static GameObject CreateRegionWithLayers(Transform parent, string name)
        {
            GameObject region = new GameObject(name);
            region.transform.SetParent(parent, false);
            foreach (UIPanelBase.PanelLayer layer in Enum.GetValues(typeof(UIPanelBase.PanelLayer)))
            {
                GameObject layerObject = new GameObject(layer.ToString());
                layerObject.transform.SetParent(region.transform, false);
            }
            return region;
        }
    }
}
