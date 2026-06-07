# LogKit / 日志与性能源码文档

## 模块职责

`LogKit` 负责统一日志接口，`PerformanceUtil` 负责开发期性能测量和内存快照。

## 源码文件

- `Runtime/Kits/LogKit/LogKit.cs`
- `Runtime/Kits/LogKit/Interface/ILogger.cs`
- `Runtime/Kits/LogKit/Logger/UnityLogger.cs`
- `Runtime/Kits/LogKit/PerformanceUtil.cs`
- `Runtime/Kits/LogKit/LogViewer.cs`

## 类型详解

## `ILogger`

### 作用

定义日志输出后端接口。

### 方法

- `Log(string message)`
- `LogWarning(string message)`
- `LogError(string message)`
- `LogException(Exception e)`

## `UnityLogger`

### 作用

默认日志实现，直接桥接 Unity `Debug`。

### 方法

- `Debug.Log`
- `Debug.LogWarning`
- `Debug.LogError`
- `Debug.LogException`

## `LogKit`

### 作用

统一日志门面。

### 字段

- `_logger`
  当前日志后端，默认是 `UnityLogger`

### 方法

#### `SetLogger(...)`

注入自定义日志后端。

#### `Log / LogWarning`

带 `[Conditional("ENABLE_LOG")]`，仅在启用日志宏时生效。

#### `LogError`

错误日志入口，不带条件编译，默认始终有效。

#### `LogException`

异常日志入口。

#### `Assert(...) / AssertNotNull(...)`

开发期断言工具。

特点：

- 仅在 `UNITY_EDITOR / DEVELOPMENT_BUILD` 下生效
- Editor 下断言失败会抛异常

#### `AssertAndLog(...)`

返回 `bool` 的校验辅助。

#### `ErrorAndReturnFalse(...)`

统一的“记录错误并返回 false”辅助方法。

## `PerformanceUtil`

### 作用

提供开发期性能测量与内存观察。

### 方法

#### `MeasureExecutionTime(...)`

测量代码块耗时。

特点：

- 仅在开发期生效
- 不吞异常，异常会自然上抛

#### `LogMemoryUsage()`

打印当前内存快照：

- Unity Reserved
- Unity Allocated
- Mono Heap

#### `ForceGarbageCollection()`

强制执行 GC 与 `Resources.UnloadUnusedAssets()`。

属于高风险操作，只适合明确的清理节点。

## `LogViewer`

### 作用

运行时日志查看器，用于把日志输出收集并展示在游戏内。

当前源码文档重点不在它的 UI 绘制细节，而在于：

- 它依赖 `LogKit` 输出结果
- 主要承担开发调试可视化用途

## 设计约束

- `Log` 和 `LogWarning` 受 `ENABLE_LOG` 控制
- `Assert` 不应替代正式的运行时错误阻断
- `ForceGarbageCollection()` 不能当作常规运行时逻辑

## 常见误用

- 以为 `Assert` 在 Release 下也会真正阻断
- 在高频逻辑里频繁强制 GC
- 忘记给项目注入自定义 `ILogger`
