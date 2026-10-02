using System;
using StellarFramework;
using StellarFramework.Localization;

namespace HotUpdate
{
    public sealed class TankArenaLocalizationService : AbstractService
    {
        private static readonly string[] Keys =
        {
            "tank.title",
            "tank.subtitle",
            "tank.score",
            "tank.wave",
            "tank.hull",
            "tank.objective",
            "tank.move",
            "tank.mode_auto",
            "tank.mode_manual",
            "tank.fire_hint_auto",
            "tank.fire_hint_manual",
            "tank.game_over",
            "tank.result",
            "tank.restart",
            "tank.resume",
            "tank.paused",
            "tank.pickup",
            "tank.phase_ready",
            "tank.package",
            "tank.systems",
            "tank.systems_title",
            "tank.systems_subtitle",
            "tank.systems_close",
            "tank.systems_state",
            "tank.systems_config",
            "tank.systems_best",
            "tank.systems_recent",
            "tank.systems_pool",
            "tank.systems_phase",
            "tank.systems_display",
            "tank.systems_feedback_on",
            "tank.systems_feedback_off",
            "tank.systems_event_ready",
            "tank.systems_event_elimination",
            "tank.systems_event_wave",
            "tank.systems_event_repair",
            "tank.systems_event_damage",
            "tank.systems_config_default",
            "tank.systems_config_override"
        };

        private static readonly string[] English =
        {
            "IRON VANGUARD",
            "SECTOR 07  /  NEBULA REACH",
            "SCORE",
            "WAVE",
            "HULL",
            "HOLD THE LINE  ·  ELIMINATE HOSTILES",
            "MOVE HULL",
            "AUTO",
            "MANUAL",
            "AUTO AIM  ·  TAP FOR MANUAL",
            "RIGHT PAD AIM  ·  HOLD TO FIRE",
            "TANK DISABLED",
            "FINAL SCORE  {score}   ·   ELIMINATIONS  {kills}",
            "DEPLOY AGAIN",
            "RESUME",
            "PAUSED",
            "HULL RESTORED  +25",
            "SECTOR STABLE",
            "PACKAGE",
            "SYSTEMS",
            "FRAMEWORK SYSTEMS",
            "Live Kit connections from this match. Values update while you play.",
            "CLOSE",
            "State",
            "Config",
            "Best",
            "Recent event",
            "Reused / rented",
            "Match",
            "Safe area / language / package",
            "SCREEN FEEDBACK  ·  ON",
            "SCREEN FEEDBACK  ·  OFF",
            "Waiting for a match event",
            "Hostile eliminated",
            "New wave",
            "Hull repaired",
            "Hull damage",
            "StreamingAssets defaults",
            "Local override"
        };

        private static readonly string[] Chinese =
        {
            "星域突围",
            "第 07 区  /  星云边界",
            "得分",
            "波次",
            "装甲",
            "守住阵线  ·  击退来袭敌军",
            "移动车体",
            "自动",
            "手动",
            "自动瞄准  ·  点击切换手动",
            "右侧摇杆瞄准  ·  按住开火",
            "坦克已损毁",
            "最终得分  {score}   ·   击毁  {kills}",
            "再次出击",
            "继续作战",
            "暂停",
            "装甲恢复  +25",
            "区域稳定",
            "热更包",
            "系统",
            "框架 Kits 实时运行面板",
            "查看这局中正在协作的 Kits，状态随游戏实时变化。",
            "关闭",
            "状态",
            "配置",
            "最高分",
            "最近事件",
            "已回收 / 已分配",
            "战局",
            "安全区 / 语言 / 热更包",
            "屏幕反馈  ·  开",
            "屏幕反馈  ·  关",
            "等待战局事件",
            "击毁敌方坦克",
            "进入新波次",
            "装甲已修复",
            "装甲受损",
            "内置默认配置",
            "本机覆盖配置"
        };

        private static readonly LocaleId EnglishLocale = LocaleId.From("en-US");
        private static readonly LocaleId ChineseLocale = LocaleId.From("zh-CN");

        public LocalizationService Text { get; private set; }

        public override void Init()
        {
            base.Init();
            LocalizationTable english = CreateTable(EnglishLocale, English);
            LocalizationTable chinese = CreateTable(ChineseLocale, Chinese);
            var catalog = new LocalizationCatalog(new[] { english, chinese });
            Text = new LocalizationService(
                catalog,
                EnglishLocale,
                new LocalizationFallbackPolicy(new[] { EnglishLocale }));
        }

        public string Get(string key)
        {
            return Text.GetRequired(LocalizationKey.From(key));
        }

        public bool ToggleLanguage()
        {
            LocaleId next = Text.CurrentLocale == EnglishLocale ? ChineseLocale : EnglishLocale;
            return Text.SetLocale(next, out string error) ||
                   LogLocaleFailure(error);
        }

        private static LocalizationTable CreateTable(LocaleId locale, string[] values)
        {
            var entries = new LocalizationEntry[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            {
                entries[i] = new LocalizationEntry(LocalizationKey.From(Keys[i]), values[i]);
            }
            return new LocalizationTable(locale, entries);
        }

        private static bool LogLocaleFailure(string error)
        {
            LogKit.LogError("[TankArena] Locale switch failed: " + (error ?? "unknown error"));
            return false;
        }
    }
}
