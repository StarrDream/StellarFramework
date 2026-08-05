# StellarFramework 代码审查报告（2026-08-04）

> 审查方式：源码逐文件阅读 + Unity 实时状态核对 + 配置解析
> 审查范围：架构核心 / 14 个 Kit / StellarToolsHub 编辑器工具 / 测试 / asmdef / Addressables 配置 / HybridCLR 热更链路
> 严重程度分级：🔴 雷（必炸/数据丢失）→ 🟡 坑（特定场景必触发）→ 🟠 隐患（潜在风险）→ 🟢 建议

---

## 一、🔴 雷：必须优先修复

### 1. AOTGenericReferences.cs 完全为空 + link.xml 覆盖不足 → 热更代码一用泛型就崩
- **位置**: `Assets/HybridCLRGenerate/AOTGenericReferences.cs`、`Assets/HybridCLRGenerate/link.xml`
- **问题**: `PatchedAOTAssemblyList` 为空，`{{ AOT generic types }}` 空。热更 DLL 中 `List<T>`/`Dictionary<K,V>`/`Task<T>` 等跨 AOT 边界泛型未补充，IL2CPP 运行时必抛 `ExecutionEngineException`。link.xml 只 preserve 了 `UnityEngine.Debug`。
- **为什么是雷**: 热更代码只要用到泛型容器就崩，且 `ExecutionEngineException` 无堆栈，极难排查。
- **修法**: 跑一遍 HybridCLR `Generate/All` 重新生成 AOTGenericReferences；link.xml 补充热更侧用到的类型。

### 2. HybridCLR git 依赖未锁定版本
- **位置**: `Packages/manifest.json`（`com.code-philosophy.hybridclr` 裸 git URL）
- **问题**: 无 tag/commit 锁定，CI 或新机器拉到不同版本，反射调用 `LoadMetadataForAOTAssembly`（HybridCLRHook.cs）会静默失败。
- **修法**: 锁定到具体 tag，如 `#v6.9.0`。

### 3. 远端热更目录硬编码 `D:/HotUpdate/`（Windows-only）
- **位置**: `Assets/StellarFramework/Editor/StellarToolsHub/Modules/Addressables/AAHotUpdatePublishToolModule.cs` + `AddressableAssetSettings.asset`（"Stellar Remote HotUpdate" Profile）
- **问题**: 假设 D 盘存在、只兼容 Windows；CI/macOS/Linux 上默认配置立即失败。
- **修法**: 改为 `[ProjectRoot]/BuildArtifacts/HotUpdate/[BuildTarget]` 或可配置路径。

### 4. Addressables Remote Catalog 未开启 → 资源热更完全失效
- **位置**: `Assets/AddressableAssetsData/AddressableAssetSettings.asset`（`m_BuildRemoteCatalog: 0`）
- **问题**: `AddressableHotUpdateManager.CheckCatalogUpdatesAsync()` 调 `CheckForCatalogUpdates(false)` 永远检测不到更新，整套热更管线形同虚设。
- **修法**: 勾选 Build Remote Catalog，并正确配置 Remote Build/Load Path。

### 5. EventKit Token 池化复用 + 生命周期触发器持旧引用 → use-after-free
- **位置**: `Assets/StellarFramework/Runtime/Kits/EventKit/GlobalEnumEvent.cs`（`EnumEventToken<T>.UnRegister` → `Recycle`）+ `EventCore.cs`（`EventUnregisterTrigger.OnDestroy`）
- **问题**: ① `Register(A)` 拿 Token T，T 加入 `UnRegisterWhenGameObjectDestroyed(go)` 的触发器；② 手动 `T.UnRegister()` → T 回收到 `TokenPool`，但触发器仍持有 T；③ `Register(B)` 从池复用 T（`IsInUse=true`，Callback=B）；④ go 销毁 → 触发器调 `T.UnRegister()` → **错误取消 B 的注册**。B 永久收不到事件。
- **为什么是雷**: 极隐蔽的幽灵 bug，只在"手动注销 + 池复用 + 旧对象销毁"时序下触发。
- **修法**: Recycle 时从触发器 `_unRegisters` 移除自身，或 Token 记录归属触发器，UnRegister 时解除绑定。

