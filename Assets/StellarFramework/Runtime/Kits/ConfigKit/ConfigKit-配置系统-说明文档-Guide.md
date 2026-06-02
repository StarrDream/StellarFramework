# ConfigKit / 配置系统说明文档

ConfigKit 用于加载普通配置和网络配置。它适合处理随包、StreamingAssets、PersistentDataPath 或远端覆盖的 JSON 配置。

## 入口 API

- `ConfigKit.LoadNormalConfig(...)` / `LoadNormalConfigAsync(...)`
- `ConfigKit.GetNormalConfig(configName)`
- `ConfigKit.LoadNetConfig(...)` / `LoadNetConfigAsync(...)`
- `ConfigKit.GetNetConfig(configName)`
- `ConfigKit.ClearAll()`
- `ConfigCore.LoadConfigAsync(...)`

## 使用模板

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.Config;

public sealed class ConfigEntry
{
    public async UniTask LoadAsync()
    {
        NormalConfig config = await ConfigKit.LoadNormalConfigAsync(
            "Gameplay",
            "Config/gameplay.json");

        int hp = config.GetInt("defaultHp", 100);
    }
}
```

## ToolsHub 关联

- `ConfigKit 配置中心` 用于查看、编辑和切换配置环境。
- 网络环境配置可与 HttpKit 配合使用。

## 常见问题

- 配置读取为空：确认路径和 JSON 格式。
- 环境切换不生效：确认读取的是同一个 configName，并清理旧缓存。
- StreamingAssets 平台路径不同：使用 `ConfigCore.GetStreamingAssetsUrl`。
