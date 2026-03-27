# LogKit & PerformanceKit 使用手册

## 1. 核心设计理念
本模块遵循以下设计原则：
- **条件编译隔离**：通过宏定义 (`ENABLE_LOG`, `UNITY_EDITOR`, `DEVELOPMENT_BUILD`) 进行隔离。在 Release 生产包中，相关日志调用不参与编译，以减少字符串拼接开销。
- **防御性编程**：提倡 Fail Fast（快速失败）。建议使用前置拦截，遇到异常情况可使用 `LogKit.LogError` 输出并 `return`。

---

## 2. 模块划分与 API 说明

### 2.1 LogKit (核心日志流转)
负责基础日志的输出。

```csharp
// 常规信息打印
LogKit.Log("[System] 登录服务器连接成功");

// 警告信息打印
LogKit.LogWarning("[Audio] 找不到音效文件，已忽略");

// 错误信息打印 (触发时建议紧跟 return 阻断逻辑)
LogKit.LogError($"[Player] 初始化失败: 缺失 Weapon 组件，当前状态: {state}");

// 状态断言 (仅在 Editor/Dev 环境下抛出异常，Release 包自动剔除)
LogKit.Assert(hp > 0, "血量不能小于等于0");
```

### 2.2 PerformanceUtil (性能与内存诊断)
提供基础的性能测试与内存查看工具。

```csharp
// 测量代码块耗时 (Release 包自动剔除)
PerformanceUtil.MeasureExecutionTime(() => 
{
    LoadConfigData();
}, "LoadConfigData");

// 打印当前内存快照 (Reserved / Allocated / Mono Heap)
PerformanceUtil.LogMemoryUsage();

// 强制触发完整 GC 与无用资源卸载 (会引起卡顿，仅限特定节点使用)
PerformanceUtil.ForceGarbageCollection();
```

---

## 3. 实机控制台 (LogViewer) 使用指南

### 3.1 部署与唤出
- **自动部署**：在 `Player Settings -> Scripting Define Symbols` 中添加 `ENABLE_LOG` 宏后，系统会在启动时自动创建 `[Stellar_LogViewer]` 节点。
- **唤出方式**：实机运行后，屏幕左上角会生成一个 `Log` 按钮，点击即可展开控制台。

### 3.2 面板功能
1. **日志 (Log) 视图**：
    - **分类过滤**：提供 `普通`、`警告`、`错误` 三种状态的独立开关。
    - **关键字搜索**：支持忽略大小写的实时文本搜索。
    - **自动滚动**：支持自动滚动到底部。
    - 限制最大保留 500 条日志。
2. **设备 (System) 视图**：
    - **实时状态监控**：定期刷新 Unity 内存分配情况（Reserved / Allocated / Mono）以及系统剩余内存估算值。
    - **硬件规格参数**：展示设备 ID、操作系统、CPU/GPU 型号等信息。

---

## 4. 编码规范建议
1. **前置拦截**：统一使用 `if` 进行参数与状态检查，减少深层嵌套。
2. **异常处理**：常规业务逻辑中尽量避免使用 `try-catch` 掩盖错误。
3. **日志内容**：`LogError` 的字符串建议包含：`[所在类/模块]`、`触发对象名`、`关键变量当前状态`。