using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.PlayMode
{
    /// <summary>
    /// UIKit PlayMode 冒烟测试。该验证不依赖 Samples。
    /// </summary>
    public class UIKitResKitPlayModeTests
    {
        [UnityTest]
        public IEnumerator UIKit_Init_Succeeds()
        {
            yield return UIKit.Instance.InitAsync().ToCoroutine();

            UIKitRuntimeSnapshot snapshot = UIKit.TakeSnapshot();
            Assert.IsTrue(snapshot.IsInitialized, snapshot.ToMultilineString());
        }

    }
}
