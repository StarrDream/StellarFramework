# HotUpdateKit / 热更新源码文档

## 模块职责

`HotUpdateKit` 负责把资源热更新和代码热更新统一到同一套运行时入口里。

当前模块主要解决三件事：

- 描述热更新产物：`HotUpdateManifest`
- 组织资源热更新流程：`IResourceHotUpdateStrategy`
- 组织代码热更新流程：`ICodeHotUpdateStrategy`、`HybridCLRHook`、`HybridCLRAAHotUpdateRunner`

默认组合是：

- 资源层：`AddressablesHotUpdateStrategy`
- 代码层：`HybridCLRCodeHotUpdateStrategy`

## 源码文件

- `Runtime/Kits/HotUpdateKit/HybridCLRHook.cs`
- `Runtime/Kits/HotUpdateKit/HotUpdateManifest.cs`
- `Runtime/Kits/HotUpdateKit/HotUpdateSettings.cs`
- `Runtime/Kits/HotUpdateKit/HotUpdateRuntimePolicy.cs`
- `Runtime/Kits/Reskit/Loaders/AddressableLoader/AddressableHotUpdateManager.cs`

## 总体结构

```text
HotUpdateKit
├─ _resourceStrategy
├─ _codeStrategy
└─ _settings

HybridCLRHook
├─ HotUpdateState
├─ HotUpdateAssemblyName
├─ HotUpdateEntryClass
├─ HotUpdateEntryMethod
└─ AOTMetaAssemblyFiles

HybridCLRAAHotUpdateRunner
├─ 读取 Manifest
├─ 初始化 Addressables
├─ 下载 dll.bytes / metadata
└─ 调用 HybridCLRHook

HotUpdateManifest
├─ hotUpdateAssemblyKey
├─ hotUpdateAssemblySha256
├─ hotUpdateEntryClass
├─ hotUpdateEntryMethod
└─ aotMetadataKeys
```

## 运行时调用链

### 启动期标准流程

1. 读取 `HotUpdateSettings`
2. `HotUpdateManifestSourceChain.BuildDefaultSources(...)`
3. `HotUpdateManifestSourceChain.LoadAsync(...)`
4. `HotUpdateKit.ResourceStrategy.InitializeAsync(...)`
5. `CheckResourceUpdatesAsync(...)`
6. `DownloadResourceUpdatesAsync(...)`
7. `HybridCLRAAHotUpdateRunner.RunAsync(...)`
8. `HybridCLRHook.LoadMetadataForAOTAssembliesAsync(...)`
9. `HybridCLRHook.LoadAndStartHotUpdateAssembly(...)`

### 资源更新与代码更新的边界

- `HotUpdateKit` 只负责“编排”
- 资源下载、catalog 更新、缓存由资源策略负责
- AOT metadata 和热更程序集加载由 `HybridCLRHook` 负责
- Manifest 来源与 fallback 顺序由 `HotUpdateManifestSourceChain` 负责

## 类型详解

## `HotUpdateManifest`

### 作用

描述本次代码热更新所需的最小运行时信息。

### 字段

- `version`
  Manifest 版本号。
- `buildTarget`
  目标平台标识。
- `hotUpdateAssemblyKey`
  热更程序集资源 key，通常指向 `dll.bytes`。
- `hotUpdateAssemblySha256`
  热更程序集 SHA256。
- `hotUpdateEntryClass`
  热更入口类全名。
- `hotUpdateEntryMethod`
  热更入口方法名。
- `aotMetadataKeys`
  AOT metadata 资源 key 列表。

### 关键方法

- `FromJson(...)`
  从 JSON 构建 Manifest，内部会清理 BOM。
- `FromRuntimeSettings(...)`
  从 `HotUpdateSettings` 生成运行时 Manifest。
- `ToJson(...)`
- `BuildDownloadKeys()`
  生成需要下载的 key 列表，包含 metadata 和热更程序集。
- `Validate(...)`
  校验字段完整性，可选严格校验 SHA256。

### 设计约束

- `hotUpdateAssemblyKey` 不能为空
- 严格模式下 `hotUpdateAssemblySha256` 不能为空且长度必须为 64
- `hotUpdateEntryClass` 和 `hotUpdateEntryMethod` 不能为空
- `aotMetadataKeys` 不能为空

## `HotUpdateManifestValidationReport`

### 作用

承载 Manifest 校验结果。

### 字段

- `Errors`
- `Warnings`
- `IsValid`

## `HotUpdateManifestLoadResult`

### 作用

承载一次 Manifest 加载结果。

### 字段

- `Success`
- `Manifest`
- `Source`
- `Error`
- `Errors`

### 工厂方法

- `Ok(...)`
- `Fail(...)`

## `IHotUpdateManifestSource`

### 作用

抽象 Manifest 读取来源。

### 成员

- `Description`
- `LoadAsync(...)`

### 实现类

- `StreamingAssetsHotUpdateManifestSource`
- `FileUriHotUpdateManifestSource`
- `HttpHotUpdateManifestSource`
- `ResourcesHotUpdateManifestSource`

## `HotUpdateManifestSourceChain`

### 作用

按优先级依次尝试多个 Manifest 来源。

### 关键方法

- `LoadAsync(...)`
  顺序尝试 source，直到成功为止。
