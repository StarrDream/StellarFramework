using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using StellarFramework.UI;

namespace StellarFramework.Examples
{
    // 1. 定义强类型面板数据 (拒绝 object 传参)
    public class ExamplePanelData : UIPanelDataBase
    {
        public string TitleMessage;
        public int RewardCount;
    }

    // 2. 定义面板类 (需配合名为 ExamplePanel.prefab 的预制体放置在 Resources/UIPanel/ 下)
    public class ExamplePanel : UIPanelBase
    {
        public Text TitleText;
        public Button ConfirmBtn;

        public override void OnInit()
        {
            // 规范：前置拦截，防止 UI 组件丢失导致后续逻辑崩溃
            if (ConfirmBtn == null || TitleText == null)
            {
                LogKit.LogError($"[ExamplePanel] 初始化失败: 缺失必要 UI 组件引用，当前状态: ConfirmBtn={ConfirmBtn}, TitleText={TitleText}");
                return;
            }

            // 绑定关闭自身的方法
            ConfirmBtn.onClick.AddListener(CloseSelf);
        }

        public override void OnOpen(UIPanelDataBase data)
        {
            // 规范：使用基类提供的强类型解析方法，自带类型校验与错误输出
            if (TryGetPanelData<ExamplePanelData>(data, out var panelData))
            {
                TitleText.text = $"{panelData.TitleMessage}\n奖励数量: {panelData.RewardCount}";
                LogKit.Log("[ExamplePanel] 面板已打开，数据解析成功");
            }
        }
    }

    /// <summary>
    /// UIKit 综合使用示例
    /// </summary>
    public class Example_UIKit : MonoBehaviour
    {
        private void Start()
        {
            // 启动异步初始化流程
            StartUIFlowAsync().Forget();
        }

        private async UniTaskVoid StartUIFlowAsync()
        {
            // 1. 初始化 UIKit (底层会自动加载 UIRoot 并构建层级栈)
            await UIKit.Instance.InitAsync();
            LogKit.Log("[Example_UIKit] UIKit 初始化完成");

            // 2. 准备强类型数据
            var data = new ExamplePanelData
            {
                TitleMessage = "恭喜通关！",
                RewardCount = 999
            };

            // 3. 异步打开面板 (底层会自动通过 ResKit 加载 Prefab 并实例化)
            // 注意：运行此代码前，请确保已通过 Tools Hub 生成了 UIRoot，并制作了 ExamplePanel.prefab
            var panel = await UIKit.OpenPanelAsync<ExamplePanel>(data);

            if (panel == null)
            {
                LogKit.LogWarning("[Example_UIKit] 面板打开失败，请检查 Resources/UIPanel/ 目录下是否存在 ExamplePanel.prefab");
            }
        }
    }
}
