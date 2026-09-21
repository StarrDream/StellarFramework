# WorldGenKit 源码文档

> 状态：WorldGenKit.Core Stable

## Assembly Boundary

```text
StellarFramework.WorldGenKit.Core
references = []
noEngineReferences = true
```

Core 禁止直接依赖：

- UnityEngine / UnityEditor
- WorldKit
- GridKit
- SpatialKit
- PathKit
- SaveKit
- SimulationKit
- PlacementKit
- runtime reflection discovery
- LINQ/yield hot paths
- `Dictionary<string, object>` world samples
- `string.GetHashCode()` persistent seed
- Unity global Random

## 目录职责

```text
WorldGenKit/
├─ Identity/       Stable IDs
├─ Channels/       Channel schema / typed handles
├─ Storage/        storage capabilities + bindings
├─ Pipeline/       Stage / Compiler / Plan / Context
├─ Determinism/    stable seed/hash
├─ Rules/          typed contracts + math primitives
├─ Diagnostics/    compile/run diagnostics/report
└─ Internal/       stable ID utilities
```

## Stable IDs

以下 ID 使用同一 canonical 规则：

- `WorldDataChannelId`
- `WorldGenerationStageId`
- `WorldRuleId`
- `WorldRuleTagId`
- `WorldGenerationDiagnosticId`

规则：

```text
lower-case
dot-separated
[a-z0-9_]
max 128 chars
```

`GetHashCode()` 只服务当前进程容器，不作为持久世界 seed。

## Channel Registry

Builder 阶段持有：

```text
Stable ID
Storage Descriptor
Source Mode
Runtime Generic Type Token
Index
RegistryGeneration
```

Build 后 Registry immutable。

Type token 使用 generic static initialization + monotonic token source，不 assembly scan，不 reflection。

### default safety

`default(ChannelHandle<T>)` invalid。

`default(WorldChannelStorageDescriptor)` 也 invalid。

早期测试曾暴露枚举 zero-value 导致 default descriptor 看起来像 `Dense/World` 的风险，现通过显式 `_initialized` 标记修复，并作为 Regression 保留。

## Storage

### Dense

连续 `T[]`：

```text
index access
Span<T>
ReadOnlySpan<T>
Fill
Clear
```

### Sparse

`Dictionary<int,T>`，有显式 default value。

适合“绝大多数样本等于默认，仅少量覆盖”。

### Chunked

`ChunkedChannelStorage<TKey,T>` 不知道 WorldKit ChunkCoord。

调用方/Adapter 自己提供 Key：

```text
Grid chunk key
WorldKit coord adapter key
server page key
custom region key
```

因此 WorldGenKit.Core 不依赖 WorldKit.Core。

### Constant

单个强类型值。

### Computed

`WorldComputedValue<TContext,T>` delegate，按需计算。

### External

持有 typed source reference；具体 query/import contract 由 Adapter 决定。

## WorldGenerationDataSet

DataSet 是编译后 Channel Index -> typed storage binding table。

内部只有一个 `object[]` 用于**粗粒度 storage instance binding**，不是 per-cell `object` 存储。

热 Stage 典型：

```text
Handle -> concrete Dense storage once
       -> Span/array inner loop
```

Storage kind 在 Bind 时与 Channel descriptor 校验。

## Stage Descriptor

`WorldGenerationStageDescriptorBuilder` 只允许一个 Channel 在同一个 Stage 的 Requires/Optional/Produces/Mutates 中出现一次。

理由：

- `Mutate` 已表达 read/write ownership；
- 不需要同一 Channel 同时 Require + Mutate；
- 避免 Descriptor 出现冲突含义。

SeedScope 必须显式声明：

```text
World
Region
Chunk
RunKey
```

default None 不是合法 compiled stage。

## Compiler Writer Contract

Core：

```text
Channel -> max 1 Producer
Channel -> max 1 Mutator
```

依赖边：

```text
Producer -> Mutator -> Consumer
ProvidedInput -> Mutator -> Consumer
Producer -> Consumer
```

Optional input 不存在 writer 时不产生 missing-producer error。

Required input 不存在 writer 且不是 ProvidedInput 时 compile fail。

### Stable topology order

Kahn-style topological sort。

多个 indegree=0 Stage 时按原 Builder 索引选择最小值；但有真实依赖边时依赖优先于 add order。

PlanHash 使用编译后的 Stage 顺序和 Stable IDs，因此等价 dependency chain 不依赖临时 RegistryGeneration。

## Execution Preflight

`WorldGenerationPlan.Execute` 在执行 Stage 前检查：

1. destination 容量；
2. RegistryGeneration；
3. Required / Produced / Mutated storage bindings。

失败不会执行 partial stage。

纯 Optional Channel 不强制绑定。

Stage 返回 Failed/Cancelled 时立即停止，不吞异常，不执行后续 Stage。

Programming exception 会向上传播。

## Determinism

Stable hash 明确编码：

- seed as UInt64 bytes；
- signed logical X/Y 使用 unchecked UInt64 bit pattern；
- stable ID UTF-16 code unit bytes with length；
- local key；
- final avalanche。

不能修改算法而不考虑 GeneratorVersion / 世界兼容性。

回归向量：

```text
123456789, -42, 77, stage.height, 999
=> 56D9FA3612E0585D
```

## Rule Layer

Rule Contract 泛型化：

```text
IWorldEligibilityRule<TContext>
IWorldScoreRule<TContext>
```

Built-in rule primitives 不读取 string channel ID。

`WorldRuleMath` 使用 caller-owned Span：

- WeightedSum
- Multiply
- Min/Max
- And/Or

所有 double 输入拒绝 NaN/Infinity，避免坏数据静默污染整个 pipeline。

`WorldCurve` 构造时复制并校验点，运行时二分定位并线性插值。

`WorldNoiseRule` 使用 stable hash，不访问全局 RNG。

## GenerationReport

Report 是可选 alloc-heavy diagnostics snapshot，不属于默认热路径。

Plan Execute 仍由调用方提供：

```text
Span<WorldGenerationStageExecutionRecord>
```

需要存档/日志/ToolsHub 检查时再 `Capture`。

## Compiled Noise Hot Path

`WorldNoiseKey` 是 additive 性能 API：

```csharp
WorldNoiseKey key = WorldNoiseRule.Compile(ruleId);
double sample = WorldNoiseRule.Sample01(seed, x, y, key, localKey);
```

它预编译 Stable Rule identity，compiled-key overload 使用固定 64-bit mixing path，避免 octave/corner 热循环重复处理 Stable ID 字符串。

旧 `WorldNoiseRule.Sample01(..., WorldRuleId, ...)` 保持原有算法和确定性结果；固定 seed regression vector 也保持不变。`WorldGenKit.Builtins` 的 Fractal Noise 使用 compiled key 路径。

## Validation Contract

Behavior：

- Channel/ID/default safety；
- cross-registry handles；
- all six storage kinds；
- pipeline dependency compile；
- failure diagnostics；
- optional binding；
- stable seed vector；
- custom `game.magic_density` without Temperature；
- stable PlanHash；
- GenerationReport；
- rule math and bad-number rejection。

Policy：

- zero asmdef references；
- no Unity；
- no WorldKit or existing Kit dependency；
- no reflection scan；
- no LINQ/yield hot path；
- no per-sample string/object bag；
- no unstable seed API。

Performance baseline：

- 1M Dense；
- 100k Sparse；
- 100k Chunked；
- 1M handle resolve；
- 100k 2-stage plan runs。

Core 不需要 PlayMode；Unity presentation/import adapters 在各自边界验证。