- `BuildDefaultSources(settings, strictProduction)`
  构建默认来源链。
- `CreateExplicitSource(...)`
  根据 URL 或本地路径生成具体 source。

### 默认顺序

1. 显式路径 / URL
2. `StreamingAssets`
3. `Resources`

严格生产模式下会减少 fallback。

## `HybridCLRHook`

### 作用

负责 AOT metadata 加载和热更程序集跳转。

### 核心字段

- `HotUpdateAssemblyName`
- `HotUpdateEntryClass`
- `HotUpdateEntryMethod`
- `AOTMetaAssemblyFiles`
- `State`
- `LastError`
- `LoadedAssemblyFullName`

### `HotUpdateState`

- `None`
- `LoadingMetadata`
- `MetadataLoaded`
- `LoadingHotUpdateAssembly`
- `LoadedHotUpdateAssembly`
- `EnteringHotUpdate`
- `EnteredHotUpdate`
- `Failed`

### 关键方法

#### `LoadMetadataForAOTAssembliesAsync(...)`

职责：

- 遍历 `AOTMetaAssemblyFiles`
- 通过外部提供的 bytes provider 读取每个 metadata
- 调用 `TryLoadMetadataForAotAssembly(...)`
- 成功后状态进入 `MetadataLoaded`

#### `LoadAndStartHotUpdateAssembly(...)`

职责：

- 校验 DLL 字节流
- 通过 `Assembly.Load` 加载程序集
- 查找入口类型和入口方法
- 反射执行热更入口

#### `TryLoadMetadataForAotAssembly(...)`

通过 `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(...)` 注入 metadata。

#### `SetFailed(...)`

统一记录失败状态和日志。

## `HybridCLRAAHotUpdateRunner`

### 作用

把 Addressables 下载流程和 HybridCLR 代码跳转连起来。

### 关键状态

- `None`
- `InitializingAddressables`
- `CheckingCatalogs`
- `DownloadingDependencies`
- `LoadingBytes`
- `LoadingMetadata`
- `LoadingAssembly`
- `EnteredHotUpdate`
- `Failed`

### 关键方法

#### `RunAsync(...)`

职责：

- 加载并校验 `HotUpdateSettings`
- 加载并校验 Manifest
- 初始化资源策略
- 检查并下载资源
- 通过 `ResKit` 的 Addressables loader 读取 bytes
- 校验 DLL SHA256
- 调用 `HybridCLRHook`

#### `LoadMetadataBytesAsync(...)`

逐个加载 metadata 对应的 `TextAsset.bytes`。

#### `VerifySha256(...)`

对比实际哈希与 Manifest 哈希。

#### `ComputeSha256(...)`

计算十六进制 SHA256。

#### `Fail(...)`

统一失败结果构造。

## `IResourceHotUpdateStrategy`

### 作用

抽象资源热更新后端。

### 方法

- `InitializeAsync(...)`
- `CheckResourceUpdatesAsync(...)`
- `DownloadResourceUpdatesAsync(...)`
- `ClearResourceCacheAsync(...)`

## `ICodeHotUpdateStrategy`

### 作用

抽象代码热更新后端。

### 方法

- `RunCodeHotUpdateAsync(...)`

## `AddressablesHotUpdateStrategy`

### 作用

默认资源策略占位实现。

当前行为：

- 若没有装配 Addressables 热更新实现，统一返回 `Unavailable`

## `HybridCLRCodeHotUpdateStrategy`

### 作用

默认代码策略实现。

本质上只是转发给 `HybridCLRAAHotUpdateRunner.RunAsync(...)`。

## `HotUpdateKit`

### 作用

整个模块的静态门面。

### 核心字段

- `_resourceStrategy`
- `_codeStrategy`
- `_settings`

### 核心属性

- `ResourceStrategy`
- `CodeStrategy`
- `Settings`

### 关键方法

#### `Configure(...)`

替换资源策略、代码策略和设置对象。

#### `SetResourceStrategy(...)`

单独替换资源策略。

#### `SetCodeHotUpdateStrategy(...)`

单独替换代码策略。

#### `InitializeAsync(...)`

转发给资源策略初始化。

#### `CheckResourceUpdatesAsync(...)`

解析默认 keys 后调用资源策略。

#### `DownloadResourceUpdatesAsync(...)`

解析默认 keys 后调用下载。

#### `ClearResourceCacheAsync(...)`

转发给资源策略。

#### `RunCodeHotUpdateAsync(...)`

校验 `HotUpdateSettings` 后调用代码策略。

#### `RunStartupHotUpdateAsync(...)`

当前只是 `RunCodeHotUpdateAsync(...)` 的别名。

## 设计约束

- 资源层和代码层是分开的抽象
- Manifest 是代码热更的事实来源
- 严格生产模式下要求更高的完整性校验
- `HotUpdateKit` 本身不直接依赖某个唯一资源后端，而是依赖策略接口

## 常见误用

- 没有正确生成 Manifest 就直接跑热更
- 没有注册可用的资源策略却调用资源热更新 API
- `HotUpdateSettings` 字段没配全
- Manifest 和实际导出 DLL 不是同一批产物

## 测试建议

- Manifest 解析与校验
- SourceChain fallback 顺序
- SHA256 校验
- 下载 key 生成
- 资源策略返回异常或不可用时的失败分支
