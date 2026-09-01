# PlayMode 测试

Assets/StellarFramework/Tests/PlayMode/ 存放必须依赖真实 Unity Runtime、生命周期、Coroutine、Scene、Resources、UIKit 或异步流程的行为测试。

## 与 EditMode 的分工

- EditMode：纯 C# Kit Behavior、Performance、Framework Policy，以及 Catalog、文档、打包、ToolsHub、AA/HotUpdate 的静态验证。
- PlayMode：真实 Runtime/Lifecycle/Resource 行为。不能仅因为“更真实”把纯 Foundation 测试移到这里。

## 运行方式

编辑器内打开 Window > General > Test Runner，选择 PlayMode，再运行 StellarFramework.PlayMode.Tests。

命令行：

~~~text
Unity -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml -logFile playmode.log
~~~

## 前置条件

先通过 Tools Hub > Start Here > Quick Start > 构建样例，保证以下 Resources 存在：

- Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab
- Assets/StellarFramework/Resources/UIPanel/ExamplePanel.prefab
- Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources/ResKitTest/TestCube_Res.prefab

UIKit/ResKit 测试依赖这些样例资源；EventKit、BindableKit、TimeKit 的生命周期测试不依赖大型 Demo。

## 当前测试清单（11 项）

| 文件 | 测试 | 验证内容 |
| --- | --- | --- |
| BindableKitPlayModeTests.cs | BindableProperty_Notifies_On_ValueChange | 相同值不通知、注销后不通知 |
|  | BindableProperty_RegisterWithInitValue_InvokesImmediately | 注册立即得到当前值 |
|  | BindableList_Notifies_Add_Remove | 列表 Add/Remove 通知 |
|  | ObserverNodePoolReuseDoesNotCancelNewCallback | ObserverNode 池化复用不会误注销新回调 |
| EventKitPlayModeTests.cs | TokenPoolReuseDoesNotCancelNewCallback | Token 池化复用不会误注销新回调 |
|  | ManualUnRegisterThenReuseIsSafe | 手动注销、复用和销毁顺序安全 |
| SaveKitPlayModeTests.cs | FileSystemStorageRoundTripAndCancellation | 真实 FileSystem Save/Load、Backup、Checksum、取消和 Delete |
| TimeKitPlayModeTests.cs | TimeKitUsesUnscaledTimeAndExplicitPause | unscaled time、Pause/Resume 与 timeScale |
| UIKitResKitPlayModeTests.cs | UIKit_Init_Succeeds | UIKit 异步初始化 |
|  | UIKit_OpenClose_ExamplePanel | 面板打开/关闭后 Loading、Active 清零 |
|  | ResKit_Loads_ResourcePrefab | Resources 后端真实加载 |

## Fixture 与日志

Fixture 只为测试服务；Samples 只为学习服务。测试故意触发的 Error/Warning 必须通过 LogAssert.Expect 或等价机制声明；未声明的 Console error 视为真实失败。

PlayMode 不承担 Integration Demo、玩法内容或 Release Player 验收。多 Kit 组合和目标平台发布见 StellarFrameworkVerification。
