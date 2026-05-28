using Cysharp.Threading.Tasks;
using StellarFramework.UI;
using UnityEngine;

namespace StellarFramework.Examples
{
    // Strongly typed panel data keeps UI calls explicit and refactor-friendly.
    public class ExamplePanelData : UIPanelDataBase
    {
        public string TitleMessage;
        public int RewardCount;
    }

    /// <summary>
    /// UIKit 综合使用示例。
    ///
    /// 场景: Scenes/UIKit_Playable.unity
    /// 操作: O 打开面板，P Push，Backspace Pop，C Close，S 执行 100 次压力测试，D 打印 Snapshot。
    /// 前置: 样例构建器会生成 Resources/UIPanel/UIRoot.prefab 与 ExamplePanel.prefab。
    /// 通过标准: 面板可打开/关闭，压力测试结束后 UIKitRuntimeSnapshot 中 Loading=0。
    /// </summary>
    public class Example_UIKit : MonoBehaviour
    {
        private int _openIndex;
        private bool _stressRunning;

        private void Start()
        {
            StartUIFlowAsync().Forget();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.O))
            {
                OpenPanelAsync("Open").Forget();
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                PushPanelAsync().Forget();
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                UIKit.Pop();
                UIKit.LogSnapshot();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                UIKit.Close<ExamplePanel>();
                UIKit.LogSnapshot();
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                RunStressAsync().Forget();
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
                UIKit.LogSnapshot();
            }
        }

        private async UniTaskVoid StartUIFlowAsync()
        {
            await UIKit.Instance.InitAsync();
            LogKit.Log("[Example_UIKit] UIKit initialized");
            UIKit.LogSnapshot();

            await OpenPanelAsync("Startup");
        }

        private async UniTask OpenPanelAsync(string reason)
        {
            ExamplePanel panel = await UIKit.OpenAsync<ExamplePanel>(CreatePanelData(reason));
            if (panel == null)
            {
                LogKit.LogWarning(
                    "[Example_UIKit] Failed to open ExamplePanel. Check Resources/UIPanel/UIRoot.prefab and ExamplePanel.prefab.");
                return;
            }

            UIKit.LogSnapshot();
        }

        private async UniTask PushPanelAsync()
        {
            ExamplePanel panel = await UIKit.PushAsync<ExamplePanel>(CreatePanelData("Push"));
            if (panel == null)
            {
                LogKit.LogWarning("[Example_UIKit] Failed to push ExamplePanel.");
                return;
            }

            UIKit.LogSnapshot();
        }

        private async UniTaskVoid RunStressAsync()
        {
            if (_stressRunning)
            {
                LogKit.LogWarning("[Example_UIKit] UIKit stress test is already running.");
                return;
            }

            _stressRunning = true;
            try
            {
                LogKit.Log("[Example_UIKit] UIKit stress test started: 100 Open/Close loops.");
                await UIKit.StressOpenCloseAsync<ExamplePanel>(100, CreatePanelData("Stress"), 5);
            }
            finally
            {
                _stressRunning = false;
            }
        }

        private ExamplePanelData CreatePanelData(string reason)
        {
            _openIndex++;
            return new ExamplePanelData
            {
                TitleMessage = $"UIKit {reason} #{_openIndex}",
                RewardCount = 900 + _openIndex
            };
        }
    }
}
