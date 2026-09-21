using System;
using StellarFramework;
using StellarFramework.Localization;
using StellarFramework.Localization.UnityUGUI;
using StellarFramework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Demo
{
    /// <summary>
    /// 强类型面板数据。
    /// </summary>
    public class MainPanelData : UIPanelDataBase
    {
        public string WelcomeMessage;
    }

    /// <summary>
    /// 主界面表现层。View 只负责表现与交互，业务修改仍通过 CoinService -> CoinModel。
    /// </summary>
    public class Panel_Main : UIPanelBase, IView
    {
        private static readonly LocalizationKey CoinKey =
            LocalizationKey.From("architecture.panel.coin");
        private static readonly LocalizationKey MineKey =
            LocalizationKey.From("architecture.panel.mine");
        private static readonly LocalizationKey CompleteRoundKey =
            LocalizationKey.From("architecture.panel.complete_round");
        private static readonly LocalizationKey HintKey =
            LocalizationKey.From("architecture.panel.hint");
        private static readonly LocalizationKey CloseKey =
            LocalizationKey.From("architecture.panel.close");

        public IReadOnlyArchitecture Architecture => DemoApp.Interface;

        [Header("UI 引用")]
        public Text CoinText;
        public Text HintText;
        public Button MineButton;
        public Button CloseButton;

        private LocalizationContext _localizationContext;
        private Text _mineLabel;
        private Text _closeLabel;
        private int _lastCoin;
        private int _lastRound = 1;
        private readonly LocalizationFormatArgument[] _coinArguments =
            new LocalizationFormatArgument[3];

        public override void OnInit()
        {
            if (CoinText == null || HintText == null || MineButton == null || CloseButton == null)
            {
                LogKit.LogError(
                    $"[MainPanel] 初始化失败: 缺失必要 UI 组件引用，当前状态: CoinText={CoinText}, HintText={HintText}, MineButton={MineButton}, CloseButton={CloseButton}");
                return;
            }

            MineButton.onClick.AddListener(OnClickMine);
            CloseButton.onClick.AddListener(CloseSelf);
            BindLocalization();
            OnBind();
        }

        public void OnBind()
        {
            ICoinModelReadOnly model = this.GetReadOnlyModel<ICoinModelReadOnly>();
            if (model == null)
            {
                LogKit.LogError("[MainPanel] OnBind 失败: 只读模型契约 ICoinModelReadOnly 未注册");
                return;
            }

            model.CoinCount.RegisterWithInitValue(OnCoinChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            model.RoundNumber.RegisterWithInitValue(OnRoundChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        public void OnUnbind()
        {
            // BindableKit lifetime binding is owned by the GameObject.
        }

        public override void OnOpen(UIPanelDataBase data)
        {
            if (TryGetPanelData<MainPanelData>(data, out MainPanelData panelData))
                LogKit.Log($"[MainPanel] 接收到外部数据: {panelData.WelcomeMessage}");

            RectTransform.localScale = Vector3.zero;
            ActionKit.Sequence(gameObject)
                .ScaleTo(RectTransform, Vector3.one, 0.4f, Ease.OutBack)
                .Start();
        }

        private void OnClickMine()
        {
            ActionKit.Sequence(gameObject)
                .ScaleTo(MineButton.transform, Vector3.one * 1.1f, 0.1f)
                .ScaleTo(MineButton.transform, Vector3.one, 0.1f)
                .Start();

            this.GetService<CoinService>().AdvanceCycle();
        }

        private void OnCoinChanged(int currentCoin)
        {
            _lastCoin = currentCoin;
            RefreshCoinText();
        }

        private void OnRoundChanged(int roundNumber)
        {
            _lastRound = roundNumber;
            RefreshCoinText();
        }

        private void BindLocalization()
        {
            LocalizationContext[] contexts =
                UnityEngine.Object.FindObjectsOfType<LocalizationContext>(true);
            if (contexts.Length == 0)
            {
                LogKit.LogError(
                    "[MainPanel] LocalizationContext 未找到。ArchitectureDemo 场景必须提供本地化上下文。");
                return;
            }

            _localizationContext = contexts[0];
            if (!_localizationContext.TryInitialize(out string error))
            {
                LogKit.LogError("[MainPanel] LocalizationContext 初始化失败: " + error);
                return;
            }

            _mineLabel = MineButton.GetComponentInChildren<Text>(true);
            _closeLabel = CloseButton.GetComponentInChildren<Text>(true);
            if (_mineLabel == null || _closeLabel == null)
            {
                LogKit.LogError(
                    $"[MainPanel] 本地化标签缺失: MineLabel={_mineLabel}, CloseLabel={_closeLabel}");
                return;
            }

            _localizationContext.LocaleChanged -= HandleLocaleChanged;
            _localizationContext.LocaleChanged += HandleLocaleChanged;
            RefreshLocalizedText();
        }

        private void HandleLocaleChanged(object sender, LocalizationChangedEventArgs args)
        {
            RefreshLocalizedText();
        }

        private void RefreshLocalizedText()
        {
            if (_localizationContext == null || !_localizationContext.IsInitialized)
                return;

            LocalizationService service = _localizationContext.Service;
            if (_closeLabel != null) _closeLabel.text = service.GetRequired(CloseKey);
            if (HintText != null) HintText.text = service.GetRequired(HintKey);
            RefreshCoinText();
        }

        private void RefreshCoinText()
        {
            if (CoinText == null) return;
            if (_localizationContext == null || !_localizationContext.IsInitialized)
            {
                CoinText.text = $"第 {_lastRound} 轮 | 金币: {_lastCoin}/{CoinService.RoundTarget}";
                if (_mineLabel != null)
                    _mineLabel.text = _lastCoin >= CoinService.RoundTarget ? "完成本轮" : "挖矿 +10";
                return;
            }

            _coinArguments[0] =
                new LocalizationFormatArgument("count", _lastCoin.ToString());
            _coinArguments[1] =
                new LocalizationFormatArgument("round", _lastRound.ToString());
            _coinArguments[2] =
                new LocalizationFormatArgument("target", CoinService.RoundTarget.ToString());
            if (!_localizationContext.Service.TryFormat(
                    CoinKey,
                    _coinArguments,
                    out string localized,
                    out string error))
            {
                LogKit.LogError("[MainPanel] 金币文本本地化失败: " + error);
                CoinText.text = _lastCoin.ToString();
                return;
            }

            CoinText.text = localized;
            if (_mineLabel != null)
            {
                _mineLabel.text = _lastCoin >= CoinService.RoundTarget
                    ? _localizationContext.Service.GetRequired(CompleteRoundKey)
                    : _localizationContext.Service.GetRequired(MineKey);
            }
        }

        private void OnDestroy()
        {
            if (_localizationContext != null)
                _localizationContext.LocaleChanged -= HandleLocaleChanged;
        }
    }
}
