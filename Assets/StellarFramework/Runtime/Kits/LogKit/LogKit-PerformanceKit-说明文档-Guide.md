# LogKit / PerformanceKit 说明文档

LogKit 提供统一日志入口，PerformanceKit 提供简单性能测量、内存日志和 GC 触发工具。它们适合框架和样例调试，不替代专业 Profiler。

## 入口 API

- `LogKit.SetLogger(logger)`：替换日志实现。
- `LogKit.Log(...)`、`LogWarning(...)`、`LogError(...)`
- `LogKit.LogException(exception)`
- `LogKit.Assert(condition, message)`
- `PerformanceUtil.MeasureExecutionTime(action, name)`
- `PerformanceUtil.LogMemoryUsage()`
- `PerformanceUtil.ForceGarbageCollection()`

## 使用模板

```csharp
LogKit.Log("加载完成");
LogKit.AssertNotNull(config, "配置不能为空");

PerformanceUtil.MeasureExecutionTime(() =>
{
    BuildCache();
}, "BuildCache");
```

## 常见问题

- 日志想写入文件：实现 `ILogger` 并 `SetLogger`。
- Assert 没阻止流程：Assert 是调试辅助，业务仍需自己 return 或 throw。
- 性能测量不准：用 Unity Profiler 做最终判断。
