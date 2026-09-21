# P13 Completion Plan / P13 完成计划

> 当前状态：A-F 已 **COMPLETE / PASS**。P13 已封板；后续新增需求进入新的独立里程碑，不再继续扩展 P13。

## 中文

P13 的目标是把 P0-P12 已冻结的框架能力整理成可独立导出、可直接运行、视觉可验证、文档可维护的正式产品形态。

### A. LocalizationKit Finalization

- Core：零依赖、engine-free、显式 Locale/Catalog/Fallback、命名参数格式化。
- Adapter：SettingsKit 与 Unity UGUI 单向组合。
- Editor：coverage、duplicate/missing/empty、fallback、模板语法与跨语言 placeholder contract。
- Sample：固定 `中文` / `English` 语言按钮；点击后整个可本地化 UI 切换对应语言。
- 字体：项目内 Source Han Sans CN Regular + SIL OFL 1.1 + SHA256。

### B. Example Productization

- F1：Action / Bindable / Event / Singleton / Config / Log。
- F2：Time / Save / Pool / Res / Audio / FSM。
- F3：Grid / Spatial / Simulation / Path / Path.GridAdapter。
- F4：Settings / UIKit / Http / HotUpdate / Flow。

每个非 UI Kit 必须以 2D/3D 对象、路径、网格、材质、移动、音频等作为主要验证证据；UGUI 只承担说明、控制和摘要。

### C. Integrations / Showcase

- 6 个 World Framework Integration 全部统一 P13 视觉与双语规范。
- FlowKit + MSV Integration 保持独立集成示例。
- ArchitectureDemo 完成统一整理。

### D. Documentation Centralization

- **COMPLETE**：`FrameworkDoc` 已成为正式文档唯一维护中心。
- **COMPLETE**：正式 Guide、Samples/Validation、World Framework 与历史开发资料已迁移并保留 `.meta` GUID。
- **COMPLETE**：Runtime / Editor / Samples / Tests 不再保留正式 Guide；FrameworkDoc 外正式 Markdown 为 0（README / LICENSE / SOURCE 除外）。
- `Assets/docs/chatgptwebmemory.md` 按项目约定保留固定路径，仅作为开发协作长期上下文。

### E. Cleanup

- **COMPLETE**：31/31 entrypoint 已由 code-first Builder 接管。
- **COMPLETE**：旧 `KitSamples/Generated` 与 `SampleTemplates/*.unity.txt` 已在引用迁移后物理删除。
- **COMPLETE**：删除后 Scene / Packaging / SampleGeneration / Runtime PlayMode Gate 已重新验证。

### F. Final Seal

- 31 个 Manifest entrypoint。
- Missing Script / broken reference = 0。
- Localization coverage + placeholder contract PASS。
- Builder GUID 幂等。
- PlayMode smoke。
- Boundary / Metadata / Standalone。
- P0-P12 frozen regression。
- Console 0 unexpected error/warning。
- `git diff --check` = 0。

**最终结论：P13 = COMPLETE / PASS。**

最终封板证据：

- Sample Manifest：**31/31 ready**。
- Distribution Catalog：**98 profiles / 0 missing requiredProfileIds**。
- FrameworkDoc 已成为正式文档唯一维护中心；FrameworkDoc 外正式 Markdown = **0**（README / LICENSE / SOURCE 与固定协作记忆除外）。
- legacy `KitSamples/Generated` 与 `SampleTemplates/*.unity.txt` 已物理删除，当前路径均不存在。
- F1 / F2 / F3 / F4、6 个 World Framework integration、ArchitectureDemo 的 Scene / PlayMode / Manifest / Metadata / Standalone / Packaging 等分批 Gate 均已有 PASS 证据。
- P0-P12 保持既有 frozen regression 证据，不因 P13 产品化修改重新开放 Core 语义。
- ResKit 同路径并发等待补齐 cancellation 语义，专项回归 **1/1 PASS**。
- Addressables / HotUpdate 最终修复：Editor Fast Mode 下不再执行 Unity 官方不支持的物理 `DownloadDependenciesAsync` 路径，而是明确返回 0-byte no-download；Player / Packed Mode 行为不变。Fast Mode Prefab、HotUpdate DLL、ResKit loader、HotUpdateManager no-op preservation、Prefab integration 均已独立 **1/1 PASS**；AOT metadata 的 AssetDatabase 导入与 Addressables address/label contract 也已验证。
- 完整 AOT metadata runtime load 继续作为 Packed / Player 显式 integration Gate，不用 Fast Mode 伪装验证正式发布路径。
- 最新源码通过 Unity 现有 Bee/Roslyn response 编译：ResKit、HotUpdate Addressables、Addressables validation assembly 均 **exit 0**。
- repository static seal：Manifest / Catalog / legacy tree / FrameworkDoc path / stale-link 均闭合；`git diff --check` 在最终 whitespace normalization 后要求保持 **0**。

环境说明（不属于 StellarFramework release failure）：

