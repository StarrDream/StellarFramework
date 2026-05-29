using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.UIKit
{
    public sealed class UIKitStackSurfacePolicyTests
    {
        [Test]
        public void UIKitPublicSurfaceExposesStackOperationsDirectly()
        {
            string uiKitPath = Path.Combine(
                Application.dataPath,
                "StellarFramework/Runtime/Kits/UIKit/UIKit.cs");

            string source = File.ReadAllText(uiKitPath);

            Assert.That(source, Does.Contain("public static TPanel Push<TPanel>("));
            Assert.That(source, Does.Contain("public static async UniTask<TPanel> PushAsync<TPanel>("));
            Assert.That(source, Does.Contain("public static void Pop()"));
            Assert.That(source, Does.Contain("public static void PopTo<TPanel>()"));
            Assert.That(source, Does.Contain("public static void ClearStack()"));
        }

        [Test]
        public void UIKitSourceDoesNotDependOnUIStackManagerType()
        {
            string uiKitPath = Path.Combine(
                Application.dataPath,
                "StellarFramework/Runtime/Kits/UIKit/UIKit.cs");

            string source = File.ReadAllText(uiKitPath);

            Assert.That(source, Does.Not.Contain("UIStackManager"));
        }
    }
}
