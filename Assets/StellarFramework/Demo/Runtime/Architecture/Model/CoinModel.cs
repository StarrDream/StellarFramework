using StellarFramework;
using StellarFramework.Bindable;

namespace StellarFramework.Demo
{
    /// <summary>
    /// 金币数据模型
    /// 职责：仅负责存储运行时数据，严禁在此处编写业务逻辑或引用 View 层。
    /// </summary>
    public class CoinModel : AbstractModel
    {
        // 规范：使用 BindableProperty 实现 0GC 的数据变更通知
        public BindableProperty<int> CoinCount = new BindableProperty<int>(0);

        public override void Init()
        {
            base.Init();
            // 可以在此处进行本地存档的读取与反序列化初始化
            CoinCount.Value = 0;
        }
    }
}