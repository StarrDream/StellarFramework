using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Examples;
using StellarFramework.Res;
using StellarFramework.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.PlayMode
{
    /// <summary>
    /// UIKit / ResKit PlayMode 冒烟测试。
    /// 前置：运行过样例构建器（UIRoot.prefab / ExamplePanel.prefab / TestCube_Res.prefab 存在）。
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

        [UnityTest]
        public IEnumerator UIKit_OpenClose_ExamplePanel()
        {
            yield return UIKit.Instance.InitAsync().ToCoroutine();

            ExamplePanel panel = null;
            yield return UIKit.OpenAsync<ExamplePanel>(new ExamplePanelData
            {
                TitleMessage = "PlayMode Test",
                RewardCount = 1
            }).ToCoroutine(result => panel = result);

            Assert.IsNotNull(panel, "UIKit.OpenAsync<ExamplePanel> 应成功返回面板实例。");

            UIKit.Close<ExamplePanel>();
            yield return null;

            UIKitRuntimeSnapshot snapshot = UIKit.TakeSnapshot();
            Assert.AreEqual(0, snapshot.LoadingPanelCount, "关闭后不应有加载中的面板。");
            Assert.AreEqual(0, snapshot.ActivePanelCount, "关闭后面板不应处于激活状态。");
        }

        [UnityTest]
        public IEnumerator ResKit_Loads_ResourcePrefab()
        {
            ResourceLoader loader = ResKit.Allocate<ResourceLoader>();
            loader.SetOwnerName("PlayModeTest");

            GameObject prefab = null;
            yield return loader.LoadAsync<GameObject>("ResKitTest/TestCube_Res")
                .ToCoroutine(result => prefab = result);

            Assert.IsNotNull(prefab, "Resources 加载 ResKitTest/TestCube_Res 失败（请先运行样例构建器）。");

            loader.Unload("ResKitTest/TestCube_Res");
            ResKit.Recycle(loader);
        }
    }
}
