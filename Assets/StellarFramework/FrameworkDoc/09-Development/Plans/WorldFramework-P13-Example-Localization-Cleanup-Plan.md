# World Framework P13 — Example Productization / Localization / Final Clean Seal

## 1. 目标

P13 是 World Framework 0→1 的最终产品化阶段。

它不是重新设计 Runtime Core，而是把整个 StellarFramework 的 Samples / Examples / Playable Scenes 整理成真正可学习、可运行、可验证、可维护、可独立导出的示例体系，并新增独立 LocalizationKit 统一处理中英文 UI。

P13 核心规则：

- GUI / UGUI 只承担 UI 职责：标题、说明、按钮、状态、语言切换。
- 除 UIKit / SettingsKit / LocalizationKit 等 UI 本身就是被测对象的 Kit 外，功能验证必须通过 2D / 3D 场景物体、动画、路线、颜色、生成/销毁、占用、网格、Terrain、音频源等实际表现体现。
- 不允许继续以大段 IMGUI 文本、Console Log 或数字标签作为非 UI Kit 的主要验证结果。
- 缺少 Example 美术资源时，由 Editor Builder 创建简单、统一、可重复生成的 Example 素材；不依赖外部商店素材。
- 所有用户可见 Sample UI 文本必须提供 zh-CN 与 en-US 两套文案。
- 现有 Runtime Kit 不允许为了 Examples 反向依赖 LocalizationKit、UGUI、Editor 或 Sample assembly。
- P0-P12 已冻结 Runtime contract 不因为 Example 整理被随意修改；若 Sample 暴露真实通用缺口，必须独立论证并优先通过 Adapter 解决。

## 2. 当前资产基线

P13 规划时审计到：

- 22 个 Kit Example 目录。
- 22 个 Kit Playable 场景。
- 5 个已生成 World Framework Integration 场景；P11 完成 TerrainGridNavigation 后会成为 6 个。
- 1 个 ArchitectureDemo Playable 场景。
- FlowKitMsvIntegration 当前有代码 Sample，但没有独立 Playable Scene。
- 当前总场景数为 28；P11 完成后至少 29。
- 当前不存在通用 Runtime LocalizationKit。
- SettingsKit 已存在 ILanguageSettingsAdapter，只负责语言设置入口，可以通过 Adapter 接 LocalizationKit。
- FlowKit Editor 有自己的 Editor-only 文案映射，不等同于 Runtime LocalizationKit。
- 当前 Assets 内没有可直接复用的 ttf / otf / ttc 字体资源。
- ExampleSceneGuide 目前使用 IMGUI 大段文字说明；P13 要把它降级为 UI 辅助职责，不再作为功能验证主体。

最终场景数量不以固定数字作为验收条件，而以 Sample Manifest / Catalog 中所有 active sample 均拥有明确 Playable/Verification 入口为准。LocalizationKit Example 与缺失的 FlowKitMsvIntegration Playable 可能会增加最终场景数量。

## 3. P13-A — LocalizationKit.Core

新增独立 engine-free Foundation Kit：LocalizationKit.Core。

建议核心类型：

- LocaleId：稳定语言 ID，例如 zh-CN / en-US。
- LocalizationKey：稳定文本 Key。
- LocalizationEntry：Key + Value。
- LocalizationTable：单 Locale 的不可变表。
- LocalizationCatalog：显式注册多个 Locale 表。
- LocalizationService：当前 Locale、默认 Locale、显式 fallback policy、查找与语言切换。
- LocalizationChangedEventArgs / event：语言切换通知。
- LocalizationLookupResult：Success / MissingLocale / MissingKey 等显式结果。

Core 约束：

- Core 不引用 UnityEngine / UnityEditor / SettingsKit / UIKit。
- Runtime 禁止 reflection / assembly scan 自动发现语言表。
- Locale / Table 通过显式注册。
- Missing key 不允许无声吞掉；TryGet 返回显式结果，Required API 可抛出明确异常。
- Fallback 必须配置化；默认可设置 current -> configured fallback -> failure，但每一步都可诊断。
- 不把每个 Text 组件维护成独立 Dictionary；Catalog/Table 在加载阶段构建索引，切语言只切当前表引用。
- Formatting 不使用反射对象绑定；只提供显式参数/命名值适配。
- 语言切换不强制依赖 SaveKit；持久化由 SettingsKit / 项目层负责。

Core Tests 必须覆盖：zh-CN/en-US lookup、locale switch event、missing locale/key、duplicate locale/key、explicit fallback、deterministic registration、format parameters、engine-free boundary、standalone export。

## 4. P13-B — Localization Adapters

### 4.1 LocalizationKit.UnityUGUIAdapter

职责：

- LocalizedText / LocalizedButtonLabel 等 UGUI 表现组件。
- ScriptableObject Localization Table authoring asset。
- Locale 切换时只更新已注册 View，不改变业务 Model。
- 不要求所有 Runtime Kit 引用 UGUI。

