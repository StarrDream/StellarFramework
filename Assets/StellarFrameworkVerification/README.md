# StellarFrameworkVerification

## 定位

StellarFrameworkVerification 是维护者专用验证区（Maintainer-only / Not distributed）。它不属于 Runtime Kit、用户 Demo、Catalog Profile 或普通导出包。

当前保留：

~~~text
Assets/StellarFrameworkVerification
├─ Editor
│  └─ ReleaseVerificationHubModule.cs
├─ Runtime
│  ├─ HotUpdateRuntimeVerification.cs
│  └─ HotUpdateVerificationPaths.cs
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

## HotUpdate PlayMode Release Gate

此 Gate 拆分为 Editor Prepare 和可被 Unity Test Runner 发现的 PlayMode Runtime Test。PlayMode 测试程序集只引用 `StellarFramework.Verification.Runtime` 与 UniTask，不引用 `UnityEditor`、`YooAsset.Editor` 或 Verification Editor assembly；它位于独立的 `StellarFramework.Tests.ReleaseGate` namespace / `StellarFramework.ReleaseGate` category，因此普通 `StellarFramework.Tests.PlayMode` 回归不需要外部验证包。

按以下顺序运行：

1. 在 Unity Editor 执行 `Tools/StellarFramework/Verification/Prepare HotUpdate PlayMode Release Gate`。这会按当前 Active Build Target 重新构建验证用 YooAsset 包，复制到 `Temp/StellarHotUpdateVerification/RemoteCDN`，并写入 `runtime-config.json`。
2. 刷新 Unity Test Runner 的 PlayMode discovery，确认列表中出现 `YooAssetHotUpdateEndToEndTests.PreparedPackageResumesRangeAndEntersHotUpdate`。
3. 只运行该精确 Gate。若准备配置或 package 缺失，测试会以 `precondition missing` 明确失败，不会退化成普通资源读取异常。
4. 等待 Unity Test Runner 的最终结果。PASS 断言覆盖强制下载中断与正 Range 恢复、YooAsset 更新、ResKit Manifest/DLL 加载、DLL SHA256、HybridCLR 入口程序集加载。

可通过 UnitySkills 将上述步骤自动串联：`POST /skill/editor_execute_menu`（上述 `menuPath`）→ `POST /skill/test_discover_start`（`testMode=PlayMode`，随后轮询 discovery job）→ `POST /skill/test_run_by_name`（精确 `testName=StellarFramework.Tests.ReleaseGate.YooAssetHotUpdateEndToEndTests.PreparedPackageResumesRangeAndEntersHotUpdate`, `testMode=PlayMode`）→ 轮询 `/skill/test_get_result` 直至 job 完成。发现结果必须实际包含该 Gate，不能以菜单执行成功代替 Test Runner discovery。

工作区提供一键驱动脚本：`Tools/Verification/Invoke-HotUpdatePlayModeReleaseGate.ps1`。在项目根目录执行 `./Tools/Verification/Invoke-HotUpdatePlayModeReleaseGate.ps1`；脚本会校验 UnitySkills 指向当前工程、依次 Prepare / fresh discovery / exact run / poll，并将 PASS/FAIL、job id、发现数量和测试结果写入 `Temp/StellarHotUpdateVerification/playmode-gate-result.json`。也可通过 `-UnitySkillsUrl`、`-TimeoutMinutes` 或 `-EvidencePath` 覆盖默认值。

## Android Release IL2CPP HotUpdate Gate

Android 验证继续使用 `Tools/AndroidVerification/` 的同一 AVD、ADB helpers、smoke runner 和 pipeline result。运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AndroidVerification\Invoke-StellarAndroidReleaseVerification.ps1 -HotUpdate
```

该 Profile 按 Android Active Build Target 执行 HybridCLR `Generate/All`：临时设置 Android IL2CPP + x86_64 + non-Development，生成 `HotUpdate.dll` 和 Android stripped AOT assemblies；之后通过现有 `HybridCLRHotUpdateAssetExporter` 写 Android Manifest / DLL SHA / metadata assets，再构建 Android YooAsset verification package。每项临时 Player 设置均在 `finally` 恢复。若 Active Build Target 不是 Android，Prepare 会明确失败，不会暗中留下 Target 切换。

管线将刚构建的 YooAsset package 通过只监听 `127.0.0.1` 的 Python 标准库 CDN 提供；它支持 GET / HEAD / HTTP Range 并保存 JSONL 请求证据。ADB 仅通过 `adb reverse` 转发同一端口。Android Intent extras 传递 CDN、package/version 和缓存期望值，不会修改项目 `HotUpdateSettings`。

Android Player 用持久化 YooAsset cache 执行冷启动和 force-stop 后的重启。两次运行都必须输出完整的结构化 JSON：冷启动须从 CDN 下载并填充缓存，重启须从缓存读取且报告 0 个重新下载文件；两次都验证 Android Manifest、ResKit Manifest/DLL、SHA256、HybridCLR AOT metadata、已加载的 `HotUpdate` Assembly，以及 `HotUpdate.HotUpdateMain.Main` 的入口日志 marker。Unity Android logger 会截断较长的单条消息，因此 Player 将 UTF-8 JSON 编为 Base64 并按 512 字符分块输出；Smoke runner 要求块序号完整且无重复，再重组并严格解析 JSON。结果、两次 logcat、截图、APK build state 和 CDN JSONL 位于 `Tools/AndroidVerification/Results/<run-id>/`。

这个 Android Release 证据与上面的 Editor Range fault-injection Gate 互补：前者证明目标平台 IL2CPP/HybridCLR 与设备缓存，后者证明 Range 中断恢复。UnitySkills 或 ADB/CDN harness 不能提供运行证据时，记录 **TOOLING EVIDENCE GAP**，不得把工具成功当成 Android 产品 PASS。
