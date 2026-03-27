# ConfigKit 使用手册

**适用场景**: 全局配置管理、用户存档、多环境网络地址管理。

---

## 1. 核心理念 (Design Philosophy)
ConfigKit 旨在解决项目开发中的配置管理需求：

1.  **热更新与存档合并 (Overlay Pattern)**
    *   **机制**：采用双层加载机制。优先读取沙盒目录 (`PersistentDataPath`)，如果文件不存在，则回退读取包内目录 (`StreamingAssets`)。
    *   **效果**：支持通过下载新 JSON 到沙盒实现热更；支持用户修改配置后保存到沙盒覆盖默认值。

2.  **多环境切换 (Environment Switching)**
    *   **机制**：`NetConfig` 支持环境配置，可在 Dev（内网）、Release（正式服）等环境间切换。

3.  **性能优化**
    *   **NormalConfig**：使用 `Dictionary` 缓存解析后的值，减少重复解析开销。
    *   **NetConfig**：使用 `Span<T>` 和 `ThreadStatic StringBuilder` 进行 URL 拼接，减少字符串拼接产生的 GC。

---

## 2. NormalConfig (普通配置)
用于管理全局开关、版本号、以及用户设置（音量、语言）。

### 2.1 初始化与加载
在游戏启动流程中，通过 `ConfigKit` 门面进行异步加载。

```csharp
using StellarFramework;

// 异步加载普通配置
yield return ConfigKit.LoadNormalConfig("TestGameConfig", "Configs/Normal/TestGameConfig.json", config =>
{
    if (config != null)
    {
        Debug.Log($"配置加载成功. 来源: {(config.IsUserSave ? "本地存档" : "包内默认")}");
    }
});
```

### 2.2 读取配置
支持基础类型和对象反序列化。

```csharp
// 获取已加载的配置实例
NormalConfig gameConfig = ConfigKit.GetNormalConfig("TestGameConfig");
if (gameConfig != null)
{
    // 1. 读取基础类型 (第二个参数为默认值)
    string lang = gameConfig.GetString("GameSettings.Language", "en");
    bool showFPS = gameConfig.GetBool("Features.ShowFPS", false);
    float volume = gameConfig.GetFloat("GameSettings.MasterVolume", 1.0f);

    // 2. 读取复杂对象 (自动 JSON 反序列化)
    var userData = gameConfig.GetVal<UserData>("UserData");
}
```

### 2.3 修改与保存 (用户存档)
当用户修改设置时调用。

```csharp
// 1. 修改内存值
gameConfig.Set("GameSettings.MasterVolume", 0.5f);

// 2. 写入磁盘 (异步操作)
// 保存后，下次启动将优先读取沙盒目录中的该文件
gameConfig.Save();
```

---

## 3. NetConfig (网络地址管理)
用于管理 API 接口地址，支持参数拼接和环境切换。

### 3.1 配置文件结构 (`TestApiConfig.json`)
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

### 3.2 代码调用
```csharp
// 1. 初始化
yield return ConfigKit.LoadNetConfig("TestApiConfig", "Configs/Net/TestApiConfig.json");

NetConfig apiConfig = ConfigKit.GetNetConfig("TestApiConfig");

// 2. 获取普通 URL
// 输出: http://127.0.0.1:8080/api/login
string url = apiConfig.GetUrl("User.Login");

// 3. 获取带参数 URL (使用 ValueTuple 隐式转换为 UrlParam 结构体，减少装箱)
// 输出: http://127.0.0.1:8080/api/item/1001
string itemUrl = apiConfig.GetUrl("Item.Detail", ("id", 1001));
```

### 3.3 环境切换
在编辑器中，可通过 `StellarFramework -> Tools Hub -> ConfigKit 配置中心` 打开 Dashboard。
在网络配置面板中切换全局环境（如 Dev/Release），该操作会修改 EditorPrefs，便于本地测试。

---

## 4. 常见问题与避坑 (Troubleshooting)

### Q1: 为什么 `GetUrl` 返回空字符串？
*   **原因**：未调用 `ConfigKit.LoadNetConfig` 或初始化未完成。
*   **解决**：确保在游戏入口流程中，等待加载回调或协程执行完毕后再进入业务逻辑。

### Q2: 修改了配置，但运行游戏没变化？
*   **原因**：可能之前调用过 `Save()`，导致沙盒目录 (`PersistentDataPath`) 下生成了存档。框架优先读取沙盒存档。
*   **解决**：在 ConfigKit Dashboard 中点击“清空所有本地存档”，强制游戏读取最新的 `StreamingAssets` 配置。

### Q3: 平台路径适配
Unity 在 Android 平台下的 `StreamingAssets` 位于 Jar 包内，无法使用 `File.ReadAllText` 读取。ConfigKit 内部统一使用 `UnityWebRequest` 处理了各平台的路径差异，业务层只需传入相对路径即可。