### 6. HttpImageDownload Sprite/Texture 双 LRU 缓存互相销毁 → 图片白屏
- **位置**: `Assets/StellarFramework/Runtime/Kits/HttpKit/HttpImageDownload.cs`
- **问题**: SpriteCache 和 TextureCache 独立 LRU 淘汰。Sprite 引用的 Texture 可能被 TextureCache 淘汰并 `Object.Destroy`，Sprite 仍留在缓存中 → 显示时纹理已销毁（白屏或 MissingReference）。
- **修法**: 淘汰 Sprite 时同时检查/淘汰其纹理；或 Sprite 缓存持有纹理引用计数。

### 7. UIKit 关闭面板时 OnClose() 后访问可能已销毁的 CanvasGroup
- **位置**: `Assets/StellarFramework/Runtime/Kits/UIKit/UIKit.cs`（`ClosePanelInternal`、`DestroyAllPanels`）
- **问题**: `panel.OnClose()` 后直接 `panel.CanvasGroup.interactable = false`。若 OnClose 中销毁了 GameObject（DestroyOnClose 或业务代码），CanvasGroup 为 null → NullReferenceException；`panel.gameObject.SetActive(false)` 在已销毁对象上抛 MissingReferenceException。
- **修法**: OnClose() 后用 Unity 假值检查 `if (panel == null) return;`，并 try-catch 保护状态一致性。

### 8. Architecture<T> 在 Domain Reload 关闭下的静态实例残留（僵尸实例）
- **位置**: `Assets/StellarFramework/Runtime/Core/Architecture/StellarFramework.cs`（`_instance` 静态字段）
- **问题**: `Enter Play Mode Options > Reload Domain = false` 时，上次 Play 会话残留的 `_instance` 若状态未置 Disposed（异常退出），下次 Init 拿到僵尸容器，内部持有已销毁对象。
- **修法**: 加 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 重置 `_instance = null`（SingletonFactory 已这么做）。

### 9. Architecture<T>.Init 部分失败回滚 → Model 重复初始化
- **位置**: `StellarFramework.cs`（Init 的 null 检测）
- **问题**: 第 3 个 Model 为 null 时前 2 个已 Init，状态回滚 Uninitialized，再次 Init 会重复初始化前 2 个 → 事件重复注册、数据覆盖。
- **修法**: 遍历前先全量 null 校验。

### 10. StringExtensions.CheckCharMatch 每次调用分配 256KB
- **位置**: `Assets/StellarFramework/Runtime/Extensions/StringExtensions.cs`
- **问题**: `int[char.MaxValue + 1]` = 256KB 堆分配/次，高频过滤场景造成 GC 卡顿。
- **修法**: 用 `Dictionary<char,int>` 或仅统计输入中出现过的字符。

### 11. AssetBundle 工具初始化会无条件覆盖用户 Prefab/材质
- **位置**: `Assets/StellarFramework/Editor/StellarToolsHub/Modules/AssetBundleToolModule.cs`（`EnsureDefaultSampleAsset` / `EnsureDefaultSampleAssetMaterial`）
- **问题**: 点击"初始化 AB"会用默认胶囊体覆盖 `TestCapsule_AB.prefab` 并改写材质，无备份、无二次确认 → **用户数据丢失**。
- **修法**: 已存在时跳过或弹确认框 + 备份。

### 12. AssetBundle 构建在 OnGUI 中同步执行 BuildPipeline
- **位置**: `AssetBundleToolModule.cs`（`BuildBundles`）
- **问题**: 大型项目构建数分钟，编辑器 UI 完全冻结（QuickStart 已修复此模式，AB 工具未跟进）。
- **修法**: 参照 QuickStart 的 delayCall/异步化。

### 13. AssetMap 生成代码用路径而非 GUID
- **位置**: `AssetBundleToolModule.cs`（`GenerateCode`）+ `Assets/StellarFramework/Generated/AssetMap/`
- **问题**: 资源移动/重命名后映射变死链，运行时加载静默失败（白屏/缺图），无过期通知。
- **修法**: 存 GUID，或加 AssetPostprocessor 自动重生成。

### 14. LogKit.LogError 无 `[Conditional]` 开关 → Release 包持续输出
- **位置**: `Assets/StellarFramework/Runtime/Kits/LogKit/LogKit.cs`
- **问题**: Log/LogWarning 可被 `ENABLE_LOG` 宏裁剪，LogError 不能。框架内部把 LogError 当"非致命错误返回路径"大量使用，替换 ILogger 后 Release 仍持续产生输出。
- **修法**: 明确 LogError 语义，或提供开关。

