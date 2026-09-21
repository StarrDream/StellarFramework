# WorldGenKit 世界生成系统说明

WorldGenKit.Core 是一个**纯 C#、零 Kit 依赖、零 UnityEngine 依赖**的世界数据生成与 Authoring Pipeline 基础 Kit。

它解决的是：

> “如何把任意强类型 Channel、Stage、Rule 和数据来源编译成一条确定、可验证、可重复执行的世界数据生成管线。”

它不是：

- 固定 Height/Biome/Temperature 生成器；
- WorldKit 的附属模块；
- Terrain / Mesh / Tilemap 渲染器；
- Resource/Feature/Placement 的业务大杂烩。

WorldGenKit.Core 可以单独导出，用于：

- 2D 地图数据生成；
- 3D HeightField 逻辑数据生成；
- RTS / 模拟经营 / 生存 / 沙盒地图；
- 自定义离线工具；
- Server 世界数据构建；
- 导入数据后的二次派生 Pipeline；
- 任意项目自定义 Channel/Stage/Rule。

---

## 1. 核心模型

```text
Channel Registry
    ↓
Stage Descriptors
    ↓
Pipeline Compile
    ↓
Immutable WorldGenerationPlan
    ↓
Typed Storage Bindings
    ↓
Execute(seed, runKey)
    ↓
Stage Execution Records / optional GenerationReport
```

## 2. Channel：世界属性不写死

项目可以注册：

```text
terrain.height
terrain.moisture
terrain.temperature
soil.fertility
environment.pollution
game.magic_density
game.corruption
strategy.value
```

Core 不存在：

```csharp
class WorldCell
{
    float Height;
    float Temperature;
    float Moisture;
}
```

注册示例：

```csharp
var pipeline = new WorldGenerationPipelineBuilder();

ChannelHandle<float> height = pipeline.Channels.Register<float>(
    WorldDataChannelId.From("terrain.height"),
    new WorldChannelStorageDescriptor(
        WorldChannelStorageKind.Dense,
        WorldChannelScope.Sample));

ChannelHandle<float> magic = pipeline.Channels.Register<float>(
    WorldDataChannelId.From("game.magic_density"),
    new WorldChannelStorageDescriptor(
        WorldChannelStorageKind.Dense,
        WorldChannelScope.Sample));
```

稳定字符串 ID 只在注册 / 解析边界使用。运行时热路径使用：

```text
ChannelHandle<T>
  Index
  RegistryGeneration
```

不同 Registry 即使 Index 相同也不能串用。

---

## 3. Storage

Core 正式识别六种 Storage：

| Kind | 用途 |
| --- | --- |
| Dense | 大多数 Sample 都有值 |
| Sparse | 少量非默认值 |
| Chunked | 按项目 Key 分页 / 分块 |
| Constant | 整个 Domain 单值 |
| Computed | 按需计算 |
| External | 由导入器 / 项目外部来源提供 |

当前提供的强类型实现：

```text
DenseChannelStorage<T>
SparseChannelStorage<T>
ChunkedChannelStorage<TKey,T>
ConstantChannelStorage<T>
ComputedChannelStorage<TContext,T>
ExternalChannelStorage<TSource,T>
```

### 为什么没有强迫一个巨大的虚接口

`IWorldChannelStorage<T>` 只作为**绑定能力标记**。

Stage 获取具体 Storage 后直接使用具体类型：

```csharp
DenseChannelStorage<float> heightData =
    context.Data.GetStorage<float, DenseChannelStorage<float>>(height);

Span<float> values = heightData.AsSpan();
```

百万 Sample 热循环不会每个元素走虚接口、字符串或 `object`。

---

## 4. ProvidedInput 与 ProducedByStage

Channel 来源是显式的：

```text
ProducedByStage
ProvidedInput
```

例如高度图导入：

```csharp
ChannelHandle<float> importedHeight = pipeline.Channels.Register<float>(
    WorldDataChannelId.From("terrain.height"),
    new WorldChannelStorageDescriptor(
        WorldChannelStorageKind.External,
        WorldChannelScope.Sample),
    WorldChannelSourceMode.ProvidedInput);
```

这样 Pipeline 可以从“已有 Height”开始，只生成：

```text
Slope
Water
Biome
Resources
...
```

不要求每张地图都从 Noise 开始。

---

## 5. Stage Contract

Stage 显式声明：

```text
Requires
Optional
Produces
Mutates
SeedScope
```

```csharp
public sealed class SlopeStage : IWorldGenerationStage
{
    public WorldGenerationStageId Id =>
        WorldGenerationStageId.From("stage.slope");

    private readonly ChannelHandle<float> _height;
    private readonly ChannelHandle<float> _slope;

    public void Describe(WorldGenerationStageDescriptorBuilder builder)
    {
        builder.SetSeedScope(WorldGenerationSeedScope.Chunk);
        builder.Require(_height);
        builder.Produce(_slope);
    }

    public WorldGenerationStageResult Execute(
        in WorldGenerationContext context)
    {
        // typed storage access
        return WorldGenerationStageResult.Succeeded();
    }
}
```

Stage 不通过 runtime reflection 自动扫描。项目明确注册：

```csharp
pipeline.AddStage(new HeightStage(...));
pipeline.AddStage(new SlopeStage(...));
pipeline.AddStage(new BiomeStage(...));
```

---

## 6. Pipeline Compiler

`Compile()` 会生成不可变 `WorldGenerationPlan`。

编译阶段检测：

- invalid Stage ID；
- duplicate Stage ID；
- invalid / foreign Channel Handle；
- duplicate Channel reference；
- missing SeedScope；
- missing required producer；
- duplicate producer；
- duplicate mutator；
- ProvidedInput 被非法 Produce；
- self dependency；
- dependency cycle。

