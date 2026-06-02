# UIKit / 界面系统源码文档

## 源码位置

- `Runtime/Kits/UIKit/UIKit.cs`：UI 门面、面板字典、页面栈、快照和压力测试。
- `Runtime/Kits/UIKit/UIPanelBase.cs`：面板基类、层级和生命周期。
- `Runtime/Kits/UIKit/UIKitSettings.cs`：UI 配置资产。
- `Runtime/Kits/UIKit/IUILoadStrategy.cs`：UI 加载策略接口。
- `Runtime/Kits/UIKit/ResKitUILoadStrategy.cs`：通过 ResKit 加载 UI Prefab。
- `Runtime/Kits/UIKit/UIAutoBind.cs`：UI 自动绑定标记。
- `Editor/StellarToolsHub/Modules/UIKitHubModule.cs`：UIKit 工具入口。

## 核心类型

- `UIKit`：继承 `MonoSingleton<UIKit>`，是 UI 系统唯一运行时入口。
- `UIKitRuntimeSnapshot`：运行时面板、栈、加载状态快照。
- `UIPanelBase`：所有 UI 面板基类。
- `UIPanelDataBase`：面板打开数据基类。
- `UIPanelBase.PanelLayer`：面板层级。
- `UIPanelBase.PanelCanvasRole`：Canvas 角色。
- `IUILoadStrategy`：UI Prefab 加载和释放接口。
- `ResKitUILoadStrategy`：用 ResKit 作为 UI 加载后端。
- `UIKitSettings`：UIRoot、面板路径、加载方式、层级等配置。
- `UIAutoBind`：标记需要生成绑定字段的节点。

## 关键方法

- `UIKit.InitAsync()`：加载 settings、创建或查找 UIRoot、初始化加载策略和层级。
- `UIKit.Open<TPanel>` / `OpenAsync<TPanel>`：加载或复用面板并调用生命周期。
- `UIKit.Push<TPanel>` / `PushAsync<TPanel>`：打开页面并维护页面栈。
- `UIKit.Pop` / `PopTo<TPanel>` / `ClearStack`：栈导航。
- `UIKit.Close<TPanel>` / `ClosePanel(Type)` / `CloseAllPanels`：关闭面板。
- `UIKit.DestroyAllPanels`：销毁所有面板实例并清空状态。
- `UIKit.Preload<TPanel>` / `PreloadAsync<TPanel>`：提前加载面板但不显示。
- `UIKit.TakeSnapshot`：生成审计快照。
- `UIPanelBase.OnOpen` / `OnClose` / `OnPause` / `OnResume`：面板生命周期。
- `UIPanelBase.TryGetPanelData<T>`：安全读取面板数据。

## 数据流

启动时，`UIKit.InitAsync` 读取 `UIKitSettings`，准备 UIRoot、层级 Canvas 和 `IUILoadStrategy`。业务调用 `UIKit.OpenAsync<TPanel>` 时，UIKit 按 panel 类型查找配置路径，通过加载策略加载 Prefab，实例化到对应 layer，调用面板 `OnOpen(data)`。页面栈调用 `PushAsync` 时，新页面进入栈顶；如果新页面是 Full Screen，下层面板会 `OnPause` 并隐藏。`Pop` 时关闭栈顶并恢复下层页面。

## 依赖关系

- 依赖 SingletonKit 的 `MonoSingleton<UIKit>`。
- 默认加载策略依赖 ResKit。
- 热更 UI 依赖 Addressables 或其他 ResKit 后端。
- UI 自动绑定和工具侧生成依赖 ToolsHub 的 UIKit 模块。
- 样例资源依赖 Resources 中的 UIRoot 和 Samples 生成物。

## 扩展点

- 新增加载后端：实现 `IUILoadStrategy`，并在 `UIKitSettings` 或初始化逻辑中接入。
- 新增面板：继承 `UIPanelBase`，配置路径，按需定义 `UIPanelDataBase`。
- 新增层级：扩展 `PanelLayer` 和 UIRoot 层级创建逻辑。
- 新增绑定生成规则：扩展 `UIAutoBind` 和 ToolsHub UIKit 工具。
- 新增运行时审计：扩展 `UIKitRuntimeSnapshot`，ToolsHub 可读取快照展示。

## 测试入口

- `UIKit_Playable.unity`：UI 打开、关闭、页面栈和样例面板。
- `FrameworkValidation_Playable.unity`：集中验证 UI 入口。
- ToolsHub `UIKit 工具`：绑定生成和样例修复。
- 修改加载、关闭或栈逻辑后，至少验证 OpenAsync、PushAsync、Pop、Full Screen 暂停恢复、DestroyAllPanels。
