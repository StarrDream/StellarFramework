using System.Collections;
using NUnit.Framework;
using StellarFramework.Event;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.PlayMode
{
    public enum PlayModeTestEvent
    {
        E1,
        E2
    }

    /// <summary>
    /// EventKit Token 池化 use-after-free 回归测试。
    /// 场景：Token 绑定生命周期触发器 → 手动注销（回池）→ 复用 → 旧宿主销毁。
    /// 修复前：旧宿主销毁会误注销复用后的新回调（幽灵 bug）。
    /// </summary>
    public class EventKitPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            GlobalEnumEvent.ClearAll<PlayModeTestEvent>();
        }

        [TearDown]
        public void TearDown()
        {
            GlobalEnumEvent.ClearAll<PlayModeTestEvent>();
        }

        [UnityTest]
        public IEnumerator TokenPoolReuseDoesNotCancelNewCallback()
        {
            int callbackBCount = 0;

            GameObject host = new GameObject("EventHost");

            // 1. 注册回调 A 并绑定到 host 的生命周期。
            IUnRegister tokenA = GlobalEnumEvent.Register(PlayModeTestEvent.E1, () => { });
            tokenA.UnRegisterWhenGameObjectDestroyed(host);

            // 2. 手动注销：tokenA 回收到 TokenPool，但 EventUnregisterTrigger 仍持有引用。
            tokenA.UnRegister();

            // 3. 复用池中的 token 注册回调 B。
            GlobalEnumEvent.Register(PlayModeTestEvent.E1, () => callbackBCount++);

            // 4. 销毁旧宿主：修复前会误注销回调 B。
            Object.Destroy(host);
            yield return null;

            // 5. 广播：回调 B 必须仍能收到事件。
            GlobalEnumEvent.Broadcast(PlayModeTestEvent.E1);

            Assert.AreEqual(1, callbackBCount,
                "修复前：复用后的 Token 被旧宿主销毁时误注销，新回调收不到事件（use-after-free）。");
        }

        [UnityTest]
        public IEnumerator ManualUnRegisterThenReuseIsSafe()
        {
            int callbackBCount = 0;
            GameObject host = new GameObject("EventHost2");

            IUnRegister tokenA = GlobalEnumEvent.Register(PlayModeTestEvent.E2, () => { });
            tokenA.UnRegisterWhenGameObjectDestroyed(host);
            tokenA.UnRegister();

            IUnRegister tokenB = GlobalEnumEvent.Register(PlayModeTestEvent.E2, () => callbackBCount++);

            // 手动注销 tokenB，再销毁宿主：不应产生任何异常或误注销。
            tokenB.UnRegister();
            Object.Destroy(host);
            yield return null;

            GlobalEnumEvent.Broadcast(PlayModeTestEvent.E2);
            Assert.AreEqual(0, callbackBCount, "已注销的回调不应再被触发。");
        }

        [UnityTest]
        public IEnumerator DestroyingHostWithMultipleBindingsUnregistersAllWithoutMutationException()
        {
            int callbackCount = 0;
            GameObject host = new GameObject("EventHost_Destroy_Multiple");

            GlobalEnumEvent.Register(PlayModeTestEvent.E1, () => callbackCount++)
                .UnRegisterWhenGameObjectDestroyed(host);
            GlobalEnumEvent.Register(PlayModeTestEvent.E2, () => callbackCount++)
                .UnRegisterWhenGameObjectDestroyed(host);

            Object.Destroy(host);
            yield return null;

            GlobalEnumEvent.Broadcast(PlayModeTestEvent.E1);
            GlobalEnumEvent.Broadcast(PlayModeTestEvent.E2);

            Assert.AreEqual(0, callbackCount,
                "宿主销毁后，绑定到同一 EventUnregisterTrigger 的全部 Token 都必须安全注销。");
        }

        [UnityTest]
        public IEnumerator DisablingHostWithMultipleBindingsUnregistersAllWithoutMutationException()
        {
            int callbackCount = 0;
            GameObject host = new GameObject("EventHost_Disable_Multiple");
            EventUnregisterOnDisableTrigger lifecycle = host.AddComponent<EventUnregisterOnDisableTrigger>();

            GlobalEnumEvent.Register(PlayModeTestEvent.E1, () => callbackCount++)
                .UnRegisterWhenDisabled(lifecycle);
            GlobalEnumEvent.Register(PlayModeTestEvent.E2, () => callbackCount++)
                .UnRegisterWhenDisabled(lifecycle);

            host.SetActive(false);
            yield return null;

            GlobalEnumEvent.Broadcast(PlayModeTestEvent.E1);
            GlobalEnumEvent.Broadcast(PlayModeTestEvent.E2);

            Assert.AreEqual(0, callbackCount,
                "宿主失活后，绑定到同一 EventUnregisterOnDisableTrigger 的全部 Token 都必须安全注销。");

            Object.Destroy(host);
            yield return null;
        }
    }
}
