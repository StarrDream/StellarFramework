using StellarFramework;
using StellarFramework.Bindable;

namespace StellarFramework.Demo
{
    public interface ICoinModelReadOnly : IReadOnlyModel
    {
        IReadOnlyBindableProperty<int> CoinCount { get; }
        IReadOnlyBindableProperty<int> RoundNumber { get; }
    }

    /// <summary>
    /// 金币数据模型
    /// 职责：仅负责存储运行时数据，严禁在此处编写业务逻辑或引用 View 层。
    /// </summary>
    public class CoinModel : AbstractModel, ICoinModelReadOnly
    {
        // 当前轮已经获得的金币。
        public BindableProperty<int> CoinCount = new BindableProperty<int>(0);
        // 从 1 开始计数，完成一轮后递增。
        public BindableProperty<int> RoundNumber = new BindableProperty<int>(1);

        IReadOnlyBindableProperty<int> ICoinModelReadOnly.CoinCount => CoinCount;
        IReadOnlyBindableProperty<int> ICoinModelReadOnly.RoundNumber => RoundNumber;

        public override void Init()
        {
            base.Init();
            CoinCount.Value = 0;
            RoundNumber.Value = 1;
        }
    }
}
