using System.Collections;
using NUnit.Framework;
using StellarFramework.Bindable;
using StellarFramework.Event;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.PlayMode
{
    /// <summary>
    /// BindableKit 回归测试：
    /// 1. BindableProperty 通知时序（相同值不通知、注销后不通知）。
    /// 2. ObserverNode 池化 use-after-free（与 EventKit 同款幽灵 bug）。
    /// </summary>
    public class BindableKitPlayModeTests
    {
        [Test]
        public void BindableProperty_Notifies_On_ValueChange()
        {
            var prop = new BindableProperty<int>(0);
            int changes = 0;

            IUnRegister token = prop.Register(v => changes++);

            prop.Value = 1;
            Assert.AreEqual(1, changes);

            prop.Value = 1;
            Assert.AreEqual(1, changes, "相同值不应触发通知。");

            token.UnRegister();
            prop.Value = 2;
            Assert.AreEqual(1, changes, "注销后不应再触发通知。");
        }

        [Test]
        public void BindableProperty_RegisterWithInitValue_InvokesImmediately()
        {
            var prop = new BindableProperty<int>(42);
            int received = -1;

            prop.RegisterWithInitValue(v => received = v);

            Assert.AreEqual(42, received, "RegisterWithInitValue 应立即回调一次当前值。");
        }

        [Test]
        public void BindableList_Notifies_Add_Remove()
        {
            var list = new BindableList<int>();
            int addCount = 0;
            int removeCount = 0;

            IUnRegister token = list.Register(e =>
            {
                if (e.Type == ListEventType.Add)
                {
                    addCount++;
                }
                else if (e.Type == ListEventType.Remove)
                {
                    removeCount++;
                }
            });

            list.Add(1);
            list.Add(2);
            list.Remove(1);

            Assert.AreEqual(2, addCount);
            Assert.AreEqual(1, removeCount);

            token.UnRegister();
        }

        [UnityTest]
        public IEnumerator ObserverNodePoolReuseDoesNotCancelNewCallback()
        {
            var prop = new BindableProperty<int>(0);
            int callbackBCount = 0;

            GameObject host = new GameObject("BindableHost");

            // 1. 注册回调 A 并绑定到 host 生命周期。
            IUnRegister tokenA = prop.Register(v => { });
            tokenA.UnRegisterWhenGameObjectDestroyed(host);

            // 2. 手动注销：ObserverNode 回池，但触发器仍持有引用。
            tokenA.UnRegister();

            // 3. 复用池中节点注册回调 B。
            prop.Register(v => callbackBCount++);

            // 4. 销毁旧宿主：修复前会误注销回调 B。
            Object.Destroy(host);
            yield return null;

            // 5. 改值：回调 B 必须收到通知。
            prop.Value = 5;

            Assert.AreEqual(1, callbackBCount,
                "修复前：复用后的 ObserverNode 被旧宿主销毁时误注销，新回调收不到通知（use-after-free）。");
        }
    }
}
