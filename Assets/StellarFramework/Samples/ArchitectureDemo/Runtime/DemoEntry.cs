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
        [Header("Scene UI")]
        [SerializeField] private GameObject panelLauncherRoot;

        private bool _isStarted;

        public bool IsPanelLauncherVisible =>
            panelLauncherRoot != null && panelLauncherRoot.activeSelf;

        private void OnEnable()
        {
            UIPanelBase.OnPanelClosedGlobal += HandlePanelClosed;
        }

        private void OnDisable()
        {
            UIPanelBase.OnPanelClosedGlobal -= HandlePanelClosed;
        }

        private async void Start()
        {
            if (_isStarted)
            {
                LogKit.LogWarning($"[DemoEntry] 重复启动已忽略, TriggerObject={gameObject.name}");
                return;
            }

            _isStarted = true;
            LogKit.Log("[DemoEntry] 开始启动业务流转...");

            if (panelLauncherRoot == null)
            {
                LogKit.LogError("[DemoEntry] 启动失败: panelLauncherRoot 未绑定。");
                return;
            }

            panelLauncherRoot.SetActive(false);

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

            Panel_Main panel = await UIKit.OpenPanelAsync<Panel_Main>(CreatePanelData());
            if (panel == null)
            {
                panelLauncherRoot.SetActive(true);
                LogKit.LogError("[DemoEntry] 首屏打开失败，已保留重新打开入口。");
                return;
            }

            LogKit.Log("[DemoEntry] 首屏加载完毕，控制权移交玩家。");
        }

        /// <summary>
        /// 场景常驻按钮调用入口。Panel 关闭后重新通过 UIKit 打开；
        /// Architecture / Model 生命周期不变，因此业务状态不会因为 View 关闭而重置。
        /// </summary>
        public void OpenMainPanel()
        {
            if (panelLauncherRoot == null)
            {
                LogKit.LogError("[DemoEntry] 打开面板失败: panelLauncherRoot 未绑定。");
                return;
            }

            if (DemoApp.Interface == null ||
                DemoApp.Interface.State != ArchitectureState.Initialized)
            {
                LogKit.LogError(
                    $"[DemoEntry] 打开面板失败: DemoApp 尚未初始化, State={DemoApp.Interface?.State.ToString() ?? "null"}");
                panelLauncherRoot.SetActive(true);
                return;
            }

            Panel_Main panel = UIKit.OpenPanel<Panel_Main>(CreatePanelData());
            if (panel == null)
            {
                LogKit.LogError("[DemoEntry] 打开面板失败: UIKit 未返回 Panel_Main。");
                panelLauncherRoot.SetActive(true);
                return;
            }

            panelLauncherRoot.SetActive(false);
        }

        /// <summary>
        /// 供测试或代码式场景装配使用；正式 Demo 场景通过 Inspector 序列化绑定。
        /// </summary>
        public void ConfigurePanelLauncher(GameObject launcherRoot)
        {
            panelLauncherRoot = launcherRoot;
        }

        private static MainPanelData CreatePanelData()
        {
            return new MainPanelData
            {
                WelcomeMessage = "欢迎体验 StellarFramework MSV 全闭环流转！"
            };
        }

        private void HandlePanelClosed(UIPanelBase panel)
        {
            if (panel is Panel_Main && panelLauncherRoot != null)
            {
                panelLauncherRoot.SetActive(true);
            }
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