# Resources / 源码文档

## 模块职责

`Resources` 目录不是一个运行时代码模块，但它是多个模块默认资源约定的一部分。

它主要承担三类职责：

- 保存框架默认运行配置
- 保存默认 UI 资源
- 保存样例和验证所需的 Resources 后端资源

## 源码文件

与这部分资源直接关联的运行时代码主要包括：

- `Runtime/Kits/Reskit/Data/ResKitRuntimeSettings.cs`
- `Runtime/Kits/Reskit/Loaders/ResourceLoader/ResourceLoader.cs`
- `Runtime/Kits/UIKit/LoadStrategy/IUILoadStrategy.cs`
- `Runtime/Kits/UIKit/LoadStrategy/ResKitUILoadStrategy.cs`
- `Runtime/Kits/UIKit/LoadStrategy/IUILoadStrategy.cs`

## 总体结构

```text
Resources
├─ 运行时配置资产
│  ├─ ResKitRuntimeSettings
│  └─ UIKitSettings
├─ 默认管理器和 UIRoot
└─ 示例资源
```

## 关键资源

典型资源包括：

- `ResKitRuntimeSettings.asset`
- `UIKitSettings.asset`
- `Managers/UIKit.prefab`
- `UIPanel/UIRoot.prefab`
- `ResKitTest` 等示例资源

## 运行时依赖链

### ResKit

1. `ResKitRuntimeSettings.LoadOrCreateDefault()`
2. 尝试从 `Resources` 读取默认运行时配置
3. 若找不到，构造运行时默认对象

### UIKit

1. `UIKitSettings` 通过默认路径被读取
2. `ResKitUILoadStrategy` 根据设置加载 `UIRoot` 和面板 prefab

### ResourceLoader

1. `ResourceLoader.LoadRealSync(...)`
2. `Resources.Load(path)`
3. 封装为 `ResData`

异步路径同理走 `Resources.LoadAsync(...)`

## 关联类型

### 运行时设置类

- `ResKitRuntimeSettings`
- `UIKitSettings`

### 加载器

- `ResourceLoader`

### 示例与 Quick Start

- 样例构建逻辑会补齐或修复一部分 Resources 资产
- Quick Start 默认路线依赖这些资产存在

## 资源路径约定

`Resources` 路径有明显约定性：

- 代码里不会带 `Resources/`
- 通常也不带扩展名

例如：

```csharp
Resources.Load<TextAsset>("Configs/GameSetting");
```

因此一旦改资源路径，要同步检查所有硬编码路径和默认设置。

## 设计约束

- `Resources` 更适合默认配置和固定资源
- 不适合大规模、频繁更新的生产资源
- 路径和命名是代码约定的一部分，不能随意移动或重命名

## 常见误用

- 把大量生产资源长期塞进 `Resources`
- 改资源结构但不改默认配置路径
- 以为 `Resources` 资源天然支持热更新

## 测试与验证

- `ResKit_Playable.unity`
- Quick Start 样例构建后验证默认资源是否存在
- 相关配置读取、UIRoot 加载和默认样例资源是否正常
