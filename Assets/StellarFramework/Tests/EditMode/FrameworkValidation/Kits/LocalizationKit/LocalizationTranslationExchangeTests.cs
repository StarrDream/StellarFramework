using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using StellarFramework.Localization.Editor;
using StellarFramework.Localization.UnityUGUI;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class LocalizationTranslationExchangeTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "StellarFramework_LocalizationExchangeTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        [Test]
        public void JsonExportContainsIdentityHashAndContext()
        {
            LocalizationTableAsset zh = CreateTable(
                "zh-CN",
                new LocalizationAuthoringEntry("ui.panel_login.btn_confirm.c4729f11", "确认"));
            LocalizationTableAsset en = CreateTable("en-US");
            LocalizationWorkspaceAsset workspace = CreateWorkspace(zh, en);
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            registry.Upsert(
                "c4729f11d92e44ecbca0c81a9a832222",
                "ui.panel_login.btn_confirm.c4729f11",
                "prefab-guid-login",
                42,
                "Panel_Login/Mid/BtnConfirm/Text",
                "UnityEngine.UI.Text",
                "zh-CN",
                "确认",
                LocalizationSourceStatus.Synced);

            string path = Path.Combine(_tempDirectory, "translations.json");
            Assert.That(
                LocalizationTranslationExchange.TryExportJson(
                    workspace,
                    registry,
                    path,
                    out string error),
                Is.True,
                error);

            string json = File.ReadAllText(path, Encoding.UTF8);
            Assert.That(json, Does.Contain("\"schemaVersion\": 1"));
            Assert.That(json, Does.Contain("ui.panel_login.btn_confirm.c4729f11"));
            Assert.That(json, Does.Contain("c4729f11d92e44ecbca0c81a9a832222"));
            Assert.That(json, Does.Contain("Panel_Login/Mid/BtnConfirm/Text"));
            Assert.That(json, Does.Contain(LocalizationSourceRecord.ComputeSourceHash("确认")));
            Assert.That(json, Does.Contain("\"locale\": \"en-US\""));

            Destroy(workspace, registry, zh, en);
        }

        [Test]
        public void CsvRoundTripImportsExternalTranslation()
        {
            LocalizationTableAsset zh = CreateTable(
                "zh-CN",
                new LocalizationAuthoringEntry("ui.panel_login.title.8f31c2a4", "登录"),
                new LocalizationAuthoringEntry("ui.panel_login.btn_quit.a9917bd3", "退出"));
            LocalizationTableAsset en = CreateTable("en-US");
            LocalizationWorkspaceAsset workspace = CreateWorkspace(zh, en);
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            string path = Path.Combine(_tempDirectory, "translations.csv");

            Assert.That(
                LocalizationTranslationExchange.TryExportCsv(
                    workspace,
                    registry,
                    path,
                    out string exportError),
                Is.True,
                exportError);

            string[] lines = File.ReadAllText(path, Encoding.UTF8)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines[0], Does.EndWith(",en-US"));
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("ui.panel_login.title.8f31c2a4,", StringComparison.Ordinal))
                {
                    lines[i] += "Login";
                }
                else if (lines[i].StartsWith("ui.panel_login.btn_quit.a9917bd3,", StringComparison.Ordinal))
                {
                    lines[i] += "Exit";
                }
            }
            File.WriteAllText(
                path,
                string.Join("\r\n", lines) + "\r\n",
                new UTF8Encoding(true));

            Assert.That(
                LocalizationTranslationExchange.TryImportCsv(
                    workspace,
                    registry,
                    path,
                    out LocalizationTranslationImportReport report,
                    out string importError),
                Is.True,
                importError);
            Assert.That(report.AppliedCount, Is.EqualTo(2));
            Assert.That(report.Issues, Is.Empty);
            Assert.That(
                en.Entries.Single(entry => entry.Key == "ui.panel_login.title.8f31c2a4").Value,
                Is.EqualTo("Login"));
            Assert.That(
                en.Entries.Single(entry => entry.Key == "ui.panel_login.btn_quit.a9917bd3").Value,
                Is.EqualTo("Exit"));

            Destroy(workspace, registry, zh, en);
        }

        [Test]
        public void ImportRejectsStaleSourceHash()
        {
            LocalizationTableAsset zh = CreateTable(
                "zh-CN",
                new LocalizationAuthoringEntry("ui.panel_login.btn_confirm.c4729f11", "确认"));
            LocalizationTableAsset en = CreateTable("en-US");
            LocalizationWorkspaceAsset workspace = CreateWorkspace(zh, en);
            var registry = ScriptableObject.CreateInstance<LocalizationSourceRegistry>();
            registry.Upsert(
                "c4729f11d92e44ecbca0c81a9a832222",
                "ui.panel_login.btn_confirm.c4729f11",
                "prefab-guid-login",
                42,
                "Panel_Login/Mid/BtnConfirm/Text",
                "UnityEngine.UI.Text",
                "zh-CN",
                "确认",
                LocalizationSourceStatus.Synced);

            string path = Path.Combine(_tempDirectory, "translations.csv");
            Assert.That(
                LocalizationTranslationExchange.TryExportCsv(
                    workspace,
                    registry,
                    path,
                    out string exportError),
                Is.True,
                exportError);

            string[] lines = File.ReadAllText(path, Encoding.UTF8)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            lines[1] += "Confirm";
            File.WriteAllText(
                path,
                string.Join("\r\n", lines) + "\r\n",
                new UTF8Encoding(true));

            zh.Configure(
                "zh-CN",
                new[]
                {
                    new LocalizationAuthoringEntry(
                        "ui.panel_login.btn_confirm.c4729f11",
                        "确认登录")
                });

            Assert.That(
                LocalizationTranslationExchange.TryImportCsv(
                    workspace,
                    registry,
                    path,
                    out LocalizationTranslationImportReport report,
                    out string importError),
                Is.True,
                importError);
            Assert.That(report.AppliedCount, Is.Zero);
            Assert.That(
                report.Issues.Any(issue =>
                    issue.Status == LocalizationTranslationImportStatus.StaleSource),
                Is.True);
            Assert.That(en.Entries, Is.Empty);

            Destroy(workspace, registry, zh, en);
        }

        private static LocalizationWorkspaceAsset CreateWorkspace(
            LocalizationTableAsset source,
            LocalizationTableAsset target)
        {
            var sourceLanguage = new LocalizationWorkspaceLanguage();
            sourceLanguage.Configure("zh-CN", "中文", source);
            var targetLanguage = new LocalizationWorkspaceLanguage();
            targetLanguage.Configure("en-US", "English", target);

            var workspace = ScriptableObject.CreateInstance<LocalizationWorkspaceAsset>();
            workspace.Configure(
                null,
                "zh-CN",
                new[] { sourceLanguage, targetLanguage });
            return workspace;
        }

        private static LocalizationTableAsset CreateTable(
            string locale,
            params LocalizationAuthoringEntry[] entries)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            table.Configure(locale, entries ?? Array.Empty<LocalizationAuthoringEntry>());
            return table;
        }

        private static void Destroy(params UnityEngine.Object[] values)
        {
            foreach (UnityEngine.Object value in values)
            {
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }
        }
    }
}
