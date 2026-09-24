# StellarFramework FrameworkDoc

## 中文

`FrameworkDoc` 是 StellarFramework 的正式文档中心。正式架构文档、Kit/Adapter Guide、ToolsHub 文档、Sample/Integration 说明、验证矩阵、World Framework 文档与历史开发资料统一维护在这里。

代码目录只保留必要的短 README 入口、LICENSE、字体来源或与资产必须共存的说明文件。旧场景模板体系已退出正式生成链。

### 目录

- `00-Overview/`：快速开始与总体索引。
- `01-Architecture/`：MSV、Kit 分层、代码可读性/注释规范、Runtime Extensions。
- `02-Kits/`：Foundation / Extension Kit、RuntimeTools 以及对应 Adapter 正式指南。
- `03-Samples/`：Example / Integration / Showcase、Visual Contract 与 Sample Index。
- `04-ToolsHub/`：ToolsHub、Packaging、Authoring。
- `05-Resources/`：Resources 目录与资源约定。
- `06-WorldFramework/`：World Framework 当前 Core Contract、Grid Contract 与 Performance / Release Matrix。
- `07-Distribution/`：Catalog、独立导出、依赖闭包。
- `08-Validation/`：测试、Benchmark、Kit Export Validation Matrix、Release Gate。
- `09-Development/`：迁移表、历史 Review、Handoff 与开发计划归档。

### 规则

1. 正式文档以 `FrameworkDoc` 为唯一维护源。
2. Runtime / Editor / Samples / Tests 目录中的 README 只做就地导航，不复制整份正式文档。
3. Sample README 必须同时包含完整 `## 中文` 与 `## English`。
4. LICENSE、字体来源等与资产绑定的文件继续留在资产旁边。
5. `Assets/docs/chatgptwebmemory.md` 是开发协作的固定长期上下文文件，按项目约定保留原路径，不属于对外 FrameworkDoc。
6. 新增正式文档不得再落回 Runtime / Editor / Samples / Tests 代码目录；阶段计划、Handoff 与历史 Review 统一归档到 `09-Development/`。

## English

`FrameworkDoc` is the canonical documentation center for StellarFramework. Formal architecture documents, Kit/Adapter guides, ToolsHub docs, Sample/Integration documentation, validation matrices, World Framework docs, and historical development records are maintained here.

Code directories keep only short local README entry points, licenses, font provenance, or documentation that must stay beside an asset. The legacy scene-template system is no longer part of the official generation path.

### Layout

- `00-Overview/` — quick start and the global index.
- `01-Architecture/` — MSV, Kit layering, code readability/documentation standards, and Runtime Extensions.
- `02-Kits/` — formal Foundation / Extension Kit guides, RuntimeTools, and their Adapter guides.
- `03-Samples/` — Example / Integration / Showcase docs, the visual contract, and sample index.
- `04-ToolsHub/` — ToolsHub, packaging, and authoring documentation.
- `05-Resources/` — Resources layout and resource conventions.
- `06-WorldFramework/` — current World Framework core contracts, grid contracts, and performance / release matrix.
- `07-Distribution/` — Catalog, standalone export, and dependency closure.
- `08-Validation/` — tests, benchmarks, Kit export validation, and release gates.
- `09-Development/` — migration maps, historical reviews, handoffs, and archived plans.

### Rules

1. `FrameworkDoc` is the single maintenance source for formal documentation.
2. README files under Runtime / Editor / Samples / Tests are short local navigation entry points and do not duplicate full formal guides.
3. Sample README files must contain complete `## 中文` and `## English` sections.
4. Licenses, font provenance, and asset-bound documentation stay beside their assets.
5. `Assets/docs/chatgptwebmemory.md` intentionally remains at its fixed collaboration path and is not a public FrameworkDoc artifact.
6. New formal documents must not be placed back under Runtime / Editor / Samples / Tests code folders; milestone plans, handoffs, and historical reviews belong under `09-Development/`.
