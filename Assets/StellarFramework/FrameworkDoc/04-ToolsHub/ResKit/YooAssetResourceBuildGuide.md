# YooAsset 资源构建

入口：**StellarFramework → Tools Hub → 资源管理 → YooAsset 资源构建**。也可以使用 **Tools → StellarFramework → 资源构建 → YooAsset** 快捷入口。

此工具读取项目已有的 YooAsset 采集配置，调用 YooAsset 的 `BuiltinBuildPipeline` 生成本地资源包。资源包可以交给 ResKit 的 YooAsset 加载器或资源内容更新 Provider 使用。构建过程不编译热更代码，不要求安装 HybridCLR。

## 第一次构建

1. 点击 **配置采集规则**，打开 YooAsset 采集编辑器。
2. 创建资源包，为它添加分组和资源目录，按项目需要配置地址、过滤和打包规则，然后保存。
3. 回到资源构建页面，点击 **刷新资源包**，选择刚创建的资源包。
4. 选择目标平台，填写资源版本，例如 `1.0.0`。Unity Hub 需要提前安装对应平台的构建支持模块。
5. 选择输出目录。默认是项目下的 `BuildArtifacts/Resources/YooAsset`。
6. 点击 **开始构建资源**。构建阶段进度由 YooAsset 的 Unity 进度窗口显示；完成后在 **上次构建** 中查看 Bundle 数量、产物大小、耗时或错误详情。
7. 点击 **打开产物目录** 获取文件。

以 Windows、`GameContent`、`1.0.0` 为例，产物目录为：

```text
BuildArtifacts/Resources/YooAsset/StandaloneWindows64/GameContent/1.0.0/
```

下一次修改资源后使用 `1.0.1` 等新版本。此工具拒绝覆盖已有版本目录；构建失败留下的同版本目录也不会被自动删除，可以选择新版本重试或自行确认后清理失败产物。

## 构建与发布

资源构建页面生成本地 Bundle、资源清单、哈希和版本文件，不上传文件，也不修改 `StreamingAssets`。资源加载或更新需要在业务启动流程中配置资源包、运行模式与资源路径，参见 [ResKit](../../02-Kits/Reskit/ResKit-统一资源-说明文档-Guide.md)。

需要编译 DLL、导出 AOT 元数据、校验客户端基线和上传代码热更内容时，使用 **热更发布器**。发布器内部复用本页面的资源构建实现，并继续执行它自己的代码产物和发布校验。

## 高级配置

页面提供 LZ4、LZMA 和不压缩三种选项，默认 LZ4。构建缓存保留，产物校验始终开启，文件名采用 HashName。

项目的自定义采集规则仍由 YooAsset 管理。需要配置加密、内置资源拷贝、其他构建管线或完整高级参数时，点击 **打开 YooAsset 原生构建器**。本页面的设置不会改写原生构建器设置或项目采集规则。

## 按需导出

在 **StellarFramework → Export** 中展开 ResKit，勾选编辑器工具 **YooAsset 资源构建工具**，对应 Catalog profile 为 `reskit.yooasset.tools`。

导出会自动包含 ToolsHub.Core、YooAsset 和当前 YooAsset 编辑器需要的最小 NUnit 兼容依赖。它不自动带入 ResKit 运行时、HybridCLR、代码更新 Provider、Addressables、Test Runner 或框架测试代码；需要运行时加载或资源更新时，再勾选相应功能。

## 常见问题

- **没有资源包**：到采集编辑器创建资源包，添加采集目录并保存，再刷新列表。
- **采集配置重复**：项目需保留唯一的 `AssetBundleCollectorSetting`，避免构建器读取到另一份配置。
- **平台不可用**：在 Unity Hub 为当前 Unity 版本安装对应平台模块。
- **版本已存在**：填写新版本或选择另一个输出目录。
- **没有生成 Bundle / 缺少清单**：检查采集目录、激活规则和过滤规则，查看页面保留的错误详情及 Unity Console。