### 4.2 LocalizationKit.SettingsAdapter

实现现有 SettingsKit.ILanguageSettingsAdapter：

- GetLanguageOptions 从 LocalizationCatalog 暴露可用语言。
- GetCurrentLanguageValue 返回当前 LocaleId。
- ApplyLanguage 调 LocalizationService。
- SettingsKit 仍然不知道 LocalizationTable / LocalizationKey。
- LocalizationKit.Core 也不知道 SettingsKit。

### 4.3 LocalizationKit.Editor / Validator

Editor-only validation：

- zh-CN 与 en-US Key coverage 100%。
- duplicate Key。
- empty Value。
- missing fallback locale。
- unused Key / 场景引用不存在 Key 可作为 warning。
- 支持一次性输出 Coverage Report。

### 4.4 字体策略

LocalizationKit Core 保持 font-agnostic。

当前仓库没有 CJK 字体资产。P13 的默认 Example 字体正式采用 Adobe Source Han Sans / 思源黑体的简体中文版本：

- 来源必须是 Adobe 官方 source-han-sans release。
- 许可证为 SIL Open Font License 1.1；字体文件旁必须保留对应 LICENSE / copyright notice。
- 默认只引入实际使用的最少字重，优先 Regular；除非场景视觉确实需要，不打包整套 Pan-CJK 字重。
- Source Han Sans SC 同时覆盖简体中文与英文，因此默认不再额外引入一套英文字体，避免风格与 fallback 不一致。
- LocalizationKit Core 仍保持 font-agnostic；字体只属于 Unity/Sample presentation 层。
- ExampleFontProvider 优先使用项目内已打包的授权字体资产；只有在维护者开发模式下才允许显式 OS-font fallback，用于诊断而不是正式 Sample 依赖。
- 字体加载失败必须产生明确诊断并使 Sample localization gate 失败，不允许静默换字体。
- 如 Source Han Sans 在特定目标平台发生兼容问题，可切换到官方 Noto Sans CJK SC；同样要求固定官方来源与 SIL OFL 1.1 LICENSE。
- 项目使用者仍可通过 Unity Adapter 替换成自己的授权字体资产。

## 5. P13-C — Example Visual Standard

新增统一 Example Visual Contract。

UI 只允许承担：Example 名称、一句话目标、Controls、当前状态/PASS/FAIL 摘要、zh-CN/en-US 切换、Reset/Run/Rebuild 等真实操作按钮。

禁止：

- 用 IMGUI 大段文字模拟场景内容。
- 只打印路径长度而不在场景里画路径。
- 只打印对象池成功而不实际显示 spawn/recycle。
- 只打印网格可通行而不显示 2D/3D 网格状态。

非 UI Kit 的实际视觉证据必须至少包含一种：

- 2D Sprite / Tile / Grid Cell。
- 3D primitive / Mesh / Terrain。
- LineRenderer path / topology edge。
- 颜色 / 材质状态变化。
- 实际 spawn / despawn / pooling。
- transform movement / animation。
- occupancy / blocked cell visualization。
- AudioSource / emitter 可视对象。
- 网络/HTTP/配置等抽象 Kit 使用 2D/3D station / indicator / request object 表达生命周期；UI 只显示补充详情。

UI 类 Kit（UIKit / SettingsKit / LocalizationKit）允许 UI 本身作为主要被测对象，但必须真实走对应 Kit API，而不是静态 mock panel。

## 6. P13-D — Example Asset Factory

统一 Editor-only ExampleAssetFactory 生成缺失素材：

- Standard Materials：success / warning / blocked / active / inactive / path / resource。
- Procedural checker / grid / icon textures。
- Simple Sprite assets。
- Primitive prefab：cell / node / station / resource / obstacle / agent / projectile。
- LineRenderer material。
- 简单 AnimatorController。
- 必要测试 AudioClip 继续使用现有 Sample 生成链。

生成规则：

- 全部输出到明确 Generated 目录。
- 同输入可重复生成。
- 不覆盖用户手工定制资产；Generated 和 Authored 分离。
- Builder 重跑不能产生随机 GUID/重复垃圾资产。

## 7. P13-E — Sample 目录统一

目标结构：

Assets/StellarFramework/Samples/
  Common/
  Examples/
    <KitName>/
      Runtime/
      Editor/
      Art/Authored/
      Art/Generated/
      Scene/
      Localization/
      README.md
  Integration/
  Showcase/ArchitectureDemo/

迁移原则：

- 使用 GUID-preserving move，保留 .meta。
- 先移动、修引用、跑测试，再删除旧空目录。
- 中途不创建旧路径的永久兼容垃圾副本。
- Catalog sourcePaths / README / QuickStart / ToolsHub 构建入口同步更新。
- 旧 central Scenes 与 SampleTemplates 在所有新 Builder 验证通过后才能删除。

