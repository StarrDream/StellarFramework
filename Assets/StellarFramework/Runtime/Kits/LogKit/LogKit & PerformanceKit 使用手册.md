# LogKit & PerformanceKit 使用手册

## 1. 核心设计理念
本模块专为商业化高标准项目打造，严格遵循以下架构心智：
- **零开销原则 (Zero Overhead)**：通过宏定义 (`ENABLE_LOG`, `UNITY_EDITOR`, `DEVELOPMENT_BUILD`) 实现物理级隔离。在 Release 生产包中，所有日志调用与实机面板均不参与编译，彻底消除字符串拼接与 GUI 渲染开销。
- **极简与零依赖**：实机控制台去配置化，硬编码核心参数，无需依赖 ScriptableObject 或外部资产，开箱即用。
- **防御性编程**：提倡 Fail Fast（快速失败），拒绝使用 Try-Catch 掩盖业务错误，强制要求精准报错与前置拦截。

---

## 2. 模块划分与 API 说明

### 2.1 LogKit (核心日志流转)
负责基础日志的格式化与输出，自动附加时间戳与线程信息。

```csharp
// 常规信息打印
LogKit.Log("[System] 登录服务器连接成功");

// 警告信息打印
LogKit.LogWarning("[Audio] 找不到音效文件: bgm_01，已忽略");

// 错误信息打印 (触发时应立即 return 阻断后续逻辑)
LogKit.LogError($"[Player] 初始化失败: 缺失 Weapon 组件，当前状态: {state}");
```

### 2.2 PerformanceUtil (性能与内存诊断)
职责单一的性能工具箱，与基础日志解耦。

```csharp
// 测量代码块耗时 (Release 包自动剔除)
PerformanceUtil.MeasureExecutionTime(() => 
{
    LoadConfigData();
}, "LoadConfigData");

// 打印当前内存快照 (Reserved / Allocated / Mono Heap)
PerformanceUtil.LogMemoryUsage();

// 强制触发完整 GC 与无用资源卸载 (极度危险，仅限场景切换时调用)
PerformanceUtil.ForceGarbageCollection();
```

---

## 3. 实机控制台 (LogViewer) 使用指南

### 3.1 部署与唤出
- **自动部署**：无需在场景中挂载任何预制体。只要在 `Player Settings -> Scripting Define Symbols` 中添加了 `ENABLE_LOG` 宏，系统会在启动时自动创建常驻的 `[Stellar_LogViewer]` 节点。
- **唤出方式**：实机运行后，屏幕左上角会自适应生成一个 `Log` 按钮，点击即可展开全屏聚合控制台。

### 3.2 面板功能
控制台采用多页签聚合架构，分为两大视图：
1. **日志 (Log) 视图**：
    - **分类过滤**：提供类似 Unity Editor Console 的 `普通`、`警告`、`错误` 三种状态的独立开关，快速屏蔽无关信息。
    - **关键字搜索**：支持忽略大小写的实时文本搜索，精准定位关键报错。
    - **自动滚动**：支持自动滚动到底部，方便在大量日志刷屏时锁定最新信息。
    - 硬编码最大保留 500 条日志，防止内存溢出。
2. **设备 (System) 视图**：
    - **实时状态监控**：每秒降频刷新 Unity 内存分配情况（Reserved / Allocated / Mono）以及系统剩余内存估算值。
    - **硬件规格参数**：静态展示设备 ID、操作系统、CPU 型号/核心数/基准频率、GPU 型号/API/总显存等底层信息。

---

## 4. 编码规范约束 (必读)
在使用本套件进行业务开发时，必须遵守以下规范：
1. **前置拦截**：统一使用 `if` 进行参数与状态检查，拒绝深层嵌套。
2. **禁用 Try-Catch**：严禁在常规业务逻辑中使用 `try-catch`。遇到异常必须让其抛出或使用 `LogKit.LogError` 拦截并 `return`。
3. **精准报错**：`LogError` 的字符串必须包含：`[所在类/模块]`、`触发对象名`、`关键变量当前状态`。解释“为什么出错”而非仅仅“出错了”。