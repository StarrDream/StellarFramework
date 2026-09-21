# PlayMode 测试

## 中文

`Assets/StellarFramework/Tests/PlayMode/` 只存放必须依赖真实 Unity Runtime、生命周期、Coroutine、Scene、UIKit 或异步流程的行为测试。

## 与 EditMode 的分工

- EditMode：纯 C# Kit Behavior、Performance、Framework Policy，以及 Catalog、文档、打包、ToolsHub、AA/HotUpdate 的静态验证。
- PlayMode：真实 Runtime / Lifecycle 行为。
- ArchitectureDemo：唯一用户入门 Demo，不等于测试套件。

## 运行方式

编辑器内打开 `Window > General > Test Runner`，选择 PlayMode，再运行 `StellarFramework.PlayMode.Tests`。

命令行：

~~~text
Unity -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml -logFile playmode.log
~~~

## 当前覆盖

- `ArchitectureDemoPlayModeTests`：真实 Mine 点击、Model 更新、UI/Localization 联动。
- `BindableKitPlayModeTests`：BindableProperty / BindableList 通知与池化生命周期。
- `EventKitPlayModeTests`：Token 注册、注销、复用和销毁顺序。
- `SaveKitPlayModeTests`：真实 FileSystem Save/Load、Backup、Checksum、取消和 Delete。
- `TimeKitPlayModeTests`：unscaled time、Pause/Resume 与 timeScale。
- `UIKitResKitPlayModeTests`：UIKit Runtime 初始化。

这些测试不要求先“构建样例”，也不依赖已删除的 KitSamples。

## Fixture 与日志

Fixture 只为测试服务；用户 Demo 只为学习服务。测试故意触发的 Error/Warning 必须通过 `LogAssert.Expect` 或等价机制声明；未声明的 Console error 视为真实失败。

Addressables 专用测试资源位于 `Tests/Fixtures`，AssetBundle 工具验证资源位于 `Generated/ToolingFixtures`，两者都不属于 Samples。

PlayMode 不承担真实 Player / IL2CPP / Remote HotUpdate 发布验收；目标平台发布验证见 `StellarFrameworkVerification`。

## English

`Assets/StellarFramework/Tests/PlayMode/` contains behavior tests that require the real Unity runtime, lifecycle, coroutine, scene, UIKit, or async execution.

The suite no longer depends on generated KitSamples. ArchitectureDemo is the only user-facing onboarding demo, while test fixtures live under dedicated Tests/Generated fixture locations.

Target-platform release validation remains in `StellarFrameworkVerification`.
