# SimulationKit 源码说明

## Foundation 边界

`StellarFramework.SimulationKit.Core` 的 asmdef 没有程序集引用并启用 `noEngineReferences`。Runtime 源码只使用 `System` 和 `System.Collections.Generic`，不引用 UnityEngine、TimeKit、GridKit、SpatialKit、ResKit、UPM 或热更插件。

## 数据结构

每个内部 `SimulationEntry` 保存 `SimulationId`、正 `IntervalTicks` 和 `NextDueTick`。数组 `_heap` 是最小堆；字典 `_indices` 保存 ID 到堆索引的反向索引。因此查询、Contains、移除和变更都不需要线性扫描。

堆比较键是 `(NextDueTick asc, Id.Value asc)`。任何交换都同时更新字典中的两个索引，保证根删除、中间删除、末尾删除和 SetInterval 上下调整后仍可定位。

## 时间不变量

`ObserveTick` 在 Register、SetInterval 和 CollectDue 开始时执行。已观察时间只能保持或前进，回退抛出 `InvalidOperationException`；失败的参数校验也不会撤销“已观察到更晚时间”这一事实。`Clear` 清空容器并重置时间线，同时保留数组容量。

所有 `nowTick + positiveTicks` 都通过溢出检查。注册和 SetInterval 在写入前检查，因此失败是原子的。Collect 在处理 root 前先检查下一次到期时间；检查失败时不写目标 Span、不改变 root，之前已经提交的写入不回滚。

## 公共 API 契约

- `SimulationId`：负值构造抛出，零无效，正值有效；值相等即标识相等。
- `TryRegister`：普通版本首个到期为 `now + interval`；显式版本允许 `firstDelay >= 0`，零延迟表示立即到期。
- `TryUnregister`：成功移除指定 ID；无效或不存在返回错误。
- `TrySetInterval`：从当前 tick 重新排定并修复堆序。
- `CollectDue`：按预算写入到期 ID；过期项最多派发一次，然后以实际 `nowTick + interval` 重排。
- `TryGetInterval` / `TryGetNextDueTick`：字典定位，缺失返回 `false`。

## CollectDue 流程

1. 观察并校验时间线。
2. 若目标 Span 为空，直接根据根判断 `HasBacklog`。
3. 根到期且仍有预算时，先安全计算 `now + interval`。
4. 将 root ID 写入目标，更新 root 的 NextDueTick，然后向下堆化。
5. 重复直到预算用尽或根未到期；根据根再次计算 `HasBacklog`。

这保证了 no-due 检查为 O(1)，批量派发为 `O(k log n)`，注册/移除/变更为 `O(log n)`，状态查询为 O(1)。数组扩容和 Dictionary 扩容只发生在容量不足时；正常 CollectDue 热路径不创建临时托管对象，也不使用 LINQ、迭代器、委托或闭包。

## AOT、线程与存档

实现只使用普通非泛型业务类型和 BCL 容器，适合 Unity Mono/IL2CPP/AOT 编译；不依赖反射生成代码。实例不是线程安全的，调用方负责把时间推进和调度串行化。调度器内部堆/字典是运行时索引，不是存档格式；存档边界由业务重建登记。

## 验证覆盖

`SimulationKitTests` 覆盖 ID、注册变体、预算/Span.Empty、稳定排序、实际 dispatch tick、no-catch-up、backlog、根/中间/末尾删除、索引完整性、SetInterval 上下重排、溢出原子性、时间回退、Clear 重置和同 tick 行为。

`SimulationKitBenchmarkTests` 记录 100k 注册/查询/变更、100k 同刻到期预算 512、1M no-due/storage 压力和 100k staggered 负载的规模、环境、耗时、校验和与 coarse managed heap trend；不使用依赖机器速度的固定阈值。

## V1 非目标与后续方向

V1 不把调度器扩展成世界模拟框架，不加入优先级、分组、休眠枚举、追赶策略、并行容器或 Unity 生命周期适配。未来若有真实项目需求，可在外部增加 TimeKit Adapter、Jobs/Burst Adapter 或业务分组层，并保持 Core 的 ID/Span/时间单调契约不变。
