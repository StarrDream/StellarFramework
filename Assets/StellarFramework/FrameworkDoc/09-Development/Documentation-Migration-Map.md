# Documentation Migration Map / 文档迁移表

> P13 状态：**COMPLETE**。正式文档中心化、路径引用迁移与 legacy template cleanup 已完成。

## 中文

P13 文档迁移按受控批次完成：

1. LocalizationKit 样板。
2. KitCatalog / Distribution / Validation。
3. Architecture / Core / ToolsHub。
4. Foundation / Extension Kit Guides。
5. Adapter Guides。
6. Samples / Integrations / ArchitectureDemo。
7. World Framework P0-P13、开发状态与历史开发资料。

最终结果：

- 正式 Guide 全部迁入 `Assets/StellarFramework/FrameworkDoc`，并连同 `.meta` 保留 Unity GUID。
- `Assets/StellarFramework` 下 FrameworkDoc 外正式 Markdown 数量为 0（README / LICENSE / SOURCE 等允许就地文件除外）。
- QuickStart、KitCatalog、README 与验证测试中的硬编码文档路径均已切到 FrameworkDoc。
- World Framework 当前长期契约与 Release Matrix 保留在 `06-WorldFramework`；阶段 Implementation Plan、Development Status、Freeze/Review 资料已进一步归档到 `09-Development`。
- 旧 `KitSamples/Generated` 与 `SampleTemplates/*.unity.txt` 已在 code-first Builder 接管并完成引用扫描后物理删除。
- `Assets/docs/chatgptwebmemory.md` 按项目约定保留固定路径，只作为开发协作长期上下文，不属于对外 FrameworkDoc。

允许继续留在原位置：

- `README.md`：目录导航入口。
- `LICENSE*`：法律/分发文件。
- 字体 `SOURCE.md`：与字体资产绑定的来源证据。
- `Assets/docs/chatgptwebmemory.md`：固定长期上下文。

## English

P13 documentation migration was completed in controlled batches covering LocalizationKit, catalog/distribution/validation, architecture/core/ToolsHub, Kit and Adapter guides, Samples/Integrations/ArchitectureDemo, and World Framework P0-P13.

Final state:

- All formal guides live under `Assets/StellarFramework/FrameworkDoc`, with their `.meta` files moved to preserve Unity GUIDs.
- Formal Markdown outside FrameworkDoc under `Assets/StellarFramework` is zero apart from allowed local README / LICENSE / SOURCE files.
- QuickStart, KitCatalog, README, and validation-test path references now target FrameworkDoc.
- Current World Framework contracts and the release matrix live under `06-WorldFramework`; milestone plans, development status, freeze records, and reviews are archived under `09-Development`.
- Legacy `KitSamples/Generated` and `SampleTemplates/*.unity.txt` were physically removed after code-first Builders took ownership and reference scans were clean.
- `Assets/docs/chatgptwebmemory.md` intentionally remains at its fixed collaboration path and is not a public FrameworkDoc artifact.
