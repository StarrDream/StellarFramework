using System;
using System.Collections.Generic;
using NUnit.Framework;
using StellarFramework.Settings;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class SettingsKitCoreTests
    {
        [Test]
        public void RegisterProviderAfterInitOnlyAppliesNewEntries()
        {
            var manager = new SettingsManager();
            manager.Configure(new MemorySettingsStorage());

            var firstApply = new CountingApplyStrategy("First");
            manager.RegisterProvider(new SingleBoolProvider("page.a", "setting.a", firstApply));
            manager.Init();

            Assert.That(firstApply.ApplyCount, Is.EqualTo(1));

            var secondApply = new CountingApplyStrategy("Second");
            manager.RegisterProvider(new SingleBoolProvider("page.b", "setting.b", secondApply));

            Assert.That(firstApply.ApplyCount, Is.EqualTo(1),
                "Registering a provider after initialization must not re-apply unrelated existing settings.");
            Assert.That(secondApply.ApplyCount, Is.EqualTo(1),
                "The newly registered setting should be loaded and applied exactly once.");
        }

        [Test]
        public void DeferredSettingDoesNotApplyUntilSaveOrApplyPending()
        {
            var manager = new SettingsManager();
            manager.Configure(new MemorySettingsStorage());

            var apply = new CountingApplyStrategy("Deferred");
            manager.RegisterProvider(new SingleBoolProvider(
                "page.deferred",
                "setting.deferred",
                apply,
                applyImmediately: false));
            manager.Init();

            Assert.That(apply.ApplyCount, Is.EqualTo(1), "Initialization applies the effective starting value once.");
            Assert.That(manager.TrySetValue("setting.deferred", false, out string setError), Is.True, setError);
            Assert.That(apply.ApplyCount, Is.EqualTo(1), "Deferred edits must not apply immediately.");
            Assert.That(manager.HasDirtySettings, Is.True);

            Assert.That(manager.ApplyPending(out string applyError), Is.True, applyError);
            Assert.That(apply.ApplyCount, Is.EqualTo(2));
            Assert.That(manager.HasDirtySettings, Is.True,
                "ApplyPending applies runtime state but does not persist/mark the value saved.");
        }

        [Test]
        public void SavePersistsNormalizedValueAndClearsDirtyState()
        {
            var storage = new MemorySettingsStorage();
            var manager = new SettingsManager();
            manager.Configure(storage);

            manager.RegisterProvider(new FloatProvider());
            manager.Init();

            Assert.That(manager.TrySetValue("setting.volume", 2.0f, out string error), Is.True, error);
            Assert.That(manager.GetValue("setting.volume", -1f), Is.EqualTo(1f));
            Assert.That(manager.HasDirtySettings, Is.True);

            Assert.That(manager.Save(out error), Is.True, error);
            Assert.That(storage.Values["setting.volume"], Is.EqualTo("1"));
            Assert.That(storage.FlushCount, Is.EqualTo(1));
            Assert.That(manager.HasDirtySettings, Is.False);
        }

        private sealed class SingleBoolProvider : ISettingsPageProvider
        {
            private readonly string _pageId;
            private readonly string _key;
            private readonly ISettingApplyStrategy _applyStrategy;
            private readonly bool _applyImmediately;

            public string ProviderName => _pageId;

            public SingleBoolProvider(
                string pageId,
                string key,
                ISettingApplyStrategy applyStrategy,
                bool applyImmediately = true)
            {
                _pageId = pageId;
                _key = key;
                _applyStrategy = applyStrategy;
                _applyImmediately = applyImmediately;
            }

            public void Register(SettingsRegistry registry)
            {
                registry.RegisterPage(new SettingsPageDefinition(_pageId, _pageId, string.Empty));
                registry.RegisterSetting(new BoolSettingDefinition(
                    _key,
                    _pageId,
                    _key,
                    string.Empty,
                    true,
                    applyImmediately: _applyImmediately,
                    applyStrategy: _applyStrategy));
            }
        }

        private sealed class FloatProvider : ISettingsPageProvider
        {
            public string ProviderName => "Float";

            public void Register(SettingsRegistry registry)
            {
                registry.RegisterPage(new SettingsPageDefinition("page.float", "Float", string.Empty));
                registry.RegisterSetting(new FloatSettingDefinition(
                    "setting.volume",
                    "page.float",
                    "Volume",
                    string.Empty,
                    0.5f,
                    0f,
                    1f,
                    step: 0.1f,
                    applyImmediately: false));
            }
        }

        private sealed class CountingApplyStrategy : ISettingApplyStrategy
        {
            public string StrategyName { get; }
            public int ApplyCount { get; private set; }

            public CountingApplyStrategy(string strategyName)
            {
                StrategyName = strategyName;
            }

            public bool TryApply(SettingDefinition definition, object value, out string error)
            {
                ApplyCount++;
                error = null;
                return true;
            }
        }

        private sealed class MemorySettingsStorage : ISettingsStorage
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public int FlushCount { get; private set; }

            public bool TryLoad(string key, out string rawValue) => Values.TryGetValue(key, out rawValue);

            public void Save(string key, string rawValue)
            {
                Values[key] = rawValue;
            }

            public void Delete(string key)
            {
                Values.Remove(key);
            }

            public void Flush()
            {
                FlushCount++;
            }
        }
    }
}
