using UnityEngine;
using StellarFramework;

namespace StellarFramework.Examples
{
    // 1. 全局单例 (Global)
    // 特性：任意地方调用 Instance 都会自动创建，跨场景不销毁
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
            /* 连接逻辑 */
        }
    }

    // 2. 场景单例 (Scene)
    // 特性：必须手动挂载在场景中，切场景自动销毁。若未挂载直接访问会精准报错，绝不执行 Find 导致卡顿
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
            /* 关卡逻辑 */
        }
    }

    // 3. 纯 C# 单例
    // 特性：不继承 MonoBehaviour，纯数据或算法类，0GC 自动创建
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
            int dmg = GameDataCalculator.Instance.CalculateDamage(100, 50);
            LogKit.Log($"[Example_SingletonKit] 计算伤害结果: {dmg}");

            // 注意：LevelDirector 必须事先挂载在当前场景中，否则这里会触发 LogError 阻断
            // LevelDirector.Instance.StartLevel(); 
        }
    }
}