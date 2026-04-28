using StellarFramework;

namespace StellarFramework.Demo
{
    /// <summary>
    /// 金币业务逻辑服务
    /// 职责：处理具体的业务规则（如合法性校验、数值计算），并修改 Model。
    /// </summary>
    public class CoinService : AbstractService
    {
        /// <summary>
        /// 执行增加金币的业务逻辑
        /// </summary>
        public void AddCoin(int amount)
        {
            // 规范：前置拦截非法参数，拒绝 Try-Catch 掩盖错误
            if (amount <= 0)
            {
                LogKit.LogError($"[CoinService] 添加金币失败: 传入的数量必须大于0，当前传入值: {amount}");
                return;
            }

            // 获取数据模型并修改，底层会自动触发 BindableProperty 的通知分发
            var model = GetModel<CoinModel>();
            model.CoinCount.Value += amount;

            LogKit.Log($"[CoinService] 成功添加金币: {amount}，当前总数: {model.CoinCount.Value}");
        }
    }
}