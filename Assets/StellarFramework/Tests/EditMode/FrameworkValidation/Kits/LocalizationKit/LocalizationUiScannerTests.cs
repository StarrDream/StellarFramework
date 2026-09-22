using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StellarFramework.Localization.Editor;
using StellarFramework.Localization.UnityUGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class LocalizationUiScannerTests
    {
        private const string TempRoot = "Assets/__LocalizationScannerTests";

        [SetUp]
        public void SetUp()
        {
            TearDown();
            AssetDatabase.CreateFolder("Assets", "__LocalizationScannerTests");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempRoot))
            {
                AssetDatabase.DeleteAsset(TempRoot);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void GeneratedKeyDoesNotDependOnSiblingOrder()
        {
            GameObject root = null;
            try
            {
                root = new GameObject("Panel_Login", typeof(RectTransform));
                Text confirm = CreateText(root.transform, "BtnConfirm", "确认");
                Text quit = CreateText(root.transform, "BtnQuit", "退出");
                const string bindingId = "8f31c2a46bd94e3ca1a2b3c4d5e6f701";

                string before = LocalizationUiScanner.BuildKey(root.name, confirm, bindingId);
                quit.transform.SetSiblingIndex(0);
                string after = LocalizationUiScanner.BuildKey(root.name, confirm, bindingId);

                Assert.That(before, Is.EqualTo("ui.panel_login.btn_confirm.8f31c2a4"));
                Assert.That(after, Is.EqualTo(before));
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ScannerPreservesBindingAndKeyAfterHierarchyReorder()
        {
            const string bindingId = "c4729f11d92e44ecbca0c81a9a832222";
            const string key = "ui.panel_login.btn_confirm.c4729f11";
            string prefabPath = TempRoot + "/Panel_Login.prefab";
            CreateBoundPrefab(prefabPath, bindingId, key);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Text confirm = prefab.GetComponentsInChildren<Text>(true).Single(text => text.text == "确认");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(confirm, out _, out long localFileId);
            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            registry.Upsert(
                bindingId,
                key,
                prefabGuid,
                localFileId,
                "Panel_Login/BtnConfirm",
                typeof(Text).FullName,
                "zh-CN",
                "确认",
                LocalizationSourceStatus.Synced);

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                contents.transform.Find("BtnQuit").SetSiblingIndex(0);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(LocalizationUiScanner.TryScanPrefab(
                prefab,
                "zh-CN",
                registry,
                out LocalizationUiScanResult scan,
                out string error), Is.True, error);

            LocalizationUiScanCandidate candidate =
                scan.Candidates.Single(item => item.SourceText == "确认");
            Assert.That(candidate.BindingId, Is.EqualTo(bindingId));
            Assert.That(candidate.Key, Is.EqualTo(key));
            Assert.That(candidate.Status, Is.EqualTo(LocalizationUiScanStatus.Synced));
            UnityEngine.Object.DestroyImmediate(registry);
        }

        [Test]
        public void IndependentPrefabCopyForksDuplicateBindingIdentity()
        {
            const string bindingId = "a9917bd3111144448888aaaaaaaaaaaa";
            const string key = "ui.panel_login.btn_quit.a9917bd3";
            string originalPath = TempRoot + "/Panel_Login.prefab";
            string copyPath = TempRoot + "/Panel_Login_Copy.prefab";
            CreateBoundPrefab(originalPath, bindingId, key, onlyQuit: true);
            Assert.That(AssetDatabase.CopyAsset(originalPath, copyPath), Is.True);

            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(originalPath);
            Text originalText = original.GetComponentInChildren<Text>(true);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(originalText, out _, out long localFileId);
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            registry.Upsert(
                bindingId,
                key,
                AssetDatabase.AssetPathToGUID(originalPath),
                localFileId,
                "Panel_Login/BtnQuit",
                typeof(Text).FullName,
                "zh-CN",
                "退出",
                LocalizationSourceStatus.Synced);

            GameObject copy = AssetDatabase.LoadAssetAtPath<GameObject>(copyPath);
            Assert.That(LocalizationUiScanner.TryScanPrefab(
                copy,
                "zh-CN",
                registry,
                out LocalizationUiScanResult scan,
                out string error), Is.True, error);

            LocalizationUiScanCandidate candidate = scan.Candidates.Single();
            Assert.That(candidate.Status, Is.EqualTo(LocalizationUiScanStatus.DuplicateBindingId));
            Assert.That(candidate.BindingId, Is.Not.EqualTo(bindingId));
            Assert.That(candidate.Key, Does.StartWith("ui.panel_login_copy.btn_quit."));
            UnityEngine.Object.DestroyImmediate(registry);
        }

        [Test]
        public void ApplyAddsBindingUpdatesSourceTableAndRegistry()
        {
            string prefabPath = TempRoot + "/Panel_Login.prefab";
            CreatePlainPrefab(prefabPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            var sourceTable = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            sourceTable.Configure("zh-CN", Array.Empty<LocalizationAuthoringEntry>());

            Assert.That(LocalizationUiScanner.TryScanPrefab(
                prefab,
                "zh-CN",
                registry,
                out LocalizationUiScanResult scan,
                out string scanError), Is.True, scanError);
            Assert.That(scan.Candidates.Count, Is.EqualTo(3));
            Assert.That(LocalizationUiScanner.TryApply(
                scan,
                sourceTable,
                registry,
                out string applyError), Is.True, applyError);

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            LocalizedTextView[] bindings = prefab.GetComponentsInChildren<LocalizedTextView>(true);
            Assert.That(bindings.Length, Is.EqualTo(3));
            Assert.That(bindings.All(view => !string.IsNullOrWhiteSpace(view.BindingId)), Is.True);
            Assert.That(bindings.Select(view => view.BindingId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(sourceTable.Entries.Count, Is.EqualTo(3));
            Assert.That(registry.Records.Count, Is.EqualTo(3));
            Assert.That(
                sourceTable.Entries.Single(entry => entry.Value == "确认").Key,
                Does.StartWith("ui.panel_login.btn_confirm."));

            UnityEngine.Object.DestroyImmediate(sourceTable);
            UnityEngine.Object.DestroyImmediate(registry);
        }

        [Test]
        public void JsonTranslationExchangeRoundTripsExternalTranslation()
        {
            var sourceTable = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            var englishTable = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            var workspace = ScriptableObject.CreateInstance<LocalizationWorkspaceAsset>();
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            string filePath = Path.Combine(Path.GetTempPath(), "stellar-localization-roundtrip.json");
            try
            {
                const string key = "ui.panel_login.btn_confirm.a1b2c3d4";
                const string bindingId = "a1b2c3d4000011112222333344445555";
                sourceTable.Configure("zh-CN", new[] { new LocalizationAuthoringEntry(key, "确认") });
                englishTable.Configure("en-US", Array.Empty<LocalizationAuthoringEntry>());

                var zh = new LocalizationWorkspaceLanguage();
                zh.Configure("zh-CN", "中文", sourceTable);
                var en = new LocalizationWorkspaceLanguage();
                en.Configure("en-US", "English", englishTable);
                workspace.Configure(null, "zh-CN", new[] { zh, en });
                registry.Upsert(
                    bindingId,
                    key,
                    "prefab-guid",
                    123,
                    "Panel_Login/Mid/BtnConfirm/Text",
                    typeof(Text).FullName,
                    "zh-CN",
                    "确认",
                    LocalizationSourceStatus.Synced);

                Assert.That(
                    LocalizationTranslationExchange.TryExportJson(
                        workspace,
                        registry,
                        filePath,
                        out string exportError),
                    Is.True,
                    exportError);

                string json = File.ReadAllText(filePath);
                Assert.That(json, Does.Contain("Panel_Login/Mid/BtnConfirm/Text"));
                Assert.That(json, Does.Contain("en-US"));
                string quote = ((char)34).ToString();
                json = json.Replace(
                    quote + "value" + quote + ": " + quote + quote,
                    quote + "value" + quote + ": " + quote + "Confirm" + quote);
                File.WriteAllText(filePath, json);

                Assert.That(
                    LocalizationTranslationExchange.TryImportJson(
                        workspace,
                        registry,
                        filePath,
                        out LocalizationTranslationImportReport report,
                        out string importError),
                    Is.True,
                    importError);
                Assert.That(report.AppliedCount, Is.EqualTo(1));
                Assert.That(
                    englishTable.Entries.Single(entry => entry.Key == key).Value,
                    Is.EqualTo("Confirm"));
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                UnityEngine.Object.DestroyImmediate(registry);
                UnityEngine.Object.DestroyImmediate(workspace);
                UnityEngine.Object.DestroyImmediate(englishTable);
                UnityEngine.Object.DestroyImmediate(sourceTable);
            }
        }

        [Test]
        public void TranslationImportRejectsStaleSourceHash()
        {
            var sourceTable = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            var englishTable = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            var workspace = ScriptableObject.CreateInstance<LocalizationWorkspaceAsset>();
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            string filePath = Path.Combine(Path.GetTempPath(), "stellar-localization-stale.json");
            try
            {
                const string key = "ui.panel_login.btn_confirm.a1b2c3d4";
                const string bindingId = "a1b2c3d4000011112222333344445555";
                sourceTable.Configure("zh-CN", new[] { new LocalizationAuthoringEntry(key, "确认") });
                englishTable.Configure("en-US", Array.Empty<LocalizationAuthoringEntry>());

                var zh = new LocalizationWorkspaceLanguage();
                zh.Configure("zh-CN", "中文", sourceTable);
                var en = new LocalizationWorkspaceLanguage();
                en.Configure("en-US", "English", englishTable);
                workspace.Configure(null, "zh-CN", new[] { zh, en });
                registry.Upsert(
                    bindingId,
                    key,
                    "prefab-guid",
                    123,
                    "Panel_Login/Mid/BtnConfirm/Text",
                    typeof(Text).FullName,
                    "zh-CN",
                    "确认",
                    LocalizationSourceStatus.Synced);

                Assert.That(
                    LocalizationTranslationExchange.TryExportJson(
                        workspace,
                        registry,
                        filePath,
                        out string exportError),
                    Is.True,
                    exportError);
                string quote = ((char)34).ToString();
                string json = File.ReadAllText(filePath).Replace(
                    quote + "value" + quote + ": " + quote + quote,
                    quote + "value" + quote + ": " + quote + "Confirm" + quote);
                File.WriteAllText(filePath, json);

                sourceTable.Configure(
                    "zh-CN",
                    new[] { new LocalizationAuthoringEntry(key, "确认登录") });

                Assert.That(
                    LocalizationTranslationExchange.TryImportJson(
                        workspace,
                        registry,
                        filePath,
                        out LocalizationTranslationImportReport report,
                        out string importError),
                    Is.True,
                    importError);
                Assert.That(report.AppliedCount, Is.EqualTo(0));
                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Status == LocalizationTranslationImportStatus.StaleSource),
                    Is.True);
                Assert.That(englishTable.Entries.Count, Is.EqualTo(0));
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                UnityEngine.Object.DestroyImmediate(registry);
                UnityEngine.Object.DestroyImmediate(workspace);
                UnityEngine.Object.DestroyImmediate(englishTable);
                UnityEngine.Object.DestroyImmediate(sourceTable);
            }
        }

        [Test]
        public void CsvTranslationExchangeExportsExcelFriendlyColumns()
        {
            var sourceTable = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            var englishTable = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            var workspace = ScriptableObject.CreateInstance<LocalizationWorkspaceAsset>();
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            string filePath = Path.Combine(Path.GetTempPath(), "stellar-localization.csv");
            try
            {
                const string key = "ui.panel_login.title.12345678";
                sourceTable.Configure("zh-CN", new[] { new LocalizationAuthoringEntry(key, "登录") });
                englishTable.Configure("en-US", Array.Empty<LocalizationAuthoringEntry>());
                var zh = new LocalizationWorkspaceLanguage();
                zh.Configure("zh-CN", "中文", sourceTable);
                var en = new LocalizationWorkspaceLanguage();
                en.Configure("en-US", "English", englishTable);
                workspace.Configure(null, "zh-CN", new[] { zh, en });
                registry.Upsert(
                    "1234567890abcdef1234567890abcdef",
                    key,
                    "prefab-guid",
                    456,
                    "Panel_Login/Top/Title",
                    typeof(Text).FullName,
                    "zh-CN",
                    "登录",
                    LocalizationSourceStatus.Synced);

                Assert.That(
                    LocalizationTranslationExchange.TryExportCsv(
                        workspace,
                        registry,
                        filePath,
                        out string error),
                    Is.True,
                    error);
                string csv = File.ReadAllText(filePath);
                Assert.That(
                    csv,
                    Does.Contain("key,bindingId,source,sourceHash,prefabGuid,hierarchy,componentType,en-US"));
                Assert.That(csv, Does.Contain("Panel_Login/Top/Title"));
                Assert.That(csv, Does.Contain("登录"));
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                UnityEngine.Object.DestroyImmediate(registry);
                UnityEngine.Object.DestroyImmediate(workspace);
                UnityEngine.Object.DestroyImmediate(englishTable);
                UnityEngine.Object.DestroyImmediate(sourceTable);
            }
        }

        private static void CreatePlainPrefab(string path)
        {
            GameObject root = new GameObject("Panel_Login", typeof(RectTransform));
            try
            {
                GameObject top = new GameObject("Top", typeof(RectTransform));
                top.transform.SetParent(root.transform, false);
                CreateText(top.transform, "Title", "登录");

                GameObject mid = new GameObject("Mid", typeof(RectTransform));
                mid.transform.SetParent(root.transform, false);
                CreateText(mid.transform, "BtnConfirm", "确认");
                CreateText(mid.transform, "BtnQuit", "退出");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateBoundPrefab(
            string path,
            string bindingId,
            string key,
            bool onlyQuit = false)
        {
            GameObject root = new GameObject("Panel_Login", typeof(RectTransform));
            try
            {
                if (!onlyQuit)
                {
                    Text confirm = CreateText(root.transform, "BtnConfirm", "确认");
                    LocalizedTextView confirmView = confirm.gameObject.AddComponent<LocalizedTextView>();
                    confirmView.ConfigureBinding(bindingId, confirm, key);
                    CreateText(root.transform, "BtnQuit", "退出");
                }
                else
                {
                    Text quit = CreateText(root.transform, "BtnQuit", "退出");
                    LocalizedTextView quitView = quit.gameObject.AddComponent<LocalizedTextView>();
                    quitView.ConfigureBinding(bindingId, quit, key);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Text CreateText(Transform parent, string name, string value)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = value;
            return text;
        }
    }
}
