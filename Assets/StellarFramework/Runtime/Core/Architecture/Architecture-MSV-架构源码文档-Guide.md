# Architecture / MSV 架构源码文档

这份文档面向维护者，说明 MSV 架构代码如何组织、关键类型怎么协作、扩展或修改时应该看哪里。

## 源码位置

- `Runtime/Core/Architecture/StellarFramework.cs`：MSV 架构主体，包含接口、`Architecture<T>`、Model/Service/View 基类和 View 扩展方法。
- `GameApp.cs`：项目默认架构入口示例。
- `GameEntry.cs`：默认场景启动入口示例。
- `Samples/ArchitectureDemo`：架构样例。
- `Tests/EditMode/FrameworkValidation`：架构与样例策略测试。

## 核心类型

- `ArchitectureState`：架构生命周期状态，区分未初始化、初始化中、已初始化、已释放。
- `IArchitecture`：写入侧架构接口，提供注册、获取 Model 和 Service 的能力。
- `IReadOnlyArchitecture`：只读侧架构接口，给 View 或外部查询只读 Model。
- `IModule`：Model 和 Service 的公共模块接口，持有 `Architecture` 引用。
- `IModel` / `IReadOnlyModel`：状态模块接口。`IModel` 可写，`IReadOnlyModel` 用于只读暴露。
- `IService`：服务模块接口。
- `IView`：View 侧接口，暴露 `Architecture`。
- `Architecture<T>`：架构容器单例，负责模块注册、初始化、查询、释放。
- `AbstractModel`：Model 基类，封装架构引用和初始化回调。
- `AbstractService`：Service 基类，封装架构引用和模块访问。
- `StellarView`：MonoBehaviour View 基类，负责绑定和解绑生命周期。
- `StellarArchitectureExtensions`：给 `IView` 提供 `GetModel<T>`、`GetReadOnlyModel<T>`、`GetService<T>` 等快捷方法。

## 关键方法

- `Architecture<T>.Interface`：懒创建架构单例，是业务入口。
- `Architecture<T>.Init()`：把架构从未初始化推进到已初始化，内部调用 `InitModules()`。
- `Architecture<T>.InitModules()`：派生类实现，注册所有 Model 和 Service。
- `RegisterModel<T>(T model)`：写入 Model 字典，设置模块所属架构，并执行初始化。
- `RegisterService<T>(T service)`：写入 Service 字典，设置模块所属架构，并执行初始化。
- `GetModel<T>()`、`GetReadOnlyModel<T>()`、`GetService<T>()`：按类型查询模块。
- `Dispose()`：释放架构，清理模块引用和状态。
- `StellarView.OnBind()`：View 建立监听和依赖关系。
- `StellarView.OnUnbind()`：View 释放监听和临时状态。

## 数据流

1. 场景入口调用 `GameApp.Interface.Init()`。
2. `Architecture<T>` 创建实例并进入初始化状态。
3. 派生类 `InitModules()` 注册 Model 和 Service。
4. Model 保存状态，例如 `BindableProperty<T>`。
5. Service 通过 `GetModel<T>()` 读取并修改 Model。
6. View 继承 `StellarView`，通过 `this.GetModel<T>()` 订阅状态，通过 `this.GetService<T>()` 触发业务。
7. View 销毁时执行解绑，架构释放时统一清理模块。

## 依赖关系

- 依赖 Unity `MonoBehaviour`，只在 `StellarView` 和场景入口层体现。
- 可配合 `BindableKit` 做状态通知。
- 可配合 `EventKit` 做跨模块事件广播。
- 不直接依赖 `UIKit`、`ResKit`、`SettingsKit` 等 Kit，但 Service 可以调用它们。

## 扩展点

- 新增业务架构：继承 `Architecture<T>`，在 `InitModules()` 注册模块。
- 新增 Model：继承 `AbstractModel`，只存业务状态和只读导出。
- 新增 Service：继承 `AbstractService`，封装业务能力。
- 新增 View：继承 `StellarView`，实现 `Architecture`、`OnBind()`、`OnUnbind()`。
- 新增只读访问：让 Model 实现 `IReadOnlyModel`，外部通过 `GetReadOnlyModel<T>()` 获取。

## 修改风险

- 修改 `Architecture<T>.Interface` 会影响所有架构单例入口。
- 修改模块字典注册逻辑会影响 Model/Service 查找和生命周期。
- 修改 `StellarView` 生命周期会影响 UI、样例和业务 View 的监听释放。
- 修改 `IView` 扩展方法需要检查所有 View 侧调用。

## 测试入口

- `Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity`：架构示例场景。
- `Assets/StellarFrameworkVerification/Scenes/FrameworkValidation_Playable.unity`：外置集中验证入口。
- `Tests/EditMode/FrameworkValidation`：文档和样例入口策略测试。
- 修改架构后至少运行 QuickStart/onboarding 相关 EditMode 测试，并在需要时手动打开外置验证区的 FrameworkValidation 场景确认 View 绑定正常。
