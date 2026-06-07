# ConfigKit / 配置系统说明文档

## 模块定位

`ConfigKit` 负责统一加载和缓存配置文件。

适合处理：

- 随包配置
- `StreamingAssets` 配置
- `persistentDataPath` 覆盖配置
- 普通 JSON 配置
- 网络环境配置

## 模块组成

- `ConfigKit`
  对外门面
- `ConfigCore`
  底层路径、请求和 JSON 解析
- `NormalConfig`
  普通配置
- `NetConfig`
  网络配置

## 标准使用方式

### 普通配置

```csharp
NormalConfig config = await ConfigKit.LoadNormalConfigAsync(
    "Gameplay",
    "Config/gameplay.json");
```

### 网络配置

```csharp
NetConfig netConfig = await ConfigKit.LoadNetConfigAsync(
    "Net",
    "Config/net.json");
```

## 运行规则

- 同名配置会缓存
- 同名配置不能映射到不同路径
- 加载优先尝试 `persistentDataPath`
- 找不到再读取 `StreamingAssets`
- 返回的底层数据最终会转换成 `JObject`

## ToolsHub 关联

- `ConfigKit 配置中心`
  查看、编辑和切换配置环境
- 配置环境通常和 `HttpKit` 一起使用

## 使用约束

- 不要把同名配置映射到多个不同路径
- `Get*Config()` 只读取已加载配置，不会隐式触发加载
- 改动配置结构时，要同步检查读取代码

## 常见问题

- 配置读出来为空
  检查路径、JSON 格式和文件是否存在。
- 环境切换不生效
  检查使用的 `configName` 是否一致，并确认旧缓存是否清理。
- 平台路径不同
  用 `ConfigCore.GetStreamingAssetsUrl(...)`。

## 相关文档

- [ConfigKit 源码文档](ConfigKit-配置系统-源码文档-Guide.md)
