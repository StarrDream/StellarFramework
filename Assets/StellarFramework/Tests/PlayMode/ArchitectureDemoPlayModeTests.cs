using System.Collections;
using NUnit.Framework;
using StellarFramework;
using StellarFramework.Demo;
using StellarFramework.Localization.UnityUGUI;
using StellarFramework.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StellarFramework.Tests.PlayMode
{
    public sealed class ArchitectureDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator ModelServiceViewAndLocalizationRunAsOneRealLoop()
        {
            if (DemoApp.Interface.State == ArchitectureState.Initialized)
                DemoApp.Interface.Dispose();

            DemoApp app = DemoApp.Interface;
            app.Init();

            LocalizationTableAsset zh = CreateTable(
                "zh-CN",
                "第 {round} 轮 | 金币: {count}/{target}",
                "挖矿 +10",
                "完成本轮",
                "连续挖矿至 30 金币，再点击一次完成本轮并开始下一轮。",
                "关闭");
            LocalizationTableAsset en = CreateTable(
                "en-US",
                "Round {round} | Coins: {count}/{target}",
                "Mine +10",
                "Complete Round",
                "Mine to 30 coins, then click once more to start the next round.",
                "Close");
            LocalizationCatalogAsset catalog =
                ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            catalog.Configure(new[] { zh, en }, "zh-CN");

            GameObject contextObject = new GameObject("LocalizationContext");
            contextObject.SetActive(false);
            LocalizationContext context =
                contextObject.AddComponent<LocalizationContext>();
            context.Configure(catalog);
            contextObject.SetActive(true);
            Assert.That(context.TryInitialize(out string initError), Is.True, initError);

            GameObject prefab = Resources.Load<GameObject>("UIPanel/Panel_Main");
            Assert.That(prefab, Is.Not.Null);
            GameObject panelObject = Object.Instantiate(prefab);
            Panel_Main panel = panelObject.GetComponent<Panel_Main>();
            Assert.That(panel, Is.Not.Null);

            panel.OnInit();
            panel.OnOpen(new MainPanelData { WelcomeMessage = "test" });
            yield return null;

            Assert.That(panel.CoinText.text, Is.EqualTo("第 1 轮 | 金币: 0/30"));
            Assert.That(
                panel.MineButton.GetComponentInChildren<Text>(true).text,
                Is.EqualTo("挖矿 +10"));

            Assert.That(context.SetLocale("en-US", out string localeError), Is.True, localeError);
            Assert.That(panel.CoinText.text, Is.EqualTo("Round 1 | Coins: 0/30"));
            Assert.That(
                panel.MineButton.GetComponentInChildren<Text>(true).text,
                Is.EqualTo("Mine +10"));
            Assert.That(
                panel.CloseButton.GetComponentInChildren<Text>(true).text,
                Is.EqualTo("Close"));

            panel.MineButton.onClick.Invoke();
            yield return null;

            ICoinModelReadOnly model =
                app.GetReadOnlyModel<ICoinModelReadOnly>();
            Assert.That(model, Is.Not.Null);
            Assert.That(model.CoinCount.Value, Is.EqualTo(10));
            Assert.That(model.RoundNumber.Value, Is.EqualTo(1));
            Assert.That(panel.CoinText.text, Is.EqualTo("Round 1 | Coins: 10/30"));

            panel.MineButton.onClick.Invoke();
            panel.MineButton.onClick.Invoke();
            yield return null;

            Assert.That(model.CoinCount.Value, Is.EqualTo(30));
            Assert.That(model.RoundNumber.Value, Is.EqualTo(1));
            Assert.That(panel.CoinText.text, Is.EqualTo("Round 1 | Coins: 30/30"));
            Assert.That(
                panel.MineButton.GetComponentInChildren<Text>(true).text,
                Is.EqualTo("Complete Round"));

            panel.MineButton.onClick.Invoke();
            yield return null;

            Assert.That(model.CoinCount.Value, Is.EqualTo(0));
            Assert.That(model.RoundNumber.Value, Is.EqualTo(2));
            Assert.That(panel.CoinText.text, Is.EqualTo("Round 2 | Coins: 0/30"));
            Assert.That(
                panel.MineButton.GetComponentInChildren<Text>(true).text,
                Is.EqualTo("Mine +10"));

            Object.Destroy(panelObject);
            Object.Destroy(contextObject);
            Object.Destroy(catalog);
            Object.Destroy(zh);
            Object.Destroy(en);
            app.Dispose();
        }

        [UnityTest]
        public IEnumerator MainPanelCanCloseReopenAndKeepModelState()
        {
            if (DemoApp.Interface.State == ArchitectureState.Initialized)
                DemoApp.Interface.Dispose();

            LocalizationTableAsset zh = CreateTable(
                "zh-CN",
                "第 {round} 轮 | 金币: {count}/{target}",
                "挖矿 +10",
                "完成本轮",
                "连续挖矿至 30 金币，再点击一次完成本轮并开始下一轮。",
                "关闭");
            LocalizationCatalogAsset catalog =
                ScriptableObject.CreateInstance<LocalizationCatalogAsset>();
            catalog.Configure(new[] { zh }, "zh-CN");

            GameObject contextObject = new GameObject("LocalizationContext");
            contextObject.SetActive(false);
            LocalizationContext context =
                contextObject.AddComponent<LocalizationContext>();
            context.Configure(catalog);
            contextObject.SetActive(true);
            Assert.That(context.TryInitialize(out string initError), Is.True, initError);

            GameObject launcher = new GameObject("OpenPanelButton");
            Button launcherButton = launcher.AddComponent<Button>();

            GameObject entryObject = new GameObject("DemoEntry");
            entryObject.SetActive(false);
            DemoEntry entry = entryObject.AddComponent<DemoEntry>();
            entry.ConfigurePanelLauncher(launcher);
            launcherButton.onClick.AddListener(entry.OpenMainPanel);
            entryObject.SetActive(true);

            yield return null;
            yield return null;

            Panel_Main panel = UIKit.GetPanel<Panel_Main>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(entry.IsPanelLauncherVisible, Is.False);

            panel.MineButton.onClick.Invoke();
            yield return null;

            ICoinModelReadOnly model =
                DemoApp.Interface.GetReadOnlyModel<ICoinModelReadOnly>();
            Assert.That(model.CoinCount.Value, Is.EqualTo(10));
            Assert.That(model.RoundNumber.Value, Is.EqualTo(1));

            panel.CloseButton.onClick.Invoke();
            yield return null;

            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(entry.IsPanelLauncherVisible, Is.True);
            Assert.That(model.CoinCount.Value, Is.EqualTo(10));

            launcherButton.onClick.Invoke();
            yield return null;

            Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(entry.IsPanelLauncherVisible, Is.False);
            Assert.That(model.CoinCount.Value, Is.EqualTo(10));
            Assert.That(panel.CoinText.text, Is.EqualTo("第 1 轮 | 金币: 10/30"));

            panel.MineButton.onClick.Invoke();
            yield return null;
            Assert.That(model.CoinCount.Value, Is.EqualTo(20));

            UIKit.DestroyAllPanels();
            Object.Destroy(entryObject);
            Object.Destroy(launcher);
            Object.Destroy(contextObject);
            Object.Destroy(catalog);
            Object.Destroy(zh);
            yield return null;
        }

        private static LocalizationTableAsset CreateTable(
            string locale,
            string coin,
            string mine,
            string completeRound,
            string hint,
            string close)
        {
            LocalizationTableAsset table =
                ScriptableObject.CreateInstance<LocalizationTableAsset>();
            table.Configure(
                locale,
                new[]
                {
                    new LocalizationAuthoringEntry(
                        "architecture.panel.coin", coin),
                    new LocalizationAuthoringEntry(
                        "architecture.panel.mine", mine),
                    new LocalizationAuthoringEntry(
                        "architecture.panel.complete_round", completeRound),
                    new LocalizationAuthoringEntry(
                        "architecture.panel.hint", hint),
                    new LocalizationAuthoringEntry(
                        "architecture.panel.close", close)
                });
            return table;
        }
    }
}
