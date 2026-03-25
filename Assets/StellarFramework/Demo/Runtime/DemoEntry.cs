using UnityEngine;
using Cysharp.Threading.Tasks;
using StellarFramework.UI;

namespace StellarFramework.Demo
{
    /// <summary>
    /// 业务生命周期入口
    /// 职责：驱动架构初始化与首屏 UI 的异步加载。
    /// </summary>
    public class DemoEntry : MonoBehaviour
    {
        private async void Start()
        {
            LogKit.Log("[DemoEntry] 开始启动业务流转...");

            // 1. 初始化业务架构 (实例化 Model 与 Service)
            DemoApp.Interface.Init();

            // 2. 异步初始化 UI 系统 (底层自动加载 UIRoot 并构建层级栈)
            await UIKit.Instance.InitAsync();

            // 3. 构造强类型参数并异步打开主界面
            var initData = new MainPanelData
            {
                WelcomeMessage = "欢迎体验 StellarFramework MSV 全闭环流转！"
            };

            await UIKit.OpenPanelAsync<Panel_Main>(initData);

            LogKit.Log("[DemoEntry] 首屏加载完毕，控制权移交玩家。");
        }

        private void OnDestroy()
        {
            // 规范：场景卸载或业务域结束时，必须销毁架构以释放内部缓存
            DemoApp.Interface.Dispose();
        }
    }
}