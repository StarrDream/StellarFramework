using UnityEngine;
using StellarFramework.Event;

namespace StellarFramework.Examples
{
    // 1. 定义枚举事件 (适用于轻量级状态通知)
    public enum ExampleGameEvent
    {
        LevelUp,
        GameOver
    }

    // 2. 定义结构体事件 (适用于携带复杂参数，0GC 且类型安全)
    public struct PlayerHitEvent : ITypeEvent
    {
        public int Damage;
        public Vector3 HitPoint;
    }

    /// <summary>
    /// EventKit 综合使用示例
    /// </summary>
    public class Example_EventKit : MonoBehaviour
    {
        private void Start()
        {
            // 注册枚举事件 (带一个 int 参数)
            GlobalEnumEvent.Register<ExampleGameEvent, int>(ExampleGameEvent.LevelUp, OnLevelUp)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 注册结构体事件
            GlobalTypeEvent.Register<PlayerHitEvent>(OnPlayerHit)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                // 广播枚举事件
                GlobalEnumEvent.Broadcast(ExampleGameEvent.LevelUp, 99);
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                // 广播结构体事件
                GlobalTypeEvent.Broadcast(new PlayerHitEvent { Damage = 50, HitPoint = Vector3.zero });
            }
        }

        private void OnLevelUp(int level)
        {
            LogKit.Log($"[Example_EventKit] 收到升级事件，当前等级: {level}");
        }

        private void OnPlayerHit(PlayerHitEvent evt)
        {
            LogKit.Log($"[Example_EventKit] 收到受击事件，伤害: {evt.Damage}, 位置: {evt.HitPoint}");
        }
    }
}