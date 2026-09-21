# StellarFramework 代码可读性与文档规范

本规范定义 StellarFramework 的“人类可读”交付标准。目标不是提高注释数量，而是让一个没有参与原始开发的 Unity/C# 开发者，在不依赖 AI、不阅读历史聊天、不猜测隐含约定的情况下，也能理解一个 Kit 的职责、边界、公开 API、关键实现和正确用法。

## 1. 总原则

代码首先通过清晰的命名、短职责、稳定目录和显式依赖表达意图；注释负责解释代码本身无法可靠表达的契约、原因、限制和设计取舍。

禁止把“注释全面”理解为逐行翻译代码。以下注释没有价值：

- `count++ // count 加一`；
- `return false // 返回 false`；
- 为满足注释率而批量生成的 `Gets X / Sets X`；
- 只对 AI 有意义、对使用者没有工程价值的提示；
- 已失效的阶段计划、临时 TODO 或历史实现说明长期留在 Runtime 源码中。

## 2. 公开 API 文档要求

Foundation / Extension / Adapter 的公开类型和公开成员属于框架契约。除极少数完全自解释且无额外语义的枚举值外，公开 API 必须使用 C# XML Documentation。

至少应覆盖：

- `public` / 面向扩展的 `protected` class、struct、interface、enum、delegate；
- public constructor、method、property、event；
- 公开的泛型参数、输入参数、返回值；
- 调用者必须知道的 exception、failure result、cancellation 与 lifetime 行为。

`<summary>` 应回答“它是什么 / 做什么”，而不是复述名称。需要时补充：

- `<param>`：单位、允许范围、是否可为空、buffer 所有权；
- `<returns>`：成功/失败语义、是否可能返回空值、是否部分写入；
- `<remarks>`：线程安全、分配、排序稳定性、坐标/边界规则、生命周期、调用顺序；
- `<exception>`：属于公开契约且调用者应处理的异常。

## 3. 即使不是 public，也必须解释的代码

下列 internal/private 实现如果复杂，仍必须有人类可读注释：

- 调度器、状态机、缓存、池、索引、序列化协调器；
- Heap、Hash、Graph、Pathfinding、Streaming、Migration 等算法核心；
- 两阶段提交、失败原子性、回滚、事务、版本迁移；
- 生命周期敏感代码（Awake/OnEnable/OnDisable/OnDestroy/Dispose）；
- 为降低 GC、避免全量扫描、保证稳定排序而采用的非直观实现；
- Unity / Addressables / HybridCLR / 平台 API 的已知约束或兼容处理。

优先在类型顶部写“职责 + 不变量”，在关键分支旁写“为什么必须这样做”。

## 4. 必须显式写清的工程语义

凡是存在以下语义，不能只靠实现细节让读者猜：

- **所有权**：谁创建、谁释放、谁持有 buffer / array / GameObject / handle；
- **可变性**：对象是否 immutable，返回集合是否允许修改；
- **线程安全**：默认线程、是否可并发、是否需要外部同步；
- **分配行为**：hot path 是否分配、调用方是否应复用 scratch/buffer；
- **单位与坐标**：tick、秒、米、cell、半开区间、轴向六边形坐标等；
- **失败语义**：异常、Result、Try 模式、是否保持 destination 不变；
- **原子性**：失败是否允许 partial write / partial registration；
- **排序与确定性**：tie-break、遍历顺序、seed、Stable ID；
- **生命周期**：注册/注销、取消、Dispose、Scene unload、Domain Reload；
- **依赖边界**：Core 与 Adapter 为什么分离，哪些依赖是可选能力。

## 5. 命名与文件组织

- 类型名表达领域概念，不使用 `Manager2`、`HelperEx`、`CommonUtil` 等模糊名称。
- `TryXxx` 必须具有可预测的失败语义，不能在常见失败路径偷偷抛异常。
- `Get` / `Find` / `Resolve` / `Create` / `Register` / `Attach` 等动词保持一致含义。
- 一个文件原则上承载一个主要类型；紧密耦合的小型私有类型可例外。
- Core、Adapter、Editor、Sample 的物理位置必须与依赖职责一致。

## 6. Sample 的人类可读标准

Sample 不只是自动测试夹具，还应是开发者可以学习的最小产品示例。

每个正式 Sample 至少应让读者能够找到：

1. 这个 Sample 展示哪个 Kit / Adapter；
2. 场景入口在哪里；
3. Play 后应该看到什么；
4. 哪几个按钮/交互对应哪条公开 API；
5. 最值得阅读的 1-3 个脚本；
6. 依赖哪些 Profile / UPM；
7. 哪些能力故意不在这个 Sample 中演示；
8. 中文和 English 的完整使用说明。

代码中的 Example 类应优先展示推荐用法，不应为了展示内部能力而绕开正式公开 API。

## 7. Guide 的最低内容

一个可独立分发的 Kit 至少需要让人类读者明确：

- Kit 的定位与“不负责什么”；
- 安装 / 导出依赖；
- 5 分钟最小示例；
- 关键公开类型与推荐入口；
- 生命周期与错误处理；
- 性能与 GC 注意事项；
- Adapter / 可选依赖；
- 常见错误与排查；
- 对应 Sample 和 Validation 证据。

源码文档与使用说明可以分开，但不能互相假设读者已经知道隐藏前提。

## 8. Review Gate

Kit 在声明“可交付 / 可独立使用”前，需要同时满足：

- **API Readability**：公开契约具有有意义的 XML 文档；
- **Implementation Readability**：复杂实现说明职责、不变量和关键 why；
- **Usage Readability**：Guide / README 能让新开发者独立跑通；
- **Sample Readability**：Sample 是教学入口，不只是测试场景；
- **No Comment Noise**：不存在大面积自动生成、重复代码字面含义的注释；
- **Freshness**：注释和文档描述与当前实现、Catalog、Sample 保持一致。

自动扫描只能用于发现候选缺口，不能以“注释覆盖率百分比”替代人工语义审查。

## 9. 当前整改策略

现有 Kit 采用渐进式整改，不为了快速清零扫描结果而批量生成无意义注释：

1. Foundation 的公开契约与复杂核心实现；
2. Extension 的公开契约；
3. Adapter 的边界、所有权和外部依赖语义；
4. Sample / Guide 人类可读性；
5. Editor / ToolsHub 的复杂工作流与危险操作说明。

每批整改后必须重新编译并运行对应 Kit 的行为/Policy 测试，避免“只改注释”时意外改变代码或生成资产。
