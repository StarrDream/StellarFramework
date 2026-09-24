using System;
using System.Linq;
using NUnit.Framework;
using StellarFramework.Localization;
using StellarFramework.Localization.Editor;
using StellarFramework.Localization.TMP;
using StellarFramework.Localization.TMP.Editor;
using StellarFramework.Localization.UnityUGUI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class LocalizationTmpAdapterTests
    {
        private const string TempRoot = "Assets/__LocalizationTmpTests";

        [SetUp]
        public void SetUp()
        {
            TearDown();
            AssetDatabase.CreateFolder("Assets", "__LocalizationTmpTests");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempRoot))
            {
                AssetDatabase.DeleteAsset(TempRoot);
            }
        }

        [Test]
        public void TmpScannerAppliesStableBindingAndSourceTable()
        {
            string prefabPath = TempRoot + "/Panel_Login_TMP.prefab";
            CreateTmpPrefab(prefabPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            var sourceTable = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            sourceTable.Configure("zh-CN", Array.Empty<LocalizationAuthoringEntry>());

            Assert.That(
                LocalizationTmpUiScanner.TryScanPrefab(
                    prefab,
                    "zh-CN",
                    registry,
                    out LocalizationTmpScanResult scan,
                    out string scanError),
                Is.True,
                scanError);
            Assert.That(scan.Candidates.Count, Is.EqualTo(3));
            Assert.That(
                LocalizationTmpUiScanner.TryApply(
                    scan,
                    sourceTable,
                    registry,
                    out string applyError),
                Is.True,
                applyError);

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            LocalizedTMPTextView[] bindings =
                prefab.GetComponentsInChildren<LocalizedTMPTextView>(true);
            Assert.That(bindings.Length, Is.EqualTo(3));
            Assert.That(bindings.Select(view => view.BindingId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(bindings.All(view => !string.IsNullOrWhiteSpace(view.Key)), Is.True);
            Assert.That(sourceTable.Entries.Count, Is.EqualTo(3));
            Assert.That(registry.Records.Count, Is.EqualTo(3));
            Assert.That(
                sourceTable.Entries.Single(entry => entry.Value == "确认").Key,
                Does.StartWith("ui.panel_login_tmp.btn_confirm."));

            UnityEngine.Object.DestroyImmediate(sourceTable);
            UnityEngine.Object.DestroyImmediate(registry);
        }

        [Test]
        public void TmpViewUsesCoreContextContractWithoutUGUITypedReference()
        {
            GameObject root = null;
            Assert.That(LocaleId.TryCreate("zh-CN", out LocaleId zhLocale, out string localeError), Is.True, localeError);
            Assert.That(
                LocalizationKey.TryCreate(
                    "ui.test.label.12345678",
                    out LocalizationKey key,
                    out string keyError),
                Is.True,
                keyError);
            var zhTable = new LocalizationTable(
                zhLocale,
                new[]
                {
                    new LocalizationEntry(
                        key,
                        "登录")
                });
            var catalog = new LocalizationCatalog(new[] { zhTable });
            var service = new LocalizationService(
                catalog,
                zhLocale,
                new LocalizationFallbackPolicy(Array.Empty<LocaleId>()));

            try
            {
                root = new GameObject("Root", typeof(RectTransform));
                FakeLocalizationContext context = root.AddComponent<FakeLocalizationContext>();
                context.Configure(service);

                GameObject label = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI),
                    typeof(LocalizedTMPTextView));
                label.transform.SetParent(root.transform, false);
                TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
                LocalizedTMPTextView view = label.GetComponent<LocalizedTMPTextView>();
                view.ConfigureBinding(
                    "1234567890abcdef1234567890abcdef",
                    text,
                    "ui.test.label.12345678");

                Assert.That(view.Refresh(out string error), Is.True, error);
                Assert.That(text.text, Is.EqualTo("登录"));
                Assert.That(view.ContextProvider, Is.SameAs(context));
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void CreateTmpPrefab(string path)
        {
            GameObject root = new GameObject("Panel_Login_TMP", typeof(RectTransform));
            try
            {
                CreateTmpText(root.transform, "Title", "登录");
                CreateTmpText(root.transform, "BtnConfirm", "确认");
                CreateTmpText(root.transform, "BtnQuit", "退出");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TextMeshProUGUI CreateTmpText(
            Transform parent,
            string name,
            string value)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            return text;
        }

        private sealed class FakeLocalizationContext : MonoBehaviour, ILocalizationContext
        {
            public LocalizationService Service { get; private set; }
            public LocaleId CurrentLocale => Service.CurrentLocale;
            public event EventHandler<LocalizationChangedEventArgs> LocaleChanged
            {
                add => Service.LocaleChanged += value;
                remove => Service.LocaleChanged -= value;
            }

            public void Configure(LocalizationService service)
            {
                Service = service;
            }

            public bool TryInitialize(out string error)
            {
                if (Service == null)
                {
                    error = "Service is missing.";
                    return false;
                }
                error = null;
                return true;
            }
        }
    }
}