- 项目启用了 `com.besty.unity-skills` 的 testable package。Unity 环境级全量 Test Runner 会把该插件自身的测试也纳入统计；曾出现 5 条 `UnitySkills.Tests.Core.ReviewFixRouterTests` 失败，这不是 StellarFramework 测试。
- GUI 重启期间机器偶发出现 `Sentinel LDK Protection System` 外部授权弹窗，会阻断 UnitySkills 8090 启动；该问题不来自 StellarFramework Runtime / Editor 源码。
- 因此 P13 Final Seal 以 **framework-owned release gates + 独立 Addressables integration gates + static/distribution seal** 为准，而不是要求同一 Unity 进程内所有第三方 package tests 全绿。

## English

P13 turns the frozen P0-P12 framework capabilities into a productized form that is independently exportable, directly runnable, visually verifiable, and maintainable.

### A. LocalizationKit Finalization

- Core stays zero-dependency and engine-free with explicit Locale/Catalog/Fallback and named formatting.
- SettingsKit and Unity UGUI remain one-way Adapters.
- Editor validation covers coverage, duplicate/missing/empty values, fallback configuration, template syntax, and cross-locale placeholder contracts.
- Sample language selectors are permanently labeled `中文` and `English`; pressing either switches all localizable UI to that language.
- Project-local Source Han Sans CN Regular is bundled with SIL OFL 1.1 and SHA256 verification.

### B. Example Productization

- F1: Action / Bindable / Event / Singleton / Config / Log.
- F2: Time / Save / Pool / Res / Audio / FSM.
- F3: Grid / Spatial / Simulation / Path / Path.GridAdapter.
- F4: Settings / UIKit / Http / HotUpdate / Flow.

Non-UI Kits must use real 2D/3D objects, paths, grids, materials, movement, audio, or equivalent scene evidence as the primary proof. UGUI is limited to explanation, controls, and summaries.

### C. Integrations / Showcase

- Productize all six World Framework integrations under the same visual and bilingual rules.
- Keep FlowKit + MSV as a dedicated integration example.
- Finish the ArchitectureDemo cleanup.

### D. Documentation Centralization

- **COMPLETE**: `FrameworkDoc` is now the single maintenance center for formal documentation.
- **COMPLETE**: formal guides, sample/validation docs, World Framework docs, and historical development records were moved with their `.meta` GUIDs preserved.
- **COMPLETE**: Runtime / Editor / Samples / Tests no longer contain formal guides; formal Markdown outside FrameworkDoc is zero apart from allowed local files.
- `Assets/docs/chatgptwebmemory.md` intentionally stays at its fixed project collaboration path.

### E. Cleanup

- **COMPLETE**: all 31 entrypoints are owned by code-first Builders.
- **COMPLETE**: legacy `KitSamples/Generated` and `SampleTemplates/*.unity.txt` were physically removed after reference migration.
- **COMPLETE**: post-removal scene, packaging, sample-generation, and runtime smoke gates were rerun.

### F. Final Seal

The final release gate covers 31 manifest entrypoints, missing references/scripts, localization contracts, Builder idempotence, PlayMode smoke, architecture/distribution policies, P0-P12 frozen regression, clean Console health, and `git diff --check`.

**Final result: P13 = COMPLETE / PASS.**

Release evidence:

- Sample Manifest: **31/31 ready**.
- Distribution Catalog: **98 profiles / 0 missing requiredProfileIds**.
- FrameworkDoc is the sole maintenance center for formal documentation; formal Markdown outside FrameworkDoc is **0** apart from allowed local README / LICENSE / SOURCE files and the fixed collaboration-memory path.
- Legacy `KitSamples/Generated` and `SampleTemplates/*.unity.txt` were physically removed.
- F1 / F2 / F3 / F4, all six World Framework integrations, and ArchitectureDemo have recorded PASS evidence for their required scene/runtime/distribution gates.
- Frozen P0-P12 Core semantics remain closed; P13 did not reopen them.
- The ResKit same-path pending-load cancellation gap was fixed and covered by a **1/1 PASS** regression test.
- Addressables / HotUpdate finalization explicitly treats Editor Fast Mode as no-download. Unity's own Addressables tests do not validate physical dependency download in Fast Mode, so the framework no longer runs that unsupported path there. Dedicated Fast Mode prefab, HotUpdate DLL, ResKit loader, HotUpdateManager no-op preservation, and prefab-integration gates each pass independently; AOT metadata import plus Addressables address/label contracts are also validated.
- Full runtime loading of all AOT metadata remains a Packed / Player explicit integration gate rather than being falsely represented by Fast Mode.
- Latest source compiles with the existing Unity Bee/Roslyn response inputs: ResKit, HotUpdate Addressables, and the Addressables validation assembly all return **exit 0**.
- Static release closure covers Manifest, Catalog, legacy-tree removal, FrameworkDoc path migration, stale-link removal, and repository whitespace validation.

Environment note:

- `com.besty.unity-skills` is configured as a testable package, so an environment-wide Unity Test Runner pass also executes the plugin's own tests. Five failures observed under `UnitySkills.Tests.Core.ReviewFixRouterTests` are outside StellarFramework.
- This machine can also show an external `Sentinel LDK Protection System` modal during Unity GUI startup, which can prevent the UnitySkills 8090 service from starting. This is not produced by StellarFramework source.
- P13 therefore seals against framework-owned release gates, dedicated integration gates, and static/distribution closure rather than requiring every third-party package test in the same Unity process to pass.
