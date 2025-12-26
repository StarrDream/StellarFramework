# StellarFramework

[![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![Version](https://img.shields.io/badge/Version-1.0.0-green.svg)](https://github.com/)
[![License](https://img.shields.io/badge/License-MIT-orange.svg)](https://opensource.org/licenses/MIT)

> **让 Unity 开发回归代码的纯粹与优雅。**

**StellarFramework** 是一套专为独立开发者项目打造的高性能 Unity 开发框架。
它拒绝臃肿的“全家桶”式设计，而是专注于解决 Unity 开发中真正的痛点：**GC 峰值、异步逻辑混乱、资源引用泄露、架构耦合**。

---

## 📥 安装与下载 (Installation)

你可以通过以下几种方式将 StellarFramework 集成到你的项目中：

### 方式一：通过 UPM 安装 (推荐)
打开 Unity 的 `Window -> Package Manager`，点击左上角 `+` 号，选择 `Add package from git URL...`，填入本仓库地址：

```text
https://github.com/StarrDream/StellarFramework.git?path=/Assets/StellarFramework
```

### 方式二：手动安装
1. 下载本仓库的 [Releases](https://github.com/StarrDream/StellarFramework/releases) 中的 `.unitypackage` 包。
2. 将其导入到你的 Unity 项目中。

---

## 🛠️ 核心依赖 (Dependencies)

本框架深度依赖现代 Unity 异步流，使用前请确保项目中包含以下库：

1.  **[UniTask](https://github.com/Cysharp/UniTask)** (必须)
    *   用于驱动 ActionKit、ResKit、UIKit 的所有异步逻辑，替代 Coroutine 实现 0GC 异步。
2.  **[Newtonsoft.Json](https://github.com/jilleJr/Newtonsoft.Json-for-Unity)** (必须)
    *   用于 ConfigKit 和 NetworkKit 的序列化处理。
3.  **Addressables** (可选)
    *   若需使用 `ResLoaderType.Addressable` 模式，请安装 Addressables 包。
    *   **注意**：安装后需在 `Project Settings -> Player -> Scripting Define Symbols` 中添加宏 `UNITY_ADDRESSABLES` 以启用相关代码。

---

## 💡 设计初衷与哲学 (Design Philosophy)

在多年的商业项目实战中，我们发现传统的 Unity 开发模式存在几个顽疾：
*   **回调地狱**：资源加载嵌套、UI 动画回调嵌套，代码难以维护。
*   **GC 刺客**：大量的 `Action` 闭包、装箱拆箱 (`boxing`)、协程 (`IEnumerator`) 分配，导致手机发热掉帧。
*   **生命周期失控**：UI 关了动画还在播、物体销毁了事件还没注销，导致 `MissingReferenceException` 频发。

**StellarFramework 的设计原则：**

*   **⚡ 性能优先 (Performance First)**：在底层实现上追求极致。例如 `EventKit` 利用泛型静态类消除字典查找开销，`BindableKit` 使用双向链表实现 0GC 通知。
*   **🌊 拥抱异步 (Async Native)**：全面放弃 Coroutine，使用 `UniTask` 重构所有异步流。代码即文档，逻辑线性化。
*   **🛡️ 安全闭环 (Safety)**：强制的生命周期绑定机制。`UnRegisterWhenGameObjectDestroyed` 贯穿全框架，根绝空引用报错。
*   **🧱 架构分离 (Clean Architecture)**：基于 **MSV (Model-Service-View)** 模式，强制分离数据、逻辑与表现。

---

## 📦 功能模块预览 (Modules)

### 1. 核心架构 (Core Architecture)
*   **MSV 模式**：提供 `IModel` (数据)、`IService` (逻辑)、`IArchitecture` (容器)。
*   **SingletonKit**：
    *   **去 Find 化**：严厉禁止 `FindObjectOfType`，使用注册表模式。
    *   **生命周期管理**：严格区分 `Global` (全生命周期) 和 `Scene` (随场景销毁) 单例。
    *   **线程安全**：内置主线程检查，防止异步线程访问 Unity API 导致崩溃。

### 2. 异步与动画 (ActionKit)
*   **链式编程**：`MonoKit.Sequence(go).Delay(1f).MoveTo(...).Start()`。
*   **UniTask 驱动**：比 DOTween 更轻量，完全基于 `UniTask`，无缝融入 `await` 流程。
*   **自动取消**：自动绑定 GameObject 的 `CancellationToken`，物体销毁时动画自动停止。

### 3. 事件系统 (EventKit)
*   **物理隔离**：使用 `private static class EventBox<T>` 物理隔离不同枚举类型的存储。
*   **零装箱**：`Enum` 作为 Key 时**不产生装箱 GC**。
*   **极速调用**：结构体事件 (`GlobalStructEvent`) 直接调用静态委托，性能接近原生 C# 调用。

### 4. 数据绑定 (BindableKit)
*   **0GC 设计**：放弃 C# `event`，使用**对象池 + 双向链表**管理观察者。
*   **全类型支持**：支持 `BindableProperty<T>`、`BindableList<T>`、`BindableDictionary<K,V>`。
*   **防忘注销**：内置自动注销触发器。

### 5. 资源管理 (ResKit)
*   **统一接口**：无论是 Resources 还是 Addressables，上层 API 统一。
*   **引用计数**：全自动管理资源生命周期，A 和 B 都释放了，资源才卸载。
*   **并发去重**：同一帧对同一资源发起 10 次请求，底层只执行 1 次 IO。

### 6. UI 系统 (UIKit)
*   **层级管理**：内置 Bottom/Middle/Popup/Top/System 五层栈式管理。
*   **泛型加载**：`OpenPanelAsync<LoginPanel>()`，约定大于配置。
*   **异步生命周期**：`OnOpen` 支持 `async/await`，轻松处理打开时的入场动画或网络请求。

### 7. 实用工具链 (Toolchain)
*   **AudioKit**：支持优先级剔除 (Priority Eviction) 的音效池，防止爆音。
*   **NetworkKit**：自动 Token 注入、防重复请求、大文件断点下载。
*   **Editor Tools**：包含字典可视化编辑、资源引用查找、批量重命名等实战工具。

---

## ⚖️ 优势与不足 (Pros & Cons)

| 特性 | 说明 |
| :--- | :--- |
| ✅ **极低开销** | 大量使用对象池、静态类泛型缓存、Struct 优化，适合对性能敏感的移动端项目。 |
| ✅ **开发高效** | `UniTask` 让异步逻辑像写同步代码一样流畅；丰富的 Editor 工具减少了配置时间。 |
| ✅ **商业稳定** | 经过实战检验的引用计数机制和异常处理（Try-Catch、CancellationToken）。 |
| ✅ **无侵入性** | 模块间耦合度低，你可以只用 `EventKit` 而不用 `UIKit`。 |
| ❌ **学习门槛** | 开发者需要熟悉 `UniTask` 和 `async/await` 编程范式。 |
| ❌ **IL2CPP体积** | 泛型的大量使用可能导致 IL2CPP 代码体积略微膨胀（可通过 Strip Engine Code 缓解）。 |
| ❌ **严格规范** | 框架限制了一些“随意”的写法（如禁止直接 Find 单例），初学者可能需要适应期。 |

---

## 🔌 拓展性 (Extensibility)

StellarFramework 不是一个封闭的黑盒，它预留了丰富的扩展接口：

*   **ResKit**：继承 `ResLoader` 即可实现自定义加载器（如 RawFileLoader, AssetBundleLoader）。
*   **ActionKit**：通过 C# 扩展方法 (`this UniActionChain chain`)，可以轻松添加自定义的 Tween 动画节点（如 `DoText`, `DoShader`）。
*   **Service**：业务逻辑层完全是普通的 C# 类，不强制继承 MonoBehaviour，易于进行单元测试或移植到服务器端。

---

## 🚀 快速开始 (Quick Start)

### 1. 初始化框架
在游戏入口 (`GameEntry.cs`) 调用：

```csharp
void Awake() 
{
    // 启动架构容器
    GameApp.Interface.Init();
    // 初始化 UI 系统
    UIKit.Instance.Init();
}
```

### 2. 监听事件
```csharp
// 注册事件，并绑定当前 GameObject 的生命周期
// 当物体销毁时，自动注销事件，防止空引用
GlobalEnumEvent.Register(GameEvent.Start, OnGameStart)
               .UnRegisterWhenGameObjectDestroyed(gameObject);
```

### 3. 加载资源
```csharp
// 申请一个加载器
var loader = ResKit.Allocate<ResourceLoader>();

// 异步加载（支持 await）
var prefab = await loader.LoadAsync<GameObject>("Hero");

// ... 使用资源 ...

// 释放加载器（自动释放其加载的所有资源引用）
loader.Recycle2Cache();
```

---

## 👤 作者与致谢 (Author & Credits)

*   **作者**: 小梦
*   **QQ**: 2649933509

**致谢**:
*   [QFramework](https://github.com/liangxiegame/QFramework) (架构思想启发)
*   凉鞋大大
*   以及所有帮助过我的朋友们

---

Copyright © 2024 StellarFramework. Released under the MIT License.
