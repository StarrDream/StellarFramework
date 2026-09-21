using System;
using System.Collections.Generic;
using NUnit.Framework;
using StellarFramework.Localization;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class LocalizationKitCoreTests
    {
        [Test]
        public void LocaleIdCanonicalizesLanguageRegionAndScript()
        {
            Assert.That(LocaleId.From("ZH-cn").Value, Is.EqualTo("zh-CN"));
            Assert.That(LocaleId.From("zh-hans-cn").Value, Is.EqualTo("zh-Hans-CN"));
            Assert.That(LocaleId.From("EN-us").Value, Is.EqualTo("en-US"));
        }

        [Test]
        public void LocaleIdRejectsMalformedTokens()
        {
            Assert.That(LocaleId.TryCreate("", out _, out _), Is.False);
            Assert.That(LocaleId.TryCreate("z", out _, out _), Is.False);
            Assert.That(LocaleId.TryCreate("zh--CN", out _, out _), Is.False);
            Assert.That(LocaleId.TryCreate(" zh-CN", out _, out _), Is.False);
        }

        [Test]
        public void LocalizationKeyIsStableAndRejectsWhitespaceOrUnsupportedCharacters()
        {
            LocalizationKey key = LocalizationKey.From("sample.menu/start");
            Assert.That(key.Value, Is.EqualTo("sample.menu/start"));
            Assert.That(LocalizationKey.TryCreate(" sample.key", out _, out _), Is.False);
            Assert.That(LocalizationKey.TryCreate("sample key", out _, out _), Is.False);
            Assert.That(LocalizationKey.TryCreate("示例.key", out _, out _), Is.False);
        }

        [Test]
        public void TableCopiesSortsAndLooksUpEntriesDeterministically()
        {
            LocalizationEntry[] source =
            {
                Entry("z.key", "Z"),
                Entry("a.key", "A"),
                Entry("m.key", "M")
            };
            LocalizationTable table = new LocalizationTable(Zh(), source);
            source[0] = Entry("other.key", "Changed");

            Assert.That(table.Count, Is.EqualTo(3));
            Assert.That(table.GetEntry(0).Key.Value, Is.EqualTo("a.key"));
            Assert.That(table.GetEntry(1).Key.Value, Is.EqualTo("m.key"));
            Assert.That(table.GetEntry(2).Key.Value, Is.EqualTo("z.key"));
            Assert.That(table.GetRequired(LocalizationKey.From("z.key")), Is.EqualTo("Z"));
        }

        [Test]
        public void TableRejectsDuplicateKey()
        {
            LocalizationEntry[] entries =
            {
                Entry("same.key", "A"),
                Entry("same.key", "B")
            };
            Assert.Throws<ArgumentException>(() => new LocalizationTable(Zh(), entries));
        }

        [Test]
        public void CatalogCopiesSortsAndRejectsDuplicateLocale()
        {
            LocalizationTable zh = Table(Zh(), Entry("key", "中文"));
            LocalizationTable en = Table(En(), Entry("key", "English"));
            LocalizationTable[] source = { zh, en };
            LocalizationCatalog catalog = new LocalizationCatalog(source);
            source[0] = null;

            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.GetTable(0).Locale, Is.EqualTo(En()));
            Assert.That(catalog.GetTable(1).Locale, Is.EqualTo(Zh()));
            Assert.Throws<ArgumentException>(() =>
                new LocalizationCatalog(new[] { zh, zh }));
        }

        [Test]
        public void ServiceLooksUpChineseAndEnglishAndSwitchesLocaleOnce()
        {
            LocalizationService service = Service();
            int eventCount = 0;
            LocaleId previous = default(LocaleId);
            LocaleId current = default(LocaleId);
            service.LocaleChanged += (_, args) =>
            {
                eventCount++;
                previous = args.PreviousLocale;
                current = args.CurrentLocale;
            };

            Assert.That(service.GetRequired(LocalizationKey.From("ui.hello")), Is.EqualTo("你好"));
            Assert.That(service.SetLocale(En(), out string error), Is.True, error);
            Assert.That(service.GetRequired(LocalizationKey.From("ui.hello")), Is.EqualTo("Hello"));
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(previous, Is.EqualTo(Zh()));
            Assert.That(current, Is.EqualTo(En()));

            Assert.That(service.SetLocale(En(), out error), Is.True, error);
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void UnknownLocaleDoesNotMutateCurrentLocale()
        {
            LocalizationService service = Service();
            Assert.That(service.SetLocale(LocaleId.From("fr-FR"), out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(service.CurrentLocale, Is.EqualTo(Zh()));
        }

        [Test]
        public void MissingKeyReturnsExplicitStatusAndRequiredThrows()
        {
            LocalizationService service = Service();
            LocalizationKey missing = LocalizationKey.From("ui.missing");
            LocalizationLookupResult result = service.Lookup(missing);

            Assert.That(result.Status, Is.EqualTo(LocalizationLookupStatus.MissingKey));
            Assert.That(result.Success, Is.False);
            Assert.That(service.TryGet(missing, out string value), Is.False);
            Assert.That(value, Is.Null);
            Assert.Throws<KeyNotFoundException>(() => service.GetRequired(missing));
        }

        [Test]
        public void DirectLookupReportsMissingLocaleWithoutChangingService()
        {
            LocalizationService service = Service();
            LocalizationLookupResult result = service.Lookup(
                LocaleId.From("fr-FR"), LocalizationKey.From("ui.hello"));
            Assert.That(result.Status, Is.EqualTo(LocalizationLookupStatus.MissingLocale));
            Assert.That(service.CurrentLocale, Is.EqualTo(Zh()));
        }

        [Test]
        public void FallbackResolvesExplicitlyAndReportsResolvedLocale()
        {
            LocalizationTable zh = Table(Zh(), Entry("ui.zh_only", "仅中文"));
            LocalizationTable en = Table(En(), Entry("ui.shared", "English fallback"));
            LocalizationCatalog catalog = new LocalizationCatalog(new[] { zh, en });
            LocalizationFallbackPolicy fallback = new LocalizationFallbackPolicy(new[] { En() });
            LocalizationService service = new LocalizationService(catalog, Zh(), fallback);

            LocalizationLookupResult result = service.Lookup(LocalizationKey.From("ui.shared"));
            Assert.That(result.Success, Is.True);
            Assert.That(result.UsedFallback, Is.True);
            Assert.That(result.RequestedLocale, Is.EqualTo(Zh()));
            Assert.That(result.ResolvedLocale, Is.EqualTo(En()));
            Assert.That(result.Value, Is.EqualTo("English fallback"));
        }

        [Test]
        public void ServiceRejectsFallbackLocaleMissingFromCatalog()
        {
            LocalizationCatalog catalog = new LocalizationCatalog(new[]
            {
                Table(Zh(), Entry("key", "value"))
            });
            LocalizationFallbackPolicy policy = new LocalizationFallbackPolicy(
                new[] { LocaleId.From("fr-FR") });

            Assert.Throws<ArgumentException>(() => new LocalizationService(catalog, Zh(), policy));
        }

        [Test]
        public void FallbackPolicyRejectsDuplicateLocale()
        {
            Assert.Throws<ArgumentException>(() =>
                new LocalizationFallbackPolicy(new[] { En(), En() }));
        }

        [Test]
        public void NamedFormattingSupportsEscapedBraces()
        {
            LocalizationEntry[] zhEntries =
            {
                Entry("ui.hello", "你好"),
                Entry("ui.welcome", "{{用户}} {name}，分数 {score}")
            };
            LocalizationCatalog catalog = new LocalizationCatalog(new[]
            {
                new LocalizationTable(Zh(), zhEntries)
            });
            LocalizationService service = new LocalizationService(catalog, Zh());
            LocalizationFormatArgument[] args =
            {
                new LocalizationFormatArgument("name", "小明"),
                new LocalizationFormatArgument("score", "100")
            };

            Assert.That(service.TryFormat(
                LocalizationKey.From("ui.welcome"), args, out string text, out string error),
                Is.True, error);
            Assert.That(text, Is.EqualTo("{用户} 小明，分数 100"));
        }

        [Test]
        public void NamedFormattingRejectsMissingOrDuplicateArgumentsAndMalformedTemplate()
        {
            LocalizationFormatArgument[] missing =
            {
                new LocalizationFormatArgument("name", "A")
            };
            Assert.That(LocalizationTemplateFormatter.TryFormat(
                "{name}:{score}", missing, out _, out string missingError), Is.False);
            Assert.That(missingError, Does.Contain("score"));

            LocalizationFormatArgument[] duplicate =
            {
                new LocalizationFormatArgument("name", "A"),
                new LocalizationFormatArgument("name", "B")
            };
            Assert.That(LocalizationTemplateFormatter.TryFormat(
                "{name}", duplicate, out _, out string duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("Duplicate"));

            Assert.That(LocalizationTemplateFormatter.TryFormat(
                "{name", missing, out _, out string malformedError), Is.False);
            Assert.That(malformedError, Does.Contain("unclosed"));
        }

        private static LocalizationService Service()
        {
            LocalizationTable zh = Table(
                Zh(),
                Entry("ui.hello", "你好"),
                Entry("ui.only_zh", "中文"));
            LocalizationTable en = Table(
                En(),
                Entry("ui.hello", "Hello"),
                Entry("ui.only_en", "English"));
            return new LocalizationService(new LocalizationCatalog(new[] { zh, en }), Zh());
        }

        private static LocaleId Zh() => LocaleId.From("zh-CN");
        private static LocaleId En() => LocaleId.From("en-US");
        private static LocalizationEntry Entry(string key, string value) =>
            new LocalizationEntry(LocalizationKey.From(key), value);
        private static LocalizationTable Table(LocaleId locale, params LocalizationEntry[] entries) =>
            new LocalizationTable(locale, entries);
    }
}