## 8. P13-F — 22 个 Kit Example 视觉重构批次

### Batch F1 — Core / State / Messaging

- ActionKit：3D action queue / command objects 顺序执行与取消。
- BindableKit：2D/3D 属性变化直接驱动物体位置/颜色/数值条。
- EventKit：多个 emitter / receiver 物体显式响应事件。
- SingletonKit：场景级/全局 service station 的注册与生命周期。
- ConfigKit：配置改变物体尺寸、速度、材质或布局；不是只显示 JSON。
- LogKit：场景对象触发不同严重级别事件，UI 只作为 log viewer。

### Batch F2 — Time / Data / Simulation / Navigation

- TimeKit：真实昼夜/钟表/周期事件物体。
- SaveKit：可交互 Save Station + 可恢复的场景对象状态。
- GridKit：2D/3D grid、occupancy、footprint、negative coordinate visualization。
- SpatialKit：移动实体 + query radius/rect + nearest 可视化。
- SimulationKit：批量 agents/plants 的 staggered simulation 可视化。
- PathKit：节点图 + A*/Dijkstra 路线 LineRenderer。
- PathKit.GridKitAdapter：blocked/cost grid + final path。

### Batch F3 — Runtime Services / Assets

- PoolKit：真实 projectile spawn/recycle。
- ResKit：Resources / AB / AA / RawText 对应真实 loaded object/station。
- AudioKit：可见 emitter + BGM/SFX 状态。
- HttpKit：request station / success-failure indicators；UI 显示 payload 摘要。
- HotUpdateKit：hot-update portal / state indicators；未配置真实产物时显示明确 unavailable 状态，不伪造成功。

### Batch F4 — Flow / FSM / UI / Settings

- FlowKit：节点驱动的真实场景阶段物体切换。
- FSMKit：角色/物体状态、动画、颜色/动作真实变化。
- UIKit：真实 Open/Push/Pop/Close UI flow。
- SettingsKit：设置真实改变 audio/graphics/input/language adapter state。
- LocalizationKit：zh-CN / en-US 切换并实时更新 UI。

## 9. P13-G — Integration / Showcase 场景统一

P11 六个 World Framework 场景全部纳入相同 UI / Localization / asset standard：Farm2D、HexStrategy、Survival3D、InfiniteFactory、StellarGridMap Migration、TerrainGridNavigation。

另外：

- ArchitectureDemo 改成同一 bilingual UI shell。
- FlowKitMsvIntegration 补独立 Playable Scene，不能只有代码 Sample。
- 所有 Integration Scene 的业务验证继续依赖实际 2D/3D world evidence。

## 10. P13-H — 清理

在全部新场景稳定后才执行：

- 删除废弃 ExampleSceneGuide IMGUI 主验证逻辑；如保留，只能作为极简 debug overlay，默认关闭。
- 删除已被新 Builder 取代的 SampleTemplates/*.unity.txt。
- 删除重复/无引用 Generated assets。
- 删除空目录、废弃 README、重复 Scene。
- 清理 Missing Script / Missing Material / broken prefab refs。
- 统一命名、scene root、camera、lighting、EventSystem、UI root。
- 清理 hard-coded 中英文用户可见文本，迁移到 Localization tables。
- 保留测试/文档需要的稳定 ID，不做无意义重命名。

## 11. P13-I — 自动化验收

必须新增：LocalizationKit Core tests、Unity adapter tests、Settings adapter tests、Localization coverage tests、Sample Catalog tests、scene missing-reference tests、non-UI visual evidence policy、builder idempotency、generated asset duplicate/orphan validation、全量 Scene PlayMode smoke、GUI policy、Runtime dependency boundary、Standalone export。

## 12. P13 最终封版门

P13 发生在 P12 Performance / Release Seal 之后，因此 P13 最后必须重新执行一次 release gates，不能沿用 P12 的旧绿灯。

最终必须满足：

- LocalizationKit 所有 profile 可独立导出。
- 所有 active Sample/Integration/Showcase 有统一结构和 README。
- 所有用户可见 Sample UI 有 zh-CN + en-US。
- zh-CN / en-US key coverage 100%。
- 所有 non-UI Kit 主要验证结果能从 2D/3D 场景直接观察。
- GUI 只承担 UI / controls / explanation / summary。
- 所有 Sample PlayMode smoke 0 error。
- Missing Script = 0。
- Missing Material / broken prefab reference = 0。
- Catalog missing dependencies = 0。
- Metadata / Standalone / asmdef boundary tests 全绿。
- P0-P12 frozen regression 全绿。
- Unity compile 0 error。
- Console 0 error / 0 warning（最终 seal 集合）。
- repository-wide git diff --check PASS。
- docs / catalog / sample index / quick start / memory 全同步。

完成这些后，才能标记：

P13 Example Productization / Localization / Final Clean Seal = FROZEN / PASS

并将整个 World Framework 0→1 主线标记为正式完成。
