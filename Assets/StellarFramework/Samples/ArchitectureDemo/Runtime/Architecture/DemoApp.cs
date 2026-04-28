using StellarFramework;

namespace StellarFramework.Demo
{
    /// <summary>
    /// Demo 业务架构入口
    /// 职责：作为当前业务域的 IOC 容器，负责注册并管理该域下的所有 Model 与 Service。
    /// </summary>
    public class DemoApp : Architecture<DemoApp>
    {
        protected override void InitModules()
        {
            // 规范：在此处统一注册模块，明确依赖关系
            RegisterModel(new CoinModel());
            RegisterService(new CoinService());
        }
    }
}