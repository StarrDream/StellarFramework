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