### Writer 规则

Core 冻结：

```text
每个 Channel 最多一个 Producer
每个 Channel 最多一个 Mutator
```

顺序：

```text
ProvidedInput / Producer
        ↓
optional Mutator
        ↓
Consumers
```

不允许靠“Stage 添加顺序”猜谁先写。

---

## 7. Optional Channel

`Optional(handle)` 的意思是：

> 这个 Stage 可以使用该 Channel，但没有它仍然可以执行。

因此：

- 纯 Optional Channel 可以不绑定 Storage；
- Required / Produced / Mutated Channel 必须绑定；
- Stage 内使用 `TryGetStorage` 判断 Optional 是否存在。

---

## 8. Deterministic Seed

WorldGenKit 使用框架自有稳定 64-bit Hash。

禁止：

- `string.GetHashCode()`；
- Unity global Random；
- 依赖 Chunk 生成先后顺序的全局递增 RNG。

概念：

```text
WorldSeed
+ Logical Run Key
+ Stage Stable ID
+ SeedScope
= StageSeed
```

同样的输入必须跨 Session 得到同样结果。

框架回归向量：

```text
seed=123456789
x=-42
y=77
stage=stage.height
localKey=999

=> 0x56D9FA3612E0585D
```

Chunk A 先生成还是 Chunk B 先生成，不得改变结果。

---

## 9. Rule Contract

Core 不使用：

```csharp
object Evaluate(object context)
```

自定义规则可以实现：

```text
IWorldEligibilityRule<TContext>
IWorldScoreRule<TContext>
```

内置低层规则原语：

- Range
- Threshold
- piecewise-linear Curve
- Inverse
- deterministic Noise
- Distance
- stable Tag Set
- WeightedSum
- Multiply
- Min / Max
- AND / OR

这些是纯数学 / 语义原语，不把 Biome、Forest、Iron 等业务概念写进 Core。

例如：

```csharp
var magicRule = new WorldThresholdRule(
    0.5,
    WorldThresholdComparison.GreaterOrEqual);

bool magical = magicRule.Evaluate(magicDensity);
```

测试已验证 `game.magic_density` 可以直接驱动生成规则，同时 Registry 中完全不存在 `terrain.temperature`。

---

## 10. GenerationReport

热路径执行：

```csharp
Span<WorldGenerationStageExecutionRecord> records = ...;

WorldGenerationRunResult result = plan.Execute(
    data,
    worldSeed,
    runKey,
    records);
```

默认不强制创建 Report 对象。

只有需要长期诊断快照时：

```csharp
WorldGenerationReport report = WorldGenerationReport.Capture(
    plan,
    worldSeed,
    runKey,
    result,
    records);
```

Report 保存：

- PlanHash；
- WorldSeed；
- RunKey；
- Run Status；
- Terminal Diagnostic；
- 实际执行 Stage Records。

---

## 11. PlanHash

PlanHash 基于：

- Stage Stable ID；
- SeedScope；
- Requires / Optional / Produces / Mutates 的 Stable Channel ID；
- 编译后的确定 Stage 顺序。

不使用 RegistryGeneration，也不直接 hash 临时 numeric Channel Index。

因此等价依赖图即使 Stage 注册顺序不同，只要编译后的语义计划一致，PlanHash 保持一致。

---

## 12. 独立使用

WorldGenKit.Core **不依赖 WorldKit**。

可以这样：

```text
WorldGenKit.Core
   ↓
Generate custom data
   ↓
export JSON / binary / texture / own DTO
```

也可以：

```text
WorldGenKit.Core
   ↓
WorldGenKit.WorldKitAdapter
   ↓
WorldKit.Core
```

WorldKit 与 WorldGenKit 是横向独立 Kit，通过 Adapter 组合。

---

## 13. 当前 Core 边界

WorldGenKit.Core 当前负责：

- Channel identity / typed handle；
- Storage capability；
- Pipeline stage contract；
- Pipeline compile/plan；
- deterministic seed；
- low-level Rule primitives；
- diagnostics / GenerationReport。

当前**不负责**：

- 内置 Terrain/Biome/Water Stage；
- Resource Scatter；
- Feature/POI；
- Placement；
- Unity Terrain/Mesh/Tilemap；
- WorldKit streaming；
- SaveKit persistence。

这些属于 Builtin / Extension / Adapter。

---

## Compiled Noise Key

高频 Noise Stage 可以先调用：

```csharp
WorldNoiseKey key = WorldNoiseRule.Compile(ruleId);
```

再使用 compiled-key `Sample01` overload。这样 Stable ID 只在编译 key 时处理一次，适合 Fractal Noise 等高频采样场景。

旧 `WorldRuleId` overload 的确定性语义保持不变。需要现成 Height/Moisture/Water/Slope/Biome/Surface/Buildable 数据链时，使用独立 `WorldGenKit.Builtins` Profile；Core 本身仍不硬编码这些地形语义。

## 14. 性能基线

Unity `2022.3.62f3c1` Editor Test Runner，本机趋势：

```text
1,000,000 Dense writes          3.013 ms
250,000 sampled Dense reads     0.485 ms
100,000 Sparse writes           0.768 ms
100,000 Sparse reads            0.748 ms
100,000 Chunked writes          2.590 ms
100,000 Chunked reads           2.497 ms
1,000,000 typed handle resolve 17.523 ms
100,000 two-stage plan runs    58.870 ms
coarse heap delta                   0
```

这些数值是本机趋势，不是跨平台固定门槛；`GC.GetTotalMemory(false)` 也不是严格零分配证明。
