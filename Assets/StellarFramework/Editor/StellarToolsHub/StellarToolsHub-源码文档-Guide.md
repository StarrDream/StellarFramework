# StellarToolsHub / 源码文档

## 模块职责

`StellarToolsHub` 是框架的统一编辑器入口。

它负责：

- 扫描和注册工具模块
- 对工具模块做分组、排序和搜索过滤
- 用 UI Toolkit 搭建主界面
- 用 `ToolModule` 抽象具体工具

## 源码文件

- `Editor/StellarToolsHub/Core/StellarFrameworkTools.cs`
- `Editor/StellarToolsHub/Core/ToolModule.cs`
- `Editor/StellarToolsHub/Core/StellarToolAttribute.cs`
- `Editor/StellarToolsHub/Core/ToolsHubEmbeddedPanel.cs`
- `Editor/StellarToolsHub/Modules/*`

## 总体结构

```text
StellarFrameworkTools
├─ PreferredGroupOrder
├─ _allModules
├─ _groupedModules
├─ _currentModule
├─ _search
├─ _sidebarScrollView
└─ _contentHost

ToolModule
├─ Initialize(window)
├─ CreateView()
├─ OnGUI()
├─ OnEnable()
└─ OnDisable()

StellarToolAttribute
└─ Title / Group / Order
```

## 打开窗口调用链

1. 菜单 `StellarFramework/Tools Hub`
2. `ShowWindow()`
3. `OnEnable()`
4. `ScanAndRegisterModules(...)`
5. `CreateGUI()`
6. `RebuildUi()`
7. 生成顶部栏、侧栏、内容区、底部栏

## 模块扫描调用链

1. `GetToolModuleTypes()`
2. 遍历 `ToolModule` 派生类型
3. 查找 `[StellarTool]`
4. 实例化模块
5. `module.Initialize(this)`
6. 写入 `Title / Group / Order`
7. 加入 `_allModules`
8. 构建 `_groupedModules`

## 界面刷新调用链

### 侧栏

1. `RefreshSidebar()`
2. 根据 `_search` 过滤模块
3. 按 Group 绘制标签
4. 为每个模块生成按钮
5. 当前模块按钮高亮

### 内容区

1. `RefreshContent()`
2. 若模块实现 `CreateView()`，直接挂载 UI Toolkit 视图
3. 否则创建 `IMGUIContainer`
4. 调用模块 `OnGUI()`

## 类型详解

## `StellarFrameworkTools`

### 作用

ToolsHub 主窗口。

### 关键字段

- `PreferredGroupOrder`
  指定分组显示顺序。
- `_allModules`
  全部工具模块实例。
- `_groupedModules`
  分组后的模块表。
- `_moduleButtons`
  模块到按钮映射。
- `_currentModule`
  当前选中模块。
- `_search`
  搜索关键字。
- `_searchField`
  搜索框。
- `_sidebarScrollView`
  左侧滚动列表。
- `_moduleTitleLabel`
- `_moduleDescriptionLabel`
- `_contentHost`
  右侧内容容器。
- `_legacyStylesReady`
  IMGUI 旧样式是否已准备。

### 关键方法

#### `ShowWindow()`

创建并显示窗口。

#### `OnEnable()`

重新扫描模块；若 UI 已存在则重建。

#### `OnDisable()`

转发到当前模块 `OnDisable()`。

#### `OnSelectionChange()`

把 Unity 选择变化转发给当前模块。

#### `CreateGUI()`

UI Toolkit 入口，内部调用 `RebuildUi()`。

#### `RebuildUi()`

重新搭建整个窗口：

- TopBar
- Sidebar
- Content
- Footer

#### `RefreshSidebar()`

刷新模块按钮列表和搜索结果。

#### `RefreshContent()`

刷新右侧模块内容区。

#### `SelectModule(...)`

切换当前模块并调用生命周期。

#### `ScanAndRegisterModules(...)`

扫描 `[StellarTool]` 模块并实例化注册。

#### `GetToolModuleTypes()`

优先使用 `TypeCache` 获取所有 `ToolModule` 派生类型。

## `ToolModule`

### 作用

所有具体工具模块的统一抽象基类。

### 典型职责

- 返回模块标题和描述
- 构建 UI Toolkit 视图
- 或提供 IMGUI 绘制逻辑
- 参与启用、停用、选择变化生命周期

## `StellarToolAttribute`

### 作用

声明模块元数据：

- `Title`
- `Group`
- `Order`

ToolsHub 扫描时依赖它决定：

- 是否把某个 `ToolModule` 暴露到窗口里
- 它应该出现在什么分组
- 它在组内的顺序

## `ToolsHubEmbeddedPanel`

### 作用

承载一些工具子面板的嵌入式绘制基类，用于把独立工具界面挂到模块窗口里。

## 模块层次

### 主入口层

- `DocumentationHubModule`
- `QuickStartHubModule`
- `AAWorkflowPublishHubModule`
- `HybridCLRHotUpdateExporterHubModule`
- `ResKitAuditHubModule`
- `SettingsKitHubModule`
- `UIKitHubModule`
- `DeveloperQuickToolsHubModule`

### 辅助工具层

- `AssetBundleToolModule`
- `ConfigKitHubModule`
- `AudioKitHubModule`
- `EventKitTrackerHubModule`
- `BuiltInModules`

## 设计约束

- 主窗口负责“发现、组织、展示”，不承载全部业务逻辑
- 复杂工作流应下沉到模块或独立逻辑类
- 新模块必须继承 `ToolModule` 并加 `[StellarTool]`
- UI Toolkit 是主界面层，IMGUI 主要用于兼容旧工具

## 常见误用

- 直接把复杂业务逻辑塞进主窗口类
- 模块没加 `[StellarTool]` 导致扫不到
- 模块在 `OnGUI()` 中抛异常且没有保护

## 测试建议

- 模块扫描与排序
- 搜索过滤
- 当前模块切换
- 文档中心索引
- Quick Start 入口
- AA / AB / HybridCLR 关键工具路径