### 15. asmdef 存在悬空 GUID 引用
- **位置**: `Assets/StellarFramework/StellarFramework.asmdef`（`GUID:f51ebe6a...`、`GUID:5c01796d...`），其中 `f51ebe6a` 被 10+ 个 asmdef 引用
- **问题**: 全项目找不到对应 .meta，可能是已删除程序集残留 → CI 构建时编译炸雷。
- **修法**: 全量核对 33 个 asmdef 的 references，清理无效 GUID。

---

## 二、🟡 坑：特定场景必然触发

### 资源链路
- **AddressableLoader 同步加载静默返回 null**（`Reskit/Loaders/AddressableLoader/AddressableLoader.cs`）：Editor 下 Use Asset Database 可能"碰巧能跑"，真机 Packed Mode 必挂；业务不判空 → NRE。
- **DownloadDependenciesAsync 完成后释放下载 Handle**（`AddressableHotUpdateManager.cs`）：下次加载重新下载，热更缓存失效、流量暴涨。
- **AssetBundleManager.GetBundlePath 强制 ToLowerInvariant**（`AssetBundleManager.cs`）：Android/iOS 大小写敏感文件系统下，带大写 Bundle 名找不到文件。
- **UpdateCatalogs 的 Handle 被释放**（`AddressableHotUpdateManager.cs`）：catalog 更新可能回滚。
- **ResKit.Allocate<T>() 泛型绕过配置后端**（`ResKit.cs`）：与 `Configure(Addressables)` 混用时代码加载路径不一致。
- **ResLoader 同步/异步并发冲突静默返回 null**（`ResLoader.cs`）。
- **AssetBundleManager.InitSync 阻塞主线程**（启动卡顿）。
- **AddressableLoader 取消时序产生孤儿资源**（`autoReleaseWhenCanceled:false` + catch 释放，取消瞬间资产已加载但失引用）。

### 热更链路
- **SHA256 开发模式静默跳过、无警告**（`HybridCLRHook.cs`）：开发期不配置 SHA256 也能"成功"，上线后严格模式直接阻断。
- **Settings 默认 AOT metadata 只有 3 个，Manifest 有 4 个**（缺 `UnityEngine.CoreModule.dll.bytes`）：Resources 兜底路径加载时热更代码调 Unity API 崩溃。
- **Manifest assemblyKey 是完整路径 vs Settings 默认是文件名**：Addressables entry address 不匹配时静默加载失败。
- **`Assembly.Load(byte[])` 无防御性复制**（重构风险）。
- **双份 Manifest**（`GameHotUpdate/Manifest/` 与 `StreamingAssets/aa/`）易不同步。
- **Manifest 写入硬编码 `Assets/StreamingAssets/aa/`**：用户自定义 AA 输出目录时不匹配。

### UI / 事件 / 绑定
- **异步 Open 期间 Close 被吞**（`UIKit.cs` OpenPanelInternalAsync）：Close 先于面板创建 → 加载完成后面板仍被打开。
- **OnPanelClosedGlobal 级联 ClosePanel 递归**（订阅者回调中关闭同类型面板 → 栈溢出）。
- **Push 全屏面板先显示后隐藏下层**（同帧闪烁窗口，可能点到下层）。
- **PopTo 找不到目标时已关闭面板无法恢复**。
- **BindableProperty 递归通知被静默丢弃**（回调内修改同一属性，UI 与数据不一致）。
- **BindableList/Dictionary 静默拒绝修改**（调用方以为已修改）。
- **EventUnregisterOnDisableTrigger 清空所有注册**（面板复用时忘记重注册则事件永久丢失）。
- **GlobalEnumEvent 静态字典强引用已销毁对象**（漏注销即内存泄漏）。
- **RegisterWithInitValue 立即回调**（回调中主动注销，返回的 IUnRegister 已失效）。
- **UIKit 面板 Root 用 `transform.Find("root")` 字符串硬编码**。

### 其余 Kit
- **ConfigKit persistentDataPath 优先于包内配置、无版本控制**：热更删除配置后旧用户 persistent 残留旧配置，永远盖过新包。
- **FSM OnExit/OnEnter 抛异常后状态机处于半切换状态**（已切换但回调未完成）。
- **ActionKit Sequence(null) 返回 null 后链式调用 NRE**。
- **AudioKit 未初始化时 PlaySound 静默丢弃**（有 LogError，可接受，但业务无感知）。
- **HttpKit 静态单例 + DontDestroyOnLoad**：GetOrCreateInstance 与 Awake 双保险，设计 OK；但 `CancelAllRequests` 在退出时使业务 await 抛 OperationCanceledException（需调用方兜底）。

