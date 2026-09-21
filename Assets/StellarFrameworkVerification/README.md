# StellarFrameworkVerification

## 定位

StellarFrameworkVerification 是维护者专用验证区（Maintainer-only / Not distributed）。它不属于 Runtime Kit、用户 Demo、Catalog Profile 或普通导出包。

当前保留：

~~~text
Assets/StellarFrameworkVerification
├─ Editor
│  └─ ReleaseVerificationHubModule.cs
├─ README.md
└─ ValidationArchitecture.md
~~~

这里不再维护额外的 FrameworkValidation Playable 场景或集中 Runner。需要自动验证的行为进入 Tests；需要真实发布链路验证的内容进入维护者工具或目标平台 Player 验收。

## 允许内容

- Player、IL2CPP、Addressables、HybridCLR、远端热更 Smoke；
- 真实 unitypackage、Bootstrap、Manifest、Bundle、DLL、AOT metadata 的发布前检查；
- 发布前工具、环境检查和结果记录；
- 只有在确实需要目标平台/多系统集成时才新增专用验证资产。

## 禁止内容

- 用户教学 Demo、Runtime Kit 或游戏内容库；
- Crop、NPC、Building、Farm、Logistics、Economy、Inventory UI 等业务玩法；
- 正式美术、动画、音乐、长期维护的完整 Demo；
- 将 StellarFrameworkVerification 注册成 kit、sample 或 adapter Profile；
- 为了“看起来更完整”额外维护第二套示例场景。

## 运行与发布

本区是 Release Gate 的维护入口，不替代 EditMode/PlayMode：

1. 先运行 Tests 的行为、Policy 与性能验证。
2. 再在独立空白工程导入导出包，确认依赖闭包和安装器。
3. 在目标 Player/IL2CPP 环境验证 AA、HybridCLR 和远端热更。
4. 把真实结果写入 KitExportValidationMatrix；无法运行写 BLOCKED、SKIPPED 或 NOT RUN。

验证架构、目录和 Evidence 规则见 [ValidationArchitecture.md](ValidationArchitecture.md)。
