using UnityEngine.UI;
using StellarFramework.UI;

namespace StellarFramework.Examples
{
    public class ExamplePanel : UIPanelBase
    {
        public Text TitleText;
        public Button ConfirmBtn;

        public override void OnInit()
        {
            if (ConfirmBtn == null || TitleText == null)
            {
                LogKit.LogError(
                    $"[ExamplePanel] 初始化失败：缺少 UI 引用，ConfirmBtn={ConfirmBtn}, TitleText={TitleText}");
                return;
            }

            ConfirmBtn.onClick.AddListener(CloseSelf);
        }

        public override void OnOpen(UIPanelDataBase data)
        {
            if (TryGetPanelData<ExamplePanelData>(data, out var panelData))
            {
                TitleText.text = $"{panelData.TitleMessage}\n奖励数量: {panelData.RewardCount}";
                LogKit.Log("[ExamplePanel] 已使用解析后的面板数据打开");
            }
        }

        public override void OnRefresh(UIPanelDataBase data)
        {
            OnOpen(data);
            LogKit.Log("[ExamplePanel] 已刷新");
        }

        public override void OnClose()
        {
            base.OnClose();
            LogKit.Log("[ExamplePanel] 已关闭");
        }

        public override void OnPause()
        {
            LogKit.Log("[ExamplePanel] 已被 UI 栈暂停");
        }

        public override void OnResume()
        {
            LogKit.Log("[ExamplePanel] 已从 UI 栈恢复");
        }
    }
}
