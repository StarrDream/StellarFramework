using System;
using System.Linq;
using NUnit.Framework;
using StellarFramework.Localization;
using StellarFramework.Localization.Editor;
using StellarFramework.Localization.SettingsAdapter;
using StellarFramework.Localization.UnityUGUI;
using StellarFramework.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class LocalizationKitAdapterTests
    {
        [Test]
        public void SettingsAdapterExposesCatalogLocalesAndAppliesLanguage()
        {
            LocalizationService service = CreateService();
            var adapter = new LocalizationLanguageSettingsAdapter(service);

            Assert.That(adapter.GetLanguageOptions().Count, Is.EqualTo(2));
            Assert.That(adapter.GetLanguageOptions()[0].Value, Is.EqualTo("en-US"));
            Assert.That(adapter.GetLanguageOptions()[1].Value, Is.EqualTo("zh-CN"));
            Assert.That(adapter.GetCurrentLanguageValue(), Is.EqualTo("zh-CN"));
            Assert.That(adapter.ApplyLanguage("en-US", out string error), Is.True, error);
            Assert.That(service.CurrentLocale, Is.EqualTo(LocaleId.From("en-US")));
            Assert.That(adapter.GetCurrentLanguageValue(), Is.EqualTo("en-US"));
        }

        [Test]
        public void SettingsAdapterSupportsConfiguredLabelsAndRejectsInvalidOptionSet()
        {
            LocalizationService service = CreateService();
            var options = new[]
            {
                new LocalizationLanguageOption(LocaleId.From("zh-CN"), "简体中文", "Chinese"),
                new LocalizationLanguageOption(LocaleId.From("en-US"), "English", "英语")
            };
            var adapter = new LocalizationLanguageSettingsAdapter(service, options);

            Assert.That(adapter.GetLanguageOptions()[0].Label, Is.EqualTo("简体中文"));
            Assert.That(adapter.GetLanguageOptions()[1].Label, Is.EqualTo("English"));
            Assert.Throws<ArgumentException>(() =>
                new LocalizationLanguageSettingsAdapter(service, new[]
                {
                    new LocalizationLanguageOption(LocaleId.From("fr-FR"), "French")
                }));
            Assert.Throws<ArgumentException>(() =>
                new LocalizationLanguageSettingsAdapter(service, new[]
                {
                    options[0],
                    options[0]
                }));
        }

        [Test]
        public void SettingsAdapterInvalidLanguageDoesNotMutateService()
        {
            LocalizationService service = CreateService();
            var adapter = new LocalizationLanguageSettingsAdapter(service);

            Assert.That(adapter.ApplyLanguage("bad locale", out string invalidError), Is.False);
            Assert.That(invalidError, Is.Not.Empty);
            Assert.That(service.CurrentLocale.Value, Is.EqualTo("zh-CN"));

            Assert.That(adapter.ApplyLanguage("fr-FR", out string missingError), Is.False);
            Assert.That(missingError, Does.Contain("does not exist"));
            Assert.That(service.CurrentLocale.Value, Is.EqualTo("zh-CN"));
        }

        [Test]
        public void TableAssetBuildsRealCoreTableAndRejectsDuplicateKey()
        {
            LocalizationTableAsset tableAsset = null;
            LocalizationTableAsset duplicateAsset = null;
            try
            {
                tableAsset = CreateTableAsset(
                    "zh-CN",
                    new LocalizationAuthoringEntry("ui.title", "标题"),
                    new LocalizationAuthoringEntry("ui.start", "开始"));
                Assert.That(tableAsset.TryBuild(out LocalizationTable table, out string error), Is.True, error);
                Assert.That(table.Locale.Value, Is.EqualTo("zh-CN"));
                Assert.That(table.GetRequired(LocalizationKey.From("ui.start")), Is.EqualTo("开始"));

                duplicateAsset = CreateTableAsset(
                    "zh-CN",
                    new LocalizationAuthoringEntry("ui.title", "A"),
                    new LocalizationAuthoringEntry("ui.title", "B"));
                Assert.That(duplicateAsset.TryBuild(out _, out string duplicateError), Is.False);
                Assert.That(duplicateError, Does.Contain("Duplicate"));
            }
            finally
            {
                DestroyAsset(tableAsset);
                DestroyAsset(duplicateAsset);
            }
        }

        [Test]
        public void CatalogAssetBuildsServiceWithExplicitFallback()
        {
            LocalizationTableAsset zh = null;
            LocalizationTableAsset en = null;
            LocalizationCatalogAsset catalog = null;
            try
            {
                zh = CreateTableAsset("zh-CN", new LocalizationAuthoringEntry("ui.zh", "中文"));
                en = CreateTableAsset("en-US", new LocalizationAuthoringEntry("ui.shared", "Fallback"));
                catalog = CreateCatalogAsset(new[] { zh, en }, "zh-CN", new[] { "en-US" });

                Assert.That(catalog.TryBuildService(out LocalizationService service, out string error), Is.True, error);
                LocalizationLookupResult result = service.Lookup(LocalizationKey.From("ui.shared"));
                Assert.That(result.Success, Is.True);
                Assert.That(result.UsedFallback, Is.True);
                Assert.That(result.ResolvedLocale.Value, Is.EqualTo("en-US"));
            }
            finally
            {
                DestroyAsset(catalog);
                DestroyAsset(zh);
                DestroyAsset(en);
            }
        }

        [Test]
        public void LocalizationContextRelaysLocaleChanges()
        {
            LocalizationTableAsset zh = null;
            LocalizationTableAsset en = null;
            LocalizationCatalogAsset catalog = null;
            GameObject root = null;
            try
            {
                zh = CreateTableAsset("zh-CN", new LocalizationAuthoringEntry("ui.title", "标题"));
                en = CreateTableAsset("en-US", new LocalizationAuthoringEntry("ui.title", "Title"));
                catalog = CreateCatalogAsset(new[] { zh, en }, "zh-CN");
                root = new GameObject("LocalizationContext_Test");
                root.SetActive(false);
                LocalizationContext context = root.AddComponent<LocalizationContext>();
                context.Configure(catalog);
                Assert.That(context.TryInitialize(out string error), Is.True, error);

                int count = 0;
                context.LocaleChanged += (_, args) =>
                {
                    count++;
                    Assert.That(args.PreviousLocale.Value, Is.EqualTo("zh-CN"));
                    Assert.That(args.CurrentLocale.Value, Is.EqualTo("en-US"));
                };
                Assert.That(context.SetLocale("en-US", out error), Is.True, error);
                Assert.That(count, Is.EqualTo(1));
            }
            finally
            {
                DestroyObject(root);
                DestroyAsset(catalog);
                DestroyAsset(zh);
                DestroyAsset(en);
            }
        }

        [Test]
        public void LocalizedTextViewRefreshesImmediatelyAndOnLocaleChange()
        {
            LocalizationTableAsset zh = null;
            LocalizationTableAsset en = null;
            LocalizationCatalogAsset catalog = null;
            GameObject root = null;
            try
            {
                zh = CreateTableAsset("zh-CN", new LocalizationAuthoringEntry("ui.title", "标题"));
                en = CreateTableAsset("en-US", new LocalizationAuthoringEntry("ui.title", "Title"));
                catalog = CreateCatalogAsset(new[] { zh, en }, "zh-CN");
                root = new GameObject("LocalizedText_Test");
                root.SetActive(false);
                LocalizationContext context = root.AddComponent<LocalizationContext>();
                Text text = root.AddComponent<Text>();
                LocalizedTextView view = root.AddComponent<LocalizedTextView>();
                context.Configure(catalog);
                view.Configure(context, text, "ui.title");

                root.SetActive(true);
                Assert.That(view.Bind(out string bindError), Is.True, bindError);
                Assert.That(text.text, Is.EqualTo("标题"));
                Assert.That(context.SetLocale("en-US", out string error), Is.True, error);
                Assert.That(text.text, Is.EqualTo("Title"));
            }
            finally
            {
                DestroyObject(root);
                DestroyAsset(catalog);
                DestroyAsset(zh);
                DestroyAsset(en);
            }
        }

        [Test]
        public void LocalizedTextViewReturnsExplicitMissingKeyFailureWithoutOverwritingTarget()
        {
            LocalizationTableAsset zh = null;
            LocalizationCatalogAsset catalog = null;
            GameObject root = null;
            try
            {
                zh = CreateTableAsset("zh-CN", new LocalizationAuthoringEntry("ui.title", "标题"));
                catalog = CreateCatalogAsset(new[] { zh }, "zh-CN");
                root = new GameObject("LocalizedText_Missing_Test");
                root.SetActive(false);
                LocalizationContext context = root.AddComponent<LocalizationContext>();
                Text text = root.AddComponent<Text>();
                text.text = "Original";
                LocalizedTextView view = root.AddComponent<LocalizedTextView>();
                context.Configure(catalog);
                view.Configure(context, text, "ui.missing");

                Assert.That(view.Refresh(out string error), Is.False);
                Assert.That(error, Does.Contain("MissingKey"));
                Assert.That(text.text, Is.EqualTo("Original"));
            }
            finally
            {
                DestroyObject(root);
                DestroyAsset(catalog);
                DestroyAsset(zh);
            }
        }

        [Test]
        public void LocalizedButtonLabelDelegatesToLocalizedTextView()
        {
            LocalizationTableAsset zh = null;
            LocalizationCatalogAsset catalog = null;
            GameObject root = null;
            try
            {
                zh = CreateTableAsset("zh-CN", new LocalizationAuthoringEntry("ui.button", "执行"));
                catalog = CreateCatalogAsset(new[] { zh }, "zh-CN");
                root = new GameObject("LocalizedButton_Test");
                root.SetActive(false);
                root.AddComponent<Button>();
                Text text = root.AddComponent<Text>();
                LocalizationContext context = root.AddComponent<LocalizationContext>();
                LocalizedTextView view = root.AddComponent<LocalizedTextView>();
                LocalizedButtonLabel buttonLabel = root.AddComponent<LocalizedButtonLabel>();
                context.Configure(catalog);
                view.Configure(context, text, "ui.button");
                buttonLabel.Configure(view);

                Assert.That(buttonLabel.Refresh(out string error), Is.True, error);
                Assert.That(text.text, Is.EqualTo("执行"));
            }
            finally
            {
                DestroyObject(root);
                DestroyAsset(catalog);
                DestroyAsset(zh);
            }
        }

        [Test]
        public void ValidatorAcceptsCompleteChineseEnglishCoverage()
        {
            LocalizationTableAsset zh = null;
            LocalizationTableAsset en = null;
            LocalizationCatalogAsset catalog = null;
            try
            {
                zh = CreateTableAsset(
                    "zh-CN",
                    new LocalizationAuthoringEntry("ui.title", "标题"),
                    new LocalizationAuthoringEntry("ui.start", "开始"));
                en = CreateTableAsset(
                    "en-US",
                    new LocalizationAuthoringEntry("ui.title", "Title"),
                    new LocalizationAuthoringEntry("ui.start", "Start"));
                catalog = CreateCatalogAsset(new[] { zh, en }, "zh-CN", new[] { "en-US" });

                LocalizationCoverageReport report = LocalizationCatalogValidator.Validate(catalog);
                Assert.That(report.IsValid, Is.True);
                Assert.That(report.ErrorCount, Is.Zero);
                Assert.That(report.UnionKeyCount, Is.EqualTo(2));
                Assert.That(report.RequiredCellCount, Is.EqualTo(4));
                Assert.That(report.PresentCellCount, Is.EqualTo(4));
                Assert.That(report.CoveragePercent, Is.EqualTo(100.0));
            }
            finally
            {
                DestroyAsset(catalog);
                DestroyAsset(zh);
                DestroyAsset(en);
            }
        }

        [Test]
        public void ValidatorReportsMissingKeyAndEmptyValue()
        {
            LocalizationTableAsset zh = null;
            LocalizationTableAsset en = null;
            LocalizationCatalogAsset catalog = null;
            try
            {
                zh = CreateTableAsset(
                    "zh-CN",
                    new LocalizationAuthoringEntry("ui.title", "标题"),
                    new LocalizationAuthoringEntry("ui.start", "开始"));
                en = CreateTableAsset(
                    "en-US",
                    new LocalizationAuthoringEntry("ui.title", ""));
                catalog = CreateCatalogAsset(new[] { zh, en }, "zh-CN");

                LocalizationCoverageReport report = LocalizationCatalogValidator.Validate(catalog);
                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Issues.Any(issue => issue.Code == "EmptyValue"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "MissingKey" && issue.Key == "ui.start"), Is.True);
                Assert.That(report.CoveragePercent, Is.LessThan(100.0));
            }
            finally
            {
                DestroyAsset(catalog);
                DestroyAsset(zh);
                DestroyAsset(en);
            }
        }

        [Test]
        public void ValidatorReportsDuplicateKeyLocaleAndMissingFallback()
        {
            LocalizationTableAsset zhA = null;
            LocalizationTableAsset zhB = null;
            LocalizationTableAsset en = null;
            LocalizationCatalogAsset catalog = null;
            try
            {
                zhA = CreateTableAsset(
                    "zh-CN",
                    new LocalizationAuthoringEntry("ui.title", "A"),
                    new LocalizationAuthoringEntry("ui.title", "B"));
                zhB = CreateTableAsset("zh-CN", new LocalizationAuthoringEntry("ui.title", "C"));
                en = CreateTableAsset("en-US", new LocalizationAuthoringEntry("ui.title", "Title"));
                catalog = CreateCatalogAsset(new[] { zhA, zhB, en }, "zh-CN", new[] { "fr-FR" });

                LocalizationCoverageReport report = LocalizationCatalogValidator.Validate(catalog);
                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Issues.Any(issue => issue.Code == "DuplicateKey"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "DuplicateLocale"), Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "MissingFallbackLocale"), Is.True);
            }
            finally
            {
                DestroyAsset(catalog);
                DestroyAsset(zhA);
                DestroyAsset(zhB);
                DestroyAsset(en);
            }
        }

        [Test]
        public void ValidatorAcceptsMatchingPlaceholderContractsAcrossLocales()
        {
            LocalizationTableAsset zh = null;
            LocalizationTableAsset en = null;
            LocalizationCatalogAsset catalog = null;
            try
            {
                zh = CreateTableAsset(
                    "zh-CN",
                    new LocalizationAuthoringEntry("ui.count", "剩余 {count} 个，用户 {name}"));
                en = CreateTableAsset(
                    "en-US",
                    new LocalizationAuthoringEntry("ui.count", "Remaining {count}, user {name}"));
                catalog = CreateCatalogAsset(new[] { zh, en }, "zh-CN");

                LocalizationCoverageReport report = LocalizationCatalogValidator.Validate(catalog);
                Assert.That(report.IsValid, Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "PlaceholderMismatch"), Is.False);
                Assert.That(report.Issues.Any(issue => issue.Code == "InvalidTemplate"), Is.False);
            }
            finally
            {
                DestroyAsset(catalog);
                DestroyAsset(zh);
                DestroyAsset(en);
            }
        }

        [Test]
        public void ValidatorRejectsPlaceholderContractMismatchAcrossLocales()
        {
            LocalizationTableAsset zh = null;
            LocalizationTableAsset en = null;
            LocalizationCatalogAsset catalog = null;
            try
            {
                zh = CreateTableAsset(
                    "zh-CN",
                    new LocalizationAuthoringEntry("ui.count", "剩余 {count} 个"));
                en = CreateTableAsset(
                    "en-US",
                    new LocalizationAuthoringEntry("ui.count", "Remaining {amount}"));
                catalog = CreateCatalogAsset(new[] { zh, en }, "zh-CN");

                LocalizationCoverageReport report = LocalizationCatalogValidator.Validate(catalog);
                Assert.That(report.IsValid, Is.False);
                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Code == "PlaceholderMismatch" &&
                        issue.Locale == "en-US" &&
                        issue.Key == "ui.count"),
                    Is.True);
            }
            finally
            {
                DestroyAsset(catalog);
                DestroyAsset(zh);
                DestroyAsset(en);
            }
        }

        [Test]
        public void ValidatorRejectsInvalidTemplateSyntaxBeforeRuntimeSwitch()
        {
            LocalizationTableAsset zh = null;
            LocalizationTableAsset en = null;
            LocalizationCatalogAsset catalog = null;
            try
            {
                zh = CreateTableAsset(
                    "zh-CN",
                    new LocalizationAuthoringEntry("ui.count", "剩余 {count} 个"));
                en = CreateTableAsset(
                    "en-US",
                    new LocalizationAuthoringEntry("ui.count", "Remaining {count"));
                catalog = CreateCatalogAsset(new[] { zh, en }, "zh-CN");

                LocalizationCoverageReport report = LocalizationCatalogValidator.Validate(catalog);
                Assert.That(report.IsValid, Is.False);
                Assert.That(
                    report.Issues.Any(issue =>
                        issue.Code == "InvalidTemplate" &&
                        issue.Locale == "en-US" &&
                        issue.Key == "ui.count"),
                    Is.True);
            }
            finally
            {
                DestroyAsset(catalog);
                DestroyAsset(zh);
                DestroyAsset(en);
            }
        }

        private static LocalizationService CreateService()
        {
            LocalizationTable zh = new LocalizationTable(
                LocaleId.From("zh-CN"),
                new[] { new LocalizationEntry(LocalizationKey.From("ui.title"), "标题") });
            LocalizationTable en = new LocalizationTable(
                LocaleId.From("en-US"),
                new[] { new LocalizationEntry(LocalizationKey.From("ui.title"), "Title") });
            return new LocalizationService(new LocalizationCatalog(new[] { zh, en }), LocaleId.From("zh-CN"));
        }

        private static LocalizationTableAsset CreateTableAsset(
            string locale,
            params LocalizationAuthoringEntry[] entries)
        {
            LocalizationTableAsset asset = ScriptableObject.CreateInstance<LocalizationTableAsset>();
            asset.name = "Table_" + locale;
            asset.Configure(locale, entries);
            return asset;
        }

        private static LocalizationCatalogAsset CreateCatalogAsset(
            LocalizationTableAsset[] tables,
            string initialLocale,
            string[] fallback = null)
        {
            LocalizationCatalogAsset asset = ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            asset.name = "Catalog_Test";
            asset.Configure(tables, initialLocale, fallback);
            return asset;
        }

        private static void DestroyAsset(UnityEngine.Object asset)
        {
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
