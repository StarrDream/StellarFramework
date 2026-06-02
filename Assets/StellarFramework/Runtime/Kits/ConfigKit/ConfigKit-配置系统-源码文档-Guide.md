# ConfigKit / 配置系统源码文档

## 源码位置

- `Runtime/Kits/ConfigKit/ConfigKit.cs`：静态入口和缓存。
- `Runtime/Kits/ConfigKit/Core/ConfigCore.cs`：底层加载流程。
- `Runtime/Kits/ConfigKit/Configs/NormalConfig.cs`：普通配置封装。
- `Runtime/Kits/ConfigKit/Configs/NetConfig.cs`：网络配置和环境封装。

## 核心类型

- `ConfigKit`：配置系统入口，管理已加载配置字典。
- `ConfigCore`：处理 StreamingAssets、PersistentDataPath 和 UnityWebRequest 加载。
- `NormalConfig`：基于 JSON 的普通配置读取。
- `NetConfig`：按 `UrlEnvironment` 管理网络地址。
- `UrlEnvironment`：开发、测试、生产等环境枚举。
- `UrlParam`：URL 参数辅助结构。

## 关键方法

- `LoadNormalConfigAsync`：加载普通配置并缓存到名称表。
- `GetNormalConfig`：读取已加载普通配置。
- `LoadNetConfigAsync`：加载网络配置。
- `GetNetConfig`：读取已加载网络配置。
- `ConfigCore.LoadConfigAsync`：底层读取 JSON 并返回加载结果。
- `GetStreamingAssetsUrl` / `GetPersistentPath`：跨平台路径转换。

## 数据流

业务调用 `ConfigKit.Load...`，ConfigKit 委托 `ConfigCore` 从路径读取 JSON。读取成功后包装成 `NormalConfig` 或 `NetConfig` 并缓存。业务后续通过 configName 获取配置对象，按 key 或环境读取值。

## 依赖关系

- 依赖 Newtonsoft.Json 的 `JObject`。
- 依赖 UnityWebRequest 读取 StreamingAssets 或 URL。
- 可与 HttpKit 共用网络环境配置。
- 可被 ToolsHub 的 ConfigKit 配置中心读取和编辑。

## 扩展点

- 新增配置类型：新增配置类并在 `ConfigKit` 中提供加载和缓存入口。
- 新增环境：扩展 `UrlEnvironment` 并同步 ToolsHub UI。
- 新增覆盖策略：优先在 `ConfigCore` 扩展路径查找，不要让业务散落路径判断。

## 测试入口

- ConfigKit 样例场景。
- ToolsHub `ConfigKit 配置中心`。
- 修改路径策略时验证 StreamingAssets、PersistentDataPath、远端 URL 三类路径。
