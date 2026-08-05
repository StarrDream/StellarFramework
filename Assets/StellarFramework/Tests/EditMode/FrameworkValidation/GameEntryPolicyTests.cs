using NUnit.Framework;
using StellarFramework.Editor.Modules;

namespace StellarFramework.Tests.FrameworkValidation
{
    /// <summary>
    /// 验证"创建初始化目录"生成的 GameEntry 模板符合框架约定：
    /// - 架构只在 OnApplicationQuit 时 Dispose（不破坏运行期）
    /// - 不包含 OnDestroy 卸载逻辑（避免中途销毁架构）
    /// </summary>
    public sealed class GameEntryPolicyTests
    {
        [Test]
        public void GameEntryDisposesArchitectureOnlyOnApplicationQuit()
        {
            string source = InitProjectFoldersModule.GameEntryTemplate;

            Assert.That(source, Does.Contain("private void OnApplicationQuit()"));
            Assert.That(source, Does.Not.Contain("private void OnDestroy()"));
            Assert.That(source, Does.Contain("GameApp.Interface.Dispose()"));
        }

        [Test]
        public void GameAppTemplateInheritsArchitecture()
        {
            string source = InitProjectFoldersModule.GameAppTemplate;

            Assert.That(source, Does.Contain("class GameApp : Architecture<GameApp>"));
            Assert.That(source, Does.Contain("InitModules()"));
        }
    }
}
