using UnityEngine;

namespace StellarFramework.Examples
{
    // 1. 全局单例 (Global)
    // 特性：
    // 1. 任意地方调用 Instance 都会自动创建
    // 2. 跨场景不销毁
    [Singleton(lifeCycle: SingletonLifeCycle.Global)]
    public class GlobalNetworkManager : MonoSingleton<GlobalNetworkManager>
    {
        public override void OnSingletonInit()
        {
            base.OnSingletonInit();
            LogKit.Log("[GlobalNetworkManager] 全局网络管理器已初始化");
        }

        public void Connect()
        {
        }
    }

    // 2. 场景单例 (Scene)
    // 特性：
    // 1. 必须手动挂载在场景中
    // 2. 切场景自动销毁
    // 3. 若未挂载直接访问会精准报错，绝不执行 Find 导致卡顿
    [Singleton(lifeCycle: SingletonLifeCycle.Scene)]
    public class LevelDirector : MonoSingleton<LevelDirector>
    {
        public override void OnSingletonInit()
        {
            base.OnSingletonInit();
            LogKit.Log("[LevelDirector] 关卡导演已初始化");
        }

        public void StartLevel()
        {
        }
    }

    // 3. 纯 C# 单例
    // 特性：
    // 1. 不继承 MonoBehaviour
    // 2. 运行时不再依赖反射实例化
    // 3. 必须由静态注册表注入创建器
    [Singleton]
    public class GameDataCalculator : Singleton<GameDataCalculator>
    {
        public int CalculateDamage(int atk, int def)
        {
            return Mathf.Max(1, atk - def);
        }
    }

    /// <summary>
    /// SingletonKit 综合调用示例
    /// </summary>
    public class Example_SingletonKit : MonoBehaviour
    {
        private void Start()
        {
            // 自动创建并挂载到 [SingletonContainer] 下
            GlobalNetworkManager.Instance.Connect();

            // 纯 C# 单例调用
            GameDataCalculator calculator = GameDataCalculator.Instance;
            if (calculator == null)
            {
                LogKit.LogError("[Example_SingletonKit] 获取纯 C# 单例失败: GameDataCalculator.Instance 为空");
                return;
            }

            int dmg = calculator.CalculateDamage(100, 50);
            LogKit.Log($"[Example_SingletonKit] 计算伤害结果: {dmg}");

            // 场景单例必须预先挂在当前场景中
            // 若项目里未挂载，请不要直接访问
            if (LevelDirector.Instance != null)
            {
                LevelDirector.Instance.StartLevel();
            }
        }
    }
}