# LogKit / PerformanceKit 源码文档

## 源码位置

- `Runtime/Kits/LogKit/LogKit.cs`
- `Runtime/Kits/LogKit/Interface/ILogger.cs`
- `Runtime/Kits/LogKit/Logger/UnityLogger.cs`
- `Runtime/Kits/LogKit/PerformanceUtil.cs`
- `Runtime/Kits/LogKit/LogViewer.cs`

## 核心类型

- `ILogger`：日志输出接口。
- `UnityLogger`：默认实现，转发到 Unity Debug。
- `LogKit`：静态日志门面。
- `PerformanceUtil`：性能和内存辅助工具。
- `LogViewer`：运行时日志查看组件。

## 关键方法

- `SetLogger`：替换日志输出目标。
- `Log` / `LogWarning` / `LogError`：标准日志。
- `LogException`：异常日志。
- `Assert` / `AssertNotNull` / `AssertAndLog`：调试断言。
- `MeasureExecutionTime`：Stopwatch 包裹同步代码块。
- `LogMemoryUsage`：输出当前内存概况。
- `ForceGarbageCollection`：触发 GC。

## 数据流

业务调用 `LogKit`，LogKit 把消息交给当前 `ILogger`。默认 logger 使用 Unity Debug 输出。PerformanceUtil 不持有状态，只在调用时测量或输出。

## 依赖关系

- 依赖 UnityEngine Debug。
- PerformanceUtil 依赖 .NET Stopwatch 和 GC。
- LogViewer 依赖 Unity UI 或文本组件。

## 扩展点

- 接文件日志：实现 `ILogger`。
- 接远端日志：实现 `ILogger` 并在内部做队列和限流。
- 增加性能指标：扩展 `PerformanceUtil`，避免把长生命周期 profiler 状态放进去。

## 测试入口

- 日志实现替换可通过 EditMode 测试验证调用次数。
- LogViewer 需要在样例场景中验证 UI 展示。
