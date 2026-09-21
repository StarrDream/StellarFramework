# LogKit / 日志与性能说明文档

`LogKit` 提供统一日志入口，`PerformanceUtil` 提供开发期性能和内存辅助工具。

## 核心入口

- `LogKit.SetLogger(...)`
- `LogKit.Log(...)`
- `LogKit.LogWarning(...)`
- `LogKit.LogError(...)`
- `LogKit.LogException(...)`
- `LogKit.Assert(...)`
- `PerformanceUtil.MeasureExecutionTime(...)`
- `PerformanceUtil.LogMemoryUsage()`
- `PerformanceUtil.ForceGarbageCollection()`

## 使用建议

- 常规运行日志通过 `Log / LogWarning`
- 错误分支用 `LogError`
- 开发期断言用 `Assert`
- 性能测量只作为轻量辅助，不代替 Profiler

## 自定义日志后端

如需写入文件、接入远端日志系统或项目自有日志系统，实现 `ILogger` 后调用：

```csharp
LogKit.SetLogger(customLogger);
```

## 常见问题

- 想输出到文件
  自定义 `ILogger`。
- Assert 没阻断流程
  Assert 是开发期辅助，不代替显式 `return / throw`。
- 性能数据不够准
  用 Unity Profiler 做最终分析。

## 相关文档

- [LogKit 源码文档](LogKit-PerformanceKit-源码文档-Guide.md)
