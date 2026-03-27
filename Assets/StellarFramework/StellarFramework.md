# StellarFramework

[![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![Version](https://img.shields.io/badge/Version-1.0.0-green.svg)](https://github.com/)
[![License](https://img.shields.io/badge/License-MIT-orange.svg)](https://opensource.org/licenses/MIT)

> 面向 Unity 项目的模块化基础框架，当前重点提供架构、资源、UI、事件、绑定、对象池与编辑器工具等能力。

---

## 1. 框架定位
`StellarFramework` 当前更适合作为：
- **项目基础层**
- **团队二次收敛底座**
- **模块化接入的运行时工具集**

而不是一个“接入即解决所有项目问题”的全家桶方案。
框架当前已经具备一批可用模块，但不同模块的成熟度并不完全一致。  
在商业项目中，建议根据项目边界按需接入，并结合本项目规范继续收口。

---

## 2. 当前已提供的主要能力

### 2.1 架构核心
- 基于 **MSV (Model-Service-View)** 的基础架构容器
- `Architecture<T>` 提供 Model / Service 注册与获取
- View 通过接口或基类接入架构域
- 支持基础生命周期状态控制

### 2.2 资源管理
- `ResKit` 提供统一的资源加载器抽象
- 当前已包含：
    - `ResourceLoader`
    - `AssetBundleLoader`
    - `AddressableLoader`（需安装 Addressables）
- 支持：
    - 资源引用计数
    - 异步并发去重
    - 加载器回收后统一释放引用
- Editor 侧提供 AssetBundle 打包辅助工具

### 2.3 UI 系统
- `UIKit` 提供基础面板加载、实例化、关闭与缓存能力
- 支持：
    - 强类型面板数据
    - 层级节点管理
    - 基础异步打开流程
    - `UIStackManager` 的栈式导航扩展
- 默认通过 `ResKitUILoadStrategy` 接入 ResKit

### 2.4 事件系统
- `EventKit` 提供：
    - 枚举事件
    - 强类型结构体事件
- 支持生命周期绑定式自动注销

### 2.5 数据绑定
- `BindableKit` 提供：
    - `BindableProperty<T>`
    - `BindableList<T>`
    - `BindableDictionary<K,V>`
- 用于驱动 Model 变化到 View 刷新

### 2.6 对象池
- `PoolKit` 提供：
    - 纯 C# 对象池
    - 基于工厂委托的对象池
- 支持对象出池 / 回池生命周期回调

### 2.7 编辑器工具
- Tools Hub 统一入口
- 当前包含：
    - AssetBundle 工具
    - UIKit 工具
    - ConfigKit Dashboard
    - 序列化辅助工具
    - 文档中心
    - 若干生产力编辑器工具

---

## 3. 当前依赖
根据使用模块不同，项目可能需要以下依赖：

### 必选依赖
1. **[UniTask](https://github.com/Cysharp/UniTask)**
    - ActionKit
    - ResKit 异步链路
    - UIKit 异步流程
    - HttpKit 异步能力
2. **[Newtonsoft.Json](https://github.com/jilleJr/Newtonsoft.Json-for-Unity)**
    - ConfigKit
    - HttpKit JSON 反序列化
    - 部分 Editor 工具

### 可选依赖
1. **Addressables**
    - 仅当使用 `AddressableLoader` 或 Addressables 热更流程时需要
    - 使用前请定义宏：`UNITY_ADDRESSABLES`
2. **HybridCLR**
    - 仅当接入代码热更模块时需要
    - 使用前请定义宏：`HYBRIDCLR_ENABLE`

---

## 4. 当前架构原则
框架当前按以下原则设计与使用：

### 4.1 MSV 分层
- **Model**：只负责状态存储
- **Service**：只负责业务逻辑与状态修改
- **View**：只负责表现与交互转发

推荐数据流：
`View -> Service -> Model -> View刷新`

### 4.2 组件式开发优先
- 场景行为优先采用 `MonoBehaviour`
- 共用能力优先通过可组合组件或模块提供
- 不鼓励把多职责长期堆进单个“大管理器”

### 4.3 运行时主链路避免反射
当前框架已开始将部分运行时反射迁移到 Editor 生成阶段。  
例如单例系统采用静态注册表生成方式，而不是运行时扫描 Attribute。

### 4.4 服务端权威逻辑不在本仓库默认覆盖范围
当前仓库内并未形成一套完整的 Shared / Server / Client 显式网络协议框架。  
如果项目涉及多人同步，请在此基础上另行构建：
- Shared 共享协议定义
- Server 权威逻辑
- Client 表现与预测
- 显式消息同步与房间隔离机制

---

## 5. 当前模块成熟度说明
下面的说明更接近当前代码状态，而不是设计目标。

| 模块 | 当前状态 | 说明 |
| :--- | :--- | :--- |
| Architecture | 可用 | 已具备基础状态控制与注册机制，仍建议继续收紧访问边界 |
| ResKit | 较可用 | 是当前较值得继续打磨的主干模块之一 |
| UIKit | 可用 | 已具备基本框架能力，状态机与导航规则仍建议继续治理 |
| EventKit | 可用 | 适合轻量到中等规模项目使用 |
| BindableKit | 可用 | 可作为模型到界面的基础驱动层 |
| PoolKit | 可用 | 适合运行时纯 C# 对象与部分工厂对象复用 |
| LogKit | 基础可用 | 当前更偏统一入口，仍建议继续强化结构化诊断能力 |
| AudioKit | 可接入 | 适合中小规模音频管理，复杂项目建议继续扩展治理 |
| ConfigKit | 可接入 | 已具备基础配置读写与 Dashboard 入口 |
| HttpKit | 可接入 | 提供常见请求封装，但不等于完整生产网络层 |
| FSMKit | 可接入 | 适合作为轻量状态机基础件 |
| RaycastKit | 可接入 | 偏工具型封装 |
| HotUpdateKit | 边界模块 | 属于接入辅助层，不属于主干运行时核心 |

---

## 6. 不建议直接理解为“生产承诺”的内容
下面这些能力，当前仓库中可能有实现、示例或文档，但**不建议直接等同于生产承诺**：
- “全链路 0GC” (应理解为尽力减少堆内存分配的设计目标)
- “适合所有商业项目直接接入”
- “所有模块都已生产稳定”
- “接入后无需二次架构治理”
- “内置网络层可直接覆盖多人项目”

这些更适合作为：
- 设计方向
- 模块目标
- 某些场景下可达成的工程结果

而不是当前版本对所有模块的统一保证。

---

## 7. 快速开始

### 7.1 初始化架构
`GameApp.cs`
```csharp
using StellarFramework;

public class GameApp : Architecture<GameApp>
{
    protected override void InitModules()
    {
    }
}
```

`GameEntry.cs`
```csharp
using StellarFramework;
using UnityEngine;

public class GameEntry : MonoBehaviour
{
    private void Start()
    {
        GameApp.Interface.Init();
    }
}
```

### 7.2 打开 UI 系统
```csharp
await UIKit.Instance.InitAsync();
```

### 7.3 打开面板
```csharp
await UIKit.OpenPanelAsync<ExamplePanel>();
```

### 7.4 注册事件
```csharp
GlobalEnumEvent.Register(GameEvent.Start, OnGameStart)
    .UnRegisterWhenGameObjectDestroyed(gameObject);
```

### 7.5 分配资源加载器
```csharp
var loader = ResKit.Allocate<ResourceLoader>();
var prefab = await loader.LoadAsync<GameObject>("Hero");
ResKit.Recycle(loader);
```

---

## 8. 目录内容说明
当前仓库中同时包含几类内容：
- **Runtime**：正式运行时代码
- **Editor**：编辑器工具
- **Demo**：演示业务流转
- **Example**：模块示例用法
- **Generated**：生成代码产物
- **Art**：部分演示素材与资源

其中：
- `Demo`
- `Example`
  更偏示例与验证用途，不建议直接视为生产层结构模板。  
  正式商业项目中，建议将框架层与业务层进一步物理隔离。

---

## 9. 使用建议
如果你准备在项目里接入本框架，建议按下面顺序评估：
1. 先接入 `Architecture`
2. 再接入 `EventKit / BindableKit / PoolKit`
3. 然后根据资源方案选择 `ResKit`
4. 最后再评估 `UIKit / AudioKit / ConfigKit / HttpKit`

这样可以避免一次性接入过多模块导致边界不清。

---

## 10. 已知注意事项

### 10.1 不同模块成熟度不完全一致
请不要假设每个模块都已经达到同样的生产稳定度。

### 10.2 示例代码不等于生产规范全部落地
`Example` 和 `Demo` 主要用于展示调用方式与验证闭环，不应直接替代项目级规范。

### 10.3 Editor 自动化不代表 Runtime 允许照搬反射方案
部分 Editor 工具依赖反射扫描属于合理范围，运行时主链路仍应优先使用静态生成或显式注册。

### 10.4 UIKit 与 ResKit 仍建议继续强化审计能力
如果你的项目规模较大，建议继续补充：
- 状态快照
- 资源持有者诊断
- UI 栈导航可视化
- 面板状态审计
- 自动化测试

---

## 11. 作者与来源说明
- 作者：小梦
- 启发来源：
    - [QFramework](https://github.com/liangxiegame/QFramework)

当前仓库中的部分设计方向、工具形式与接口组织，带有明显的个人框架演进痕迹。  
如果用于团队项目，建议在接入后由项目主程继续做目录治理、边界收口与文档统一。

---

## 12. 许可证
Copyright © 2024 StellarFramework.  
Released under the MIT License.