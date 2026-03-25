using StellarFramework;
using StellarFramework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace StellarFramework.Demo
{
    /// <summary>
    /// 强类型面板数据
    /// 职责：约束外部打开面板时必须传入的参数结构，杜绝 object 装箱。
    /// </summary>
    public class MainPanelData : UIPanelDataBase
    {
        public string WelcomeMessage;
    }

    /// <summary>
    /// 主界面表现层
    /// 职责：实现 IView 接入架构，负责 UI 交互、动画表现，并通过 Service 驱动业务。
    /// </summary>
    public class Panel_Main : UIPanelBase, IView
    {
        // 规范：显式指定当前 View 归属的架构，以便底层进行依赖注入
        public IArchitecture Architecture => DemoApp.Interface;

        [Header("UI 引用")] public Text CoinText;
        public Button MineButton;
        public Button CloseButton;

        public override void OnInit()
        {
            // 规范：前置拦截组件丢失，防止后续逻辑触发空指针异常
            if (CoinText == null || MineButton == null || CloseButton == null)
            {
                LogKit.LogError(
                    $"[MainPanel] 初始化失败: 缺失必要 UI 组件引用，当前状态: CoinText={CoinText}, MineButton={MineButton}, CloseButton={CloseButton}");
                return;
            }

            MineButton.onClick.AddListener(OnClickMine);
            CloseButton.onClick.AddListener(CloseSelf);

            // 手动触发绑定逻辑
            OnBind();
        }

        public void OnBind()
        {
            var model = this.GetModel<CoinModel>();
            // 规范：注册数据监听后，必须绑定当前 GameObject 的生命周期，防止 UI 销毁后产生野指针泄漏
            model.CoinCount.RegisterWithInitValue(OnCoinChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        public void OnUnbind()
        {
            // UnRegisterWhenGameObjectDestroyed 已接管销毁逻辑，此处无需手动反注册
        }

        public override void OnOpen(UIPanelDataBase data)
        {
            // 规范：强类型解析入参
            if (TryGetPanelData<MainPanelData>(data, out var panelData))
            {
                LogKit.Log($"[MainPanel] 接收到外部数据: {panelData.WelcomeMessage}");
            }

            // 规范：使用 ActionKit 替代 DOTween，执行 0GC 的入场动画
            RectTransform.localScale = Vector3.zero;
            MonoKit.Sequence(gameObject)
                .ScaleTo(RectTransform, Vector3.one, 0.4f, Ease.OutBack)
                .Start();
        }

        private void OnClickMine()
        {
            // 表现层逻辑：播放按钮点击反馈动画
            MonoKit.Sequence(gameObject)
                .ScaleTo(MineButton.transform, Vector3.one * 1.1f, 0.1f)
                .ScaleTo(MineButton.transform, Vector3.one, 0.1f)
                .Start();

            // 业务层逻辑：View 严禁直接修改 Model，必须通过 Service 派发
            this.GetService<CoinService>().AddCoin(10);
        }

        private void OnCoinChanged(int currentCoin)
        {
            // 数据驱动表现：当 Model 发生变化时，被动刷新 UI
            CoinText.text = $"当前金币: {currentCoin}";
        }
    }
}