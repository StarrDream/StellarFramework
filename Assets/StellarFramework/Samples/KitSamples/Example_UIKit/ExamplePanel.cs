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
                    $"[ExamplePanel] Init failed: missing UI refs, ConfirmBtn={ConfirmBtn}, TitleText={TitleText}");
                return;
            }

            ConfirmBtn.onClick.AddListener(CloseSelf);
        }

        public override void OnOpen(UIPanelDataBase data)
        {
            if (TryGetPanelData<ExamplePanelData>(data, out var panelData))
            {
                TitleText.text = $"{panelData.TitleMessage}\nReward Count: {panelData.RewardCount}";
                LogKit.Log("[ExamplePanel] Opened with resolved panel data");
            }
        }

        public override void OnRefresh(UIPanelDataBase data)
        {
            OnOpen(data);
            LogKit.Log("[ExamplePanel] Refreshed");
        }

        public override void OnClose()
        {
            base.OnClose();
            LogKit.Log("[ExamplePanel] Closed");
        }

        public override void OnPause()
        {
            LogKit.Log("[ExamplePanel] Paused by UI stack");
        }

        public override void OnResume()
        {
            LogKit.Log("[ExamplePanel] Resumed by UI stack");
        }
    }
}
