# ConfigKit / 配置系统源码文档

## 模块职责

`ConfigKit` 负责统一配置加载、缓存和访问。

当前模块分成两层：

- `ConfigCore`
  处理底层路径、`UnityWebRequest`、BOM 清理、JSON 解析
- `ConfigKit`
  处理配置实例缓存、去重加载和按名称访问

## 源码文件

- `Runtime/Kits/ConfigKit/ConfigKit.cs`
- `Runtime/Kits/ConfigKit/Core/ConfigCore.cs`
- `Runtime/Kits/ConfigKit/Configs/NormalConfig.cs`
- `Runtime/Kits/ConfigKit/Configs/NetConfig.cs`

## 总体结构

```text
ConfigCore
└─ LoadConfigAsync(...)

ConfigKit
├─ _normalConfigs
├─ _netConfigs
├─ _normalLoadingTasks
├─ _netLoadingTasks
├─ LoadNormalConfigAsync(...)
├─ LoadNetConfigAsync(...)
├─ GetNormalConfig(...)
└─ GetNetConfig(...)
```

## 调用链

### 加载普通配置

1. 业务调用 `LoadNormalConfigAsync(configName, relativePath)`
2. 校验名称和路径
3. 检查缓存中是否已存在
4. 检查是否有同名加载中任务
5. 若没有，则创建 `UniTaskCompletionSource`
6. `RunNormalLoadTask(...)` 调用 `BuildNormalConfigAsync(...)`
7. `BuildNormalConfigAsync(...)` 再调用 `ConfigCore.LoadConfigAsync(...)`
8. 成功后创建 `NormalConfig` 并写入缓存

### 加载网络配置

流程与普通配置相同，最终创建 `NetConfig`。

## 类型详解

## `ConfigCore`

### 作用

底层配置加载核心。

### 类型

#### `ConfigLoadResult`

字段：

- `Data : JObject`
- `IsUserSave : bool`

表示配置数据以及是否来自用户存档 / 热更目录。

### 方法

#### `LoadConfigProcess(...)`

协程版加载入口。

#### `LoadConfigAsync(...)`

异步版加载入口。

职责：

- 校验路径
- 优先尝试 `persistentDataPath`
- 找不到则回退 `StreamingAssets`
- 发起 `UnityWebRequest`
- 清理 UTF-8 BOM
- 解析 `JObject`

失败时返回空结果并记录错误日志。

#### `GetStreamingAssetsUrl(...)`

按平台生成 `StreamingAssets` 的可访问 URL。

#### `GetPersistentPath(...)`

获取持久化目录中的物理路径。

## `ConfigKit`

### 作用

配置系统对外门面。

### 核心字段

- `_normalConfigs`
  普通配置缓存
- `_netConfigs`
  网络配置缓存
- `_normalLoadingTasks / _netLoadingTasks`
  去重加载任务表
- `_normalConfigPaths / _netConfigPaths`
  配置名到路径映射

### 关键方法

#### `LoadNormalConfigAsync(...)`

普通配置异步加载入口。

职责：

- 校验名称不为空
- 归一化路径
- 保证同名配置不能映射到不同路径
- 命中缓存直接返回
- 命中加载中任务则等待同一个任务
- 否则创建新加载任务

#### `LoadNetConfigAsync(...)`

网络配置异步加载入口，流程与普通配置一致。

#### `LoadNormalConfig(...) / LoadNetConfig(...)`

协程包装入口。

#### `GetNormalConfig(...) / GetNetConfig(...)`

从缓存获取已加载配置，不会触发自动加载。

#### `ClearAll()`

清空所有缓存、加载任务和路径映射。

### 内部方法

#### `BuildNormalConfigAsync(...)`

调用 `ConfigCore`，把 `JObject` 构建成 `NormalConfig`。

#### `BuildNetConfigAsync(...)`

调用 `ConfigCore`，把 `JObject` 构建成 `NetConfig`。

#### `RunNormalLoadTask(...) / RunNetLoadTask(...)`

包装异步加载并负责清理加载中任务表。

#### `TryNormalizeAndValidatePath(...)`

归一化并校验路径合法性。

#### `EnsureConfigNameMatchesPath(...)`

防止同名配置被不同路径重复绑定。

#### `NormalizeRelativePath(...)`

把路径统一成正斜杠格式并去掉前导分隔符。

## 设计约束

- 同名配置只能对应一个路径
- `Get*Config()` 只读缓存，不隐式加载
- 配置加载优先 `persistentDataPath`，再回退 `StreamingAssets`
- JSON 必须能解析成 `JObject`

## 常见误用

- 同一个配置名传入不同路径
- 未加载前直接 `Get*Config()`
- 传空路径或空名称

## 测试建议

- 路径归一化
- 同名不同路径冲突
- 重复加载去重
- `persistentDataPath` 优先级
- BOM 清理与 JSON 解析失败
