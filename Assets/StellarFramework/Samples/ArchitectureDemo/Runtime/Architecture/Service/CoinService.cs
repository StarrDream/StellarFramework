using StellarFramework;

namespace StellarFramework.Demo
{
    /// <summary>
    /// 金币业务逻辑服务
    /// 职责：处理具体的业务规则（如合法性校验、数值计算），并修改 Model。
    /// </summary>
    public class CoinService : AbstractService
    {
        public const int MineReward = 10;
        public const int RoundTarget = 30;

        /// <summary>
        /// 推进一次当前轮次。
        /// 未达到目标时执行一次挖矿；达到目标后再次调用则完成本轮并开始下一轮。
        /// </summary>
        public void AdvanceCycle()
        {
            CoinModel model = GetModel<CoinModel>();
            if (model == null)
            {
                LogKit.LogError("[CoinService] 推进失败: CoinModel 未注册。");
                return;
            }

            if (model.CoinCount.Value >= RoundTarget)
            {
                int completedRound = model.RoundNumber.Value;
                model.CoinCount.Value = 0;
                model.RoundNumber.Value = completedRound + 1;

                LogKit.Log(
                    $"[CoinService] 第 {completedRound} 轮完成，开始第 {model.RoundNumber.Value} 轮。");
                return;
            }

            int nextCoin = model.CoinCount.Value + MineReward;
            model.CoinCount.Value = nextCoin > RoundTarget ? RoundTarget : nextCoin;

            LogKit.Log(
                $"[CoinService] 第 {model.RoundNumber.Value} 轮挖矿 +{MineReward}，进度 {model.CoinCount.Value}/{RoundTarget}。");
        }
    }
}