### 编辑器工具
- **DictionarySerializerWindow 用 `cacheKey.Contains(field.Name)` 脆弱匹配**：字段名包含关系（`myDict` vs `myDictExtra`）导致写错数据。
- **AB 规则存 EditorPrefs**（跨项目冲突，应存 ScriptableObject）。
- **HybridCLR 导出器频繁 AssetDatabase.Refresh**（大项目卡顿）。
- **测试共享临时目录**（`Assets/Temp/HybridCLRHotUpdateAssetExporterTests`，并行跑互删）。
- **测试用 File.ReadAllText 读源码做字符串断言**（路径过时即挂、假阳性、非功能测试）。
- **FrameworkValidation.Tests 依赖 Samples.Runtime**（纯净项目编译失败）。
- **URPMaterialConverter fallback `Shader.Find("Standard")`**（纯 URP 项目变洋红）。
- **PackagePublisher 无白名单，`GetAllAssetPaths` 全量遍历**（性能 + 误打包 Assets/GameHotUpdate 下敏感文件）。

---

## 三、🟠 隐患 / 🟢 建议（摘要）

- Architecture<T>.Interface 非线程安全（UniTask 多线程场景）。
- RegisterReadOnlyModelContracts 反射产生 GC（大规模 Model 时）。
- RenderPipelineCompatibility 字符串匹配判管线（自定义 SRP 误判）。
- SmartFind 的 Tag 查找在 Runtime 永远返回 false（UnityEditorInternalBridge 只在 Editor 有实现）。
- TransformExtensions.ClearChildren 延迟销毁导致 childCount 不立即归零（与 ClearChildrenImmediate 行为不一致）。
- GameEntry 的 `GameApp.Interface == null` 是死代码（getter 永不返回 null）。
- SingletonFactory 纯 C# 单例的销毁检查无意义 + 多线程竞态窗口。
- MonoSingleton 依赖子类调用 base.Awake()（隐式契约）。
- Addressables Non-Recursive Building 开启：依赖链管理不当会 Missing Asset。
- `m_BuildAddressablesWithPlayerBuild: 0`：CI 直接打 Player 会缺 AA 数据。
- EventKit CallbackKey 的 delegate hash 对 lambda 无效 → lambda 重复注册检测失效。
- AudioKit：`[RequireComponent(AudioListener)]` 全局单例有多个监听器风险（其他场景有 AudioListener 时警告）。
- SettingsKit 全量 Save + Flush（高频调用时性能）。

---

## 四、最危险的 5 个问题（修复优先级）

1. **EventKit Token 池化 use-after-free** — 幽灵 bug，触发即事件静默丢失，极难排查。
2. **AOTGenericReferences 为空 + HybridCLR 版本未锁定** — 热更代码用泛型必崩，且构建不可复现。
3. **Remote Catalog 未开启 + D:/HotUpdate/ 硬编码** — 资源热更整体失效，跨平台不可用。
4. **UIKit OnClose 后访问已销毁对象** — 面板关闭路径确定性崩溃风险。
5. **AssetBundle 工具覆盖用户 Prefab + AssetMap 用路径** — 编辑器数据丢失 + 运行时静默失败。

---

## 五、建议修复顺序

| 优先级 | 内容 |
|---|---|
| P0 | EventKit Token 池化修复；UIKit OnClose 空检查；AOTGenericReferences 重新生成 |
| P0 | manifest.json 锁定 HybridCLR 版本；开启 Remote Catalog；远端路径可配置化 |
| P1 | HttpImageDownload 双缓存联动；Architecture Domain Reload 重置 + Init 预校验 |
| P1 | AssetBundle 工具防覆盖 + AssetMap 改 GUID + 构建移出 OnGUI |
| P1 | asmdef 悬空 GUID 清理；LogKit.LogError 开关 |
| P2 | ResKit 同步/异步冲突显式报错；Addressables Handle 释放策略修正 |
| P2 | 测试隔离性 + 移除对 Samples 依赖；AB 规则改 ScriptableObject |

> 说明：本报告为静态审查结论，部分"雷"（如 Remote Catalog、路径硬编码）已与工程实际配置逐一核对确认；修复后需配套 EditMode/PlayMode 回归。
