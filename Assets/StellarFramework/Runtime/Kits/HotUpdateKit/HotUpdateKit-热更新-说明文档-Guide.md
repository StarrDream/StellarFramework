# HotUpdateKit / 热更新说明文档

## 模块定位

`HotUpdateKit` 把资源热更新和 `HybridCLR` 代码热更新收敛成一套统一的运行时入口，但这些能力均为可选层，不会随基础 Kit 自动引入。

职责边界：

- Addressables 负责 `catalog / hash / bundle` 的检查、下载和缓存
- `HotUpdateKit` 负责 `Manifest`、DLL SHA256、AOT metadata 和热更入口调度

## 模块结构

运行时主要由三层组成：

- `HotUpdateKit`
  对外门面，负责资源策略、代码策略和设置对象
- `HotUpdateManifest`
  描述代码热更新产物
- `HybridCLRHook / HybridCLRAAHotUpdateRunner`
  负责加载 metadata、校验 DLL、执行热更入口

## 按需安装边界

- `HotUpdate.Core`：只提供策略接口、Manifest 与明确的不可用结果；不依赖 Addressables、HybridCLR，也不包含代码热更实现。
- `HotUpdate.AddressablesAdapter`：在 `HotUpdate.Core + ResKit.Addressables` 之上提供 AA 资源热更新。
- `HotUpdate.HybridCLR`：在 Addressables 适配层之上提供 DLL、AOT metadata 与代码热更入口；只有此层需要 HybridCLR。

未选择这些 Profile 的项目不会因为 ResKit、UIKit 或 ToolsHub 而被要求安装 HybridCLR。

## 核心入口

- `HotUpdateKit.Configure(...)`
- `HotUpdateKit.InitializeAsync(...)`
- `HotUpdateKit.CheckResourceUpdatesAsync(...)`
- `HotUpdateKit.DownloadResourceUpdatesAsync(...)`
- `HotUpdateKit.RunCodeHotUpdateAsync(...)`
- `HotUpdateKit.RunStartupHotUpdateAsync(...)`

## 启动流程

### 资源热更新

1. `InitializeAsync(...)`
2. `CheckResourceUpdatesAsync(...)`
3. `DownloadResourceUpdatesAsync(...)`

### 代码热更新

1. 读取 `HotUpdateSettings`
2. 加载 `HotUpdateManifest`
3. 下载或读取 `dll.bytes` 和 `AOT metadata`
4. 校验 SHA256
5. 调用 `HybridCLRHook.LoadMetadataForAOTAssembliesAsync(...)`
6. 调用 `HybridCLRHook.LoadAndStartHotUpdateAssembly(...)`

## Manifest

`HotUpdateManifest.json` 是代码热更新的运行时事实来源。

典型字段：

- `hotUpdateAssemblyKey`
- `hotUpdateAssemblySha256`
- `hotUpdateEntryClass`
- `hotUpdateEntryMethod`
- `aotMetadataKeys`

建议：

- 不手写 Manifest
- 统一由 ToolsHub 导出 `dll.bytes` 时生成
- Manifest、catalog、hash、bundle 必须来自同一批产物

## Addressables 模式

### 本地内置 AA

特点：

- 资源和 Manifest 一起进入 `StreamingAssets/aa`
- 适合随包发布
- 不做远端下载

### 远端热更 AA

特点：

- 旧 Player 不重打包
- 通过远端 `HotUpdateManifest.json`、catalog、hash、bundle 更新资源和热更 DLL
- 可用 HTTP/CDN，也可先用本地 `file:///` 目录模拟

## HybridCLR 约定

运行时要求：

- 已安装并配置 `HybridCLR`
- 已启用 `HYBRIDCLR_ENABLE`
- 热更程序集已导出为 `.dll.bytes`
- AOT metadata 已导出为 `.dll.bytes`
- `HotUpdateSettings` 配置的入口类和方法与热更程序集一致

## 生产放行门禁

编辑器验证用于检查配置、依赖边界和失败诊断；真实发布还必须在每个目标平台的 IL2CPP Player 完成以下验收：

1. 从发布 CDN 下载 catalog、bundle、Manifest、DLL 与所有 metadata。
2. 校验 Manifest 与 DLL 的 SHA256，并验证 catalog/hash/bundle 属于同一批产物。
3. 成功加载 AOT metadata、进入热更入口，并验证失败时能阻断启动或回退到稳定版本。
4. 在断网、旧 catalog、损坏 DLL、缺失 metadata 四种场景验证可观测错误与回滚策略。

## ToolsHub 关联

- `AA 配置与发布`
  负责本地内置 AA 和远端热更 AA 的统一发布入口
- `HybridCLR DLL 导出`
  负责导出 `.dll.bytes`、`AOT metadata` 和 `HotUpdateManifest.json`

## 常见问题

- 远端没有更新
  检查 `.hash`、`RemoteLoadPath` 和 Manifest 地址。
- Manifest 解析失败
  检查 JSON 是否损坏；框架会处理 UTF-8 BOM。
- DLL SHA 不匹配
  Manifest 和 DLL 不是同一批导出结果。
- Metadata 加载失败
  检查 `AOT metadata` key 是否可通过 Addressables 正常加载。

## 相关文档

- [HotUpdateKit 源码文档](HotUpdateKit-热更新-源码文档-Guide.md)
