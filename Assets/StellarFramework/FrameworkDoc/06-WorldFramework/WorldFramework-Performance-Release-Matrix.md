# World Framework — Performance / Release Matrix

## 1. 范围

本矩阵不重新设计已经冻结的 Runtime contract。目标是给 World Framework 建立统一、可重复执行的性能趋势与 Release Gate。

当前统一测量环境：

- Unity 2022.3.62f3c1
- EditMode Test Runner
- 同一 StellarFramework 工作区
- 数值用于本机趋势，不承诺跨 CPU / Player / 平台的绝对毫秒
- GC.GetTotalMemory(false) 只表示 coarse heap trend
- 只有明确使用 GC.GetAllocatedBytesForCurrentThread() 且断言为 0 的条目，才记为 exact hot-path zero allocation

## 2. 十项性能矩阵

| Requirement | Workload | Current fresh evidence | Allocation evidence | Gate |
| --- | --- | --- | --- | --- |
| channel dense read/write | DenseChannelStorage 1,000,000 samples | write 4.269 ms; sampled read 0.517 ms | coarse delta 4,096 bytes belongs to the combined WorldGen benchmark, not this operation alone | PASS |
| sparse lookup | SparseChannelStorage 100,000 entries | write 2.196 ms; read 1.682 ms | same combined coarse trend as above | PASS |
| chunk generation | 64x64, 7-stage Builtins pipeline, 64 chunks per iteration, 5 measurements | isolated seal min 354.109 ms; median 356.938 ms, about 5.58 ms/chunk | exact hot-path allocated bytes = 0 after warmup with reused data/scratch | PASS |
| pipeline compile | 512x512 Builtins 7-stage plan | compile 1.218 ms | compilation is setup-time and is not claimed zero-allocation | PASS |
| neighbor/topology | 1,000,000 queries | Orthogonal4 102.694 ms; Orthogonal8 245.871 ms; Hex6 113.986 ms | coarse allocation delta = 0 | PASS |
| scatter candidate resolution | 512x512, 20,763 generated candidates, 9,527 accepted | generate min/median 26.399/27.207 ms; resolve 21.297/21.397 ms | coarse delta 8,192 bytes in trend benchmark; separate reusable hot-path test is exact 0 | PASS |
| feature candidate resolution | 4,096 deterministic feature candidates | min/median 53.673/54.904 ms | coarse delta 4,096 bytes shared with Placement benchmark; separate reusable hot-path test is exact 0 | PASS |
| streaming churn | radius 24, 2,401 resident target, 200 movement steps | final release batch min/median 107.202/108.216 ms; checksum 138,507,200 | exact hot-path allocated bytes = 0 | PASS / trend changed |
| save delta size | real WorldDeltaPersistenceState + typed codec + SaveKit UnityJson | 1k = 124,025 bytes; 10k = 1,262,517 bytes; 126.25 bytes/delta at 10k; 10x count growth = 10.180x | capture/serialization is persistence work and allocates by design; gate is approximately linear size growth | PASS |
| allocation / GC | reused Dense/Sparse + Resource Resolver + Feature Resolver, 128 iterations | isolated seal elapsed 7,306.539 ms; checksum 3,125,504 | exact allocated bytes = 0 | PASS |

## 3. Streaming trend note

An earlier historical release baseline recorded streaming churn near 41 ms median on the same Unity version. A later continuous benchmark batch observed 161.197 ms median; isolated and final release reruns stabilized around 108-113 ms median.

The current result is not described as “no regression”:

- resident target remains 2,401
- movement steps remain 200
- transition checksum remains 138,507,200
- final resident count remains 2,401
- exact hot-path allocation remains 0 bytes
- no integration-sample change modified WorldKit.Streaming Runtime semantics

The matrix therefore records 107.202/108.216 ms as the selected release-batch Editor baseline and preserves the older values as historical trend. Target-device release work should compare Player/IL2CPP measurements rather than treating Editor timing as a product SLA.

The additional gap benchmarks also showed large Editor timing variance during the same session: chunk generation median moved from roughly 131 ms to 357 ms, while exact hot-path allocation remained 0 in every measured gap run. The release gate therefore treats these milliseconds as trend evidence, not a fixed SLA; semantic checks, checksum, workload size and exact allocation assertions remain the hard automated conditions.

## 4. Memory budget interpretation

This matrix does not invent one universal MB budget for every game. The framework uses explicit storage shapes:

- Dense storage has exact element-count x element-size lower bounds.
- Constant storage has a fixed single-value lower bound.
- Sparse / Chunked / Computed / External storage is reported as variable instead of being assigned a fake exact byte count.
- WorldFramework.ToolsHub Memory Report already validates and reports these categories.
- Benchmarks use explicit sample/chunk/candidate counts so large-array costs are visible and reproducible.

Any project-specific hard memory ceiling belongs in the project Profile/Release target, not hidden in Core.

## 5. Architecture performance gates

The following rules remain executable through WorldFrameworkFoundationBoundaryTests:

- Runtime World Framework roots may not use reflection/assembly scanning for registration/discovery.
- Runtime hot data may not become Dictionary<string, object> or Dictionary<string, dynamic> per-cell bags.
- Grid/WorldGen/Placement Foundation contracts remain engine-free where frozen.
- Unity presentation/projection remains in explicit Adapter assemblies.
- Foundation cannot acquire Extension dependencies.
- Jobs/Burst is not injected into Core APIs merely to improve one benchmark; it remains a future optional adapter/implementation path after stable data layout.

WorldGenerationDataSet contains one coarse-grained object[] storage binding table keyed by typed handles. It binds channel storage instances once and is explicitly not a per-sample object bag.

## 6. Benchmark set

Fresh release benchmark execution:

- existing selected release benchmarks: 10/10 PASS
- additional gap benchmarks: 3/3 PASS
- total selected performance release set: 13/13 PASS, 0 failed, 0 skipped

The new gap benchmark class is:

Assets/StellarFramework/Tests/EditMode/FrameworkValidation/Performance/WorldFramework/WorldFrameworkReleaseBenchmarkTests.cs

It adds:

1. Chunk generation with exact allocation evidence.
2. SaveDelta serialized-size scaling.
3. Reusable hot-path exact allocation evidence for Dense/Sparse/Resource/Feature.

## 7. Release seal result

Current release evidence:

- frozen behavior regression: **337/337 PASS**.
- selected benchmark release set: **13/13 PASS**.
- World Framework Boundary: **27/27 PASS**.
- Kit Architecture Metadata: **7/7 PASS**.
- Standalone Source Export: **30/30 PASS**.
- relevant non-benchmark release set: **401/401 PASS**.
- combined selected seal evidence: **414/414 PASS, 0 failed, 0 skipped**.
- Catalog requiredProfileIds closure: **93 profiles / 0 missing**.
- Unity compile/update: **idle**.
- Unity diagnose: **healthy=true**.
- final Console after completed-test history was cleared: **0 errors / 0 warnings**.
- repository-wide git diff --check: **PASS / exit 0**.
- status, memory, architecture guide, README and validation matrix synchronized.

The three TimeKit Error logs observed before the final clear were expected negative-input test output and remain documented; no runtime error was hidden or changed to a warning.

**Performance / Release baseline = FROZEN / PASS.**
