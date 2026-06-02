# HotUpdateKit / 热更新说明文档

HotUpdateKit 负责把资源热更新和 HybridCLR 代码热更新串成启动流程。资源版本、catalog/hash、bundle 下载和缓存由 Addressables 官方机制负责；框架负责 `HotUpdateManifest`、DLL SHA256 校验、AOT metadata 加载和入口方法调用。

## 入口 API

- `HotUpdateKit.Configure(...)`：注入资源热更策略和代码热更策略。
- `HotUpdateKit.InitializeAsync(...)`：初始化资源热更系统。
- `HotUpdateKit.CheckResourceUpdatesAsync(keys)`：检查 Addressables catalog 和下载大小。
- `HotUpdateKit.DownloadResourceUpdatesAsync(keys, progress)`：下载资源依赖。
- `HotUpdateKit.RunCodeHotUpdateAsync(...)`：运行 HybridCLR 代码热更。
- `HotUpdateKit.RunStartupHotUpdateAsync(...)`：启动期完整热更流程。
- `HybridCLRHook.LoadMetadataForAOTAssembliesAsync(...)`：加载 AOT metadata。
- `HybridCLRHook.LoadAndStartHotUpdateAssembly(bytes)`：加载热更 DLL 并调用入口。

## ToolsHub 流程

本地内置 AA：

1. 打开 `AA 配置与发布`。
2. 选择 `本地内置 AA`。
3. 点击 `一键本地内置构建`。
4. 资源和 Manifest 进入 `StreamingAssets/aa`。

远端热更 AA：

1. 选择 `远端热更 AA`。
2. 设置发布目录或 URL。
3. 点击 `一键远端热更发布`。
4. 工具导出 DLL、metadata、Manifest，构建 Addressables，复制 catalog/hash/bundle 到远端目录。

## 相关专题

- [AA 本地内置](AA-LocalBuiltIn-Guide.md)
- [AA 远端热更](AA-RemoteHotUpdate-Guide.md)
- [HotUpdateManifest](HotUpdateManifest-Guide.md)
- [HybridCLR 热更新](HybridCLR-热更新-Guide.md)
- [HotUpdateKit 源码文档](HotUpdateKit-热更新-源码文档-Guide.md)

## 常见问题

- 远端不更新：检查 `.hash` 和 RemoteLoadPath。
- Manifest 解析失败：确认 JSON 没损坏，框架已处理 UTF-8 BOM。
- DLL SHA 不匹配：Manifest 和 DLL 不是同一批发布。
- Metadata 加载失败：确认 AOT metadata key 在 Addressables 中可加载。
