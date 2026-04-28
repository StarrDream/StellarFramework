# ConfigKit / 配置系统

## 定位

`ConfigKit` 用来处理两类配置：

- `NormalConfig`
  普通 JSON 配置，适合用户设置、数值开关和本地覆盖。
- `NetConfig`
  网络环境与接口路由配置，适合环境切换和参数化 URL 拼接。

## 路径规则

统一传相对路径，不要传绝对路径。

常见示例：

- `Configs/Normal/TestGameConfig.json`
- `Configs/Net/TestApiConfig.json`

底层查找顺序：

1. `PersistentDataPath/相对路径`
2. `StreamingAssets/相对路径`

同一个 `configName` 必须稳定对应同一条相对路径，不要在运行时用同名配置切到另一份文件。

## NormalConfig

### 推荐加载方式

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework;

private async UniTask InitConfigsAsync()
{
    NormalConfig gameConfig = await ConfigKit.LoadNormalConfigAsync(
        "TestGameConfig",
        "Configs/Normal/TestGameConfig.json");

    if (gameConfig != null)
    {
        Debug.Log($"配置加载成功，来源: {(gameConfig.IsUserSave ? "本地存档" : "包内默认")}");
    }
}
```

### 兼容加载方式

```csharp
yield return ConfigKit.LoadNormalConfig(
    "TestGameConfig",
    "Configs/Normal/TestGameConfig.json",
    config =>
    {
        if (config != null)
        {
            Debug.Log("配置加载成功");
        }
    });
```

### 读取配置

```csharp
NormalConfig gameConfig = ConfigKit.GetNormalConfig("TestGameConfig");
if (gameConfig != null)
{
    string lang = gameConfig.GetString("GameSettings.Language", "en");
    bool showFps = gameConfig.GetBool("Features.ShowFPS", false);
    float volume = gameConfig.GetFloat("GameSettings.MasterVolume", 1.0f);
    var userData = gameConfig.GetVal<UserData>("UserData");
}
```

### 修改与保存

```csharp
gameConfig.Set("GameSettings.MasterVolume", 0.5f);
gameConfig.Save();
```

`Save()` 会把当前配置写入沙盒目录。下次启动时，`PersistentDataPath` 中的覆盖层会优先于 `StreamingAssets` 生效。

## NetConfig

### 配置文件结构

```json
{
  "ActiveProfile": "Dev",
  "Environments": {
    "Dev": {
      "GameSvc": "http://127.0.0.1:8080"
    },
    "Release": {
      "GameSvc": "https://api.game.com"
    }
  },
  "Endpoints": {
    "User.Login": {
      "Service": "GameSvc",
      "Path": "/api/login"
    },
    "Item.Detail": {
      "Service": "GameSvc",
      "Path": "/api/item/{id}"
    }
  }
}
```

### 推荐加载方式

```csharp
NetConfig apiConfig = await ConfigKit.LoadNetConfigAsync(
    "TestApiConfig",
    "Configs/Net/TestApiConfig.json");
```

### 路由调用

```csharp
string loginUrl = apiConfig.GetUrl("User.Login");
string itemUrl = apiConfig.GetUrl("Item.Detail", ("id", 1001));
```

## 编辑器辅助

通过 `StellarFramework -> Tools Hub -> ConfigKit 配置中心` 可以：

- 切换 `Dev / Release` 等全局环境
- 查看和清理本地覆盖层
- 检查样例配置文件是否已生成

## 常见问题

### Q1: 为什么 `GetUrl` 返回空字符串？
- 原因：`NetConfig` 尚未完成加载，或者 Key 不存在。
- 处理：先等 `LoadNetConfigAsync` 完成，再检查配置项名称。

### Q2: 修改了配置，但运行结果没变化？
- 原因：当前读到的是 `PersistentDataPath` 下的本地覆盖层。
- 处理：清理本地覆盖层，或在配置中心里执行清空操作。

### Q3: Android 为什么不能直接 `File.ReadAllText`？
- 原因：`StreamingAssets` 在 Android 上位于 Jar 包内。
- 处理：业务层只传相对路径，底层统一走 `UnityWebRequest`。

### Q4: 新接口和旧接口该怎么选？
- 新项目优先用 `LoadNormalConfigAsync` / `LoadNetConfigAsync`
- 旧项目可以继续使用协程包装接口

## 对应示例

- 代码示例：[Example_ConfigKit.cs](</c:/GitProjects/StellarFramework/Assets/StellarFramework/Samples/KitSamples/Example_ConfigKit/Example_ConfigKit.cs:1>)
- 样例配置：`Assets/StreamingAssets/Configs/Normal/TestGameConfig.json`
- 样例配置：`Assets/StreamingAssets/Configs/Net/TestApiConfig.json`
