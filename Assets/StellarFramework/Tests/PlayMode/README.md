# PlayMode 测试

`Assets/StellarFramework/Tests/PlayMode/` 存放框架运行时的 PlayMode 测试，覆盖真实运行链路（进入 Play Mode 后验证），弥补原有仅 EditMode 测试的空白。

## 运行方式

1. 打开 Unity **Window > General > Test Runner**
2. 切到 **PlayMode** 标签
3. 选择 `StellarFramework.PlayMode.Tests` 程序集 → **Run All**

或命令行（CI）：
```bash
Unity -batchmode -quit -projectPath . \
  -runTests -testPlatform PlayMode \
  -testResults TestResults/playmode.xml \
  -logFile build.log
```

## 前置条件

- 已运行过样例构建器（ToolsHub → Quick Start → 构建样例），保证：
  - `Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab`
  - `Assets/StellarFramework/Resources/UIPanel/ExamplePanel.prefab`
  - `Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources/ResKitTest/TestCube_Res.prefab`
- UIKit 测试依赖上述 Resources 资源（ResKit 默认 Resources 后端）。

## 测试清单

| 文件 | 测试 | 验证内容 |
|---|---|---|
| `EventKitPlayModeTests.cs` | `TokenPoolReuseDoesNotCancelNewCallback` | **use-after-free 回归**：Token 回收复用后，旧宿主销毁不得误注销新回调（修复前会静默丢失事件） |
| | `ManualUnRegisterThenReuseIsSafe` | 手动注销 + 复用 + 销毁宿主的完整时序安全 |
| `BindableKitPlayModeTests.cs` | `BindableProperty_Notifies_On_ValueChange` | 相同值不通知、注销后不通知 |
| | `BindableProperty_RegisterWithInitValue_InvokesImmediately` | RegisterWithInitValue 立即回调当前值 |
| | `BindableList_Notifies_Add_Remove` | 列表 Add/Remove 事件 |
| | `ObserverNodePoolReuseDoesNotCancelNewCallback` | **use-after-free 回归**：Bindable 的 ObserverNode 池化复用安全 |
| `UIKitResKitPlayModeTests.cs` | `UIKit_Init_Succeeds` | UIKit 异步初始化成功 |
| | `UIKit_OpenClose_ExamplePanel` | 打开/关闭 ExamplePanel，关闭后 Loading=0、Active=0 |
| | `ResKit_Loads_ResourcePrefab` | Resources 后端加载 TestCube_Res |

共 9 个测试。

## 为什么需要 PlayMode 测试

- **use-after-free 回归**（EventKit/BindableKit）：Token/ObserverNode 池化 + 生命周期触发器绑定是极难肉眼排查的幽灵 bug，PlayMode 可真实触发 `Object.Destroy` → `OnDestroy` 时序。
- **UIKit 运行链路**：UI 面板的打开/关闭、单例初始化只能在 Play Mode 验证。
- **ResKit 加载**：Resources 异步加载的真实资源路径。

## 与 EditMode 测试的分工

- `Tests/EditMode/`：文档入口、ToolsHub 工作流、AA/HotUpdate 发布链路的静态/策略检查。
- `Tests/PlayMode/`：运行时行为（生命周期、异步、UI）。
