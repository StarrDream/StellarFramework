using StellarFramework.UI;
using UnityEngine;

namespace StellarFramework.Demo
{
    /// <summary>
    /// 业务生命周期入口
    /// 职责：驱动架构初始化与首屏 UI 的异步加载。
    /// </summary>
    public class DemoEntry : MonoBehaviour
    {
        private bool _isStarted;

        private async void Start()
        {
            if (_isStarted)
            {
                LogKit.LogWarning($"[DemoEntry] 重复启动已忽略, TriggerObject={gameObject.name}");
                return;
            }

            _isStarted = true;
            LogKit.Log("[DemoEntry] 开始启动业务流转...");

            if (DemoApp.Interface == null)
            {
                LogKit.LogError($"[DemoEntry] 启动失败: DemoApp.Interface 为空, TriggerObject={gameObject.name}");
                return;
            }

            if (DemoApp.Interface.State == ArchitectureState.Uninitialized)
            {
                DemoApp.Interface.Init();
            }
            else if (DemoApp.Interface.State != ArchitectureState.Initialized)
            {
                LogKit.LogError(
                    $"[DemoEntry] 启动失败: DemoApp 状态非法, TriggerObject={gameObject.name}, State={DemoApp.Interface.State}");
                return;
            }

            await UIKit.Instance.InitAsync();

            if (this == null || gameObject == null)
            {
                LogKit.LogWarning("[DemoEntry] 启动链路中断: 宿主已销毁");
                return;
            }

            MainPanelData initData = new MainPanelData
            {
                WelcomeMessage = "欢迎体验 StellarFramework MSV 全闭环流转！"
            };

            await UIKit.OpenPanelAsync<Panel_Main>(initData);
            LogKit.Log("[DemoEntry] 首屏加载完毕，控制权移交玩家。");
        }

        private void OnDestroy()
        {
            if (!_isStarted)
            {
                return;
            }

            if (DemoApp.Interface != null && DemoApp.Interface.State == ArchitectureState.Initialized)
            {
                DemoApp.Interface.Dispose();
            }

            _isStarted = false;
        }
    }
}