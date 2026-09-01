# StellarFrameworkVerification

## 定位

StellarFrameworkVerification 是维护者专用验证区（Maintainer-only / Not distributed）。它不属于 Runtime Kit、用户 Sample、Catalog Profile 或普通导出包。

当前保留：

~~~text
Assets/StellarFrameworkVerification
├─ Editor
├─ Example_FrameworkValidation
├─ Scenes
├─ README.md
└─ ValidationArchitecture.md
~~~

Editor 下的发布前自检入口、Example_FrameworkValidation 下的集中 Runner 和 Scenes 下的 FrameworkValidation_Playable 场景，统一定义为 General Integration / Legacy Verification Runner。不要为了新规范重做或删除现有 Runner。

## 允许内容

- 多 Kit 的接口、生命周期和关键数据流验证；
- Player、IL2CPP、Addressables、HybridCLR、远端热更 Smoke；
- 真实 unitypackage、Bootstrap、Manifest、Bundle、DLL、AOT metadata 的发布前检查；
- 维护者 Runner、发布前工具和最小 Debug 文本/Primitive/Gizmo。

## 禁止内容

- 用户教学 Sample、Runtime Kit 或游戏内容库；
- Crop、NPC、Building、Farm、Logistics、Economy、Inventory UI 等业务玩法；
- 正式美术、动画、音乐、长期维护的完整 Demo；
- 将 StellarFrameworkVerification 注册成 kit、sample 或 adapter Profile。

未来只有在有真实实现和证据时才创建 Integration、PlayerSmoke、Release、Tools 目录；本轮不创建空目录和 FoundationIntegration Scene。Foundation Integration 应使用 FakeEntity、TestAgent、MovingPoint、TestPayload 等中性语义。

## 运行与发布

本区是 Release Gate 的维护入口，不替代 EditMode/PlayMode：

1. 先运行 Tests 的 Local Fast/Framework Gate。
2. 再在独立空白工程导入导出包，确认依赖闭包和安装器。
3. 在目标 Player/IL2CPP 环境验证 AA、HybridCLR 和远端热更。
4. 把真实结果写入 KitExportValidationMatrix；无法运行写 BLOCKED、SKIPPED 或 NOT RUN。

验证架构、目录和 Evidence 规则见 [ValidationArchitecture.md](ValidationArchitecture.md)。
