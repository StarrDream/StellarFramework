# ConfigKit 使用手册

## 1. 设计理念 (Why)
*   **AppConfig**: 管理游戏内的全局开关（音量、语言、版本号）。需要支持“默认配置”和“用户存档”的自动合并。
*   **UrlConfig**: 管理网络接口地址。开发阶段需要频繁切换 Dev/Release 环境，硬编码 URL 是大忌。

**ConfigKit 的特性：**
*   **双层加载**：优先读取 `PersistentDataPath` (用户存档/热更)，缺失时回退到 `StreamingAssets` (包内默认)。
*   **高性能读取**：内部使用 `Dictionary` 缓存，避免每次读取都进行 JSON 解析。
*   **环境切换**：UrlConfig 支持一键切换 Dev/Release 环境，无需修改代码。

---

## 2. AppConfig (应用配置)

### 2.1 初始化
在游戏启动时调用：
```csharp
// 必须在协程或异步方法中调用
AppConfig.Init((success) => {
    if (success) Debug.Log("配置加载完成");
});
```

### 2.2 读取配置
```csharp
// 获取基础类型
string lang = AppConfig.GetString("GameSettings.Language", "en");
bool showFPS = AppConfig.GetBool("Features.ShowFPS", false);

// 获取复杂对象 (自动反序列化)
var userData = AppConfig.GetVal<UserData>("UserData");
```

### 2.3 修改与保存
```csharp
// 修改内存值
AppConfig.Set("GameSettings.MasterVolume", 0.8f);

// 异步写入磁盘 (PersistentDataPath)
AppConfig.Save();
```

### 2.4 编辑器工具
*   `Tools -> AppConfig -> 生成默认配置文件`: 在 StreamingAssets 生成模板。
*   `Tools -> AppConfig -> 清除本地存档`: 重置为默认状态。

---

## 3. UrlConfig (URL 管理)

### 3.1 配置文件结构 (urlConfig.json)
```json
{
  "ActiveProfile": "Dev",
  "Environments": {
    "Dev": { "ApiRoot": "http://127.0.0.1:8080" },
    "Release": { "ApiRoot": "https://api.game.com" }
  },
  "Endpoints": {
    "Login": { "Service": "ApiRoot", "Path": "/user/login" },
    "GetItem": { "Service": "ApiRoot", "Path": "/item/{id}" }
  }
}
```

### 3.2 代码调用
```csharp
// 1. 普通 URL
string loginUrl = UrlConfig.GetUrl("Login"); 
// 输出: http://127.0.0.1:8080/user/login

// 2. 带参数 URL (零 GC 拼接)
// 内部使用了 Span<T> 和 StringBuilder 缓存，无内存分配
string itemUrl = UrlConfig.GetUrl("GetItem", ("id", 1001));
// 输出: http://127.0.0.1:8080/item/1001
```

### 3.3 环境切换
*   菜单栏 `Tools -> UrlConfig -> 选择开发/正式模式`。
*   此操作会修改 EditorPrefs，**不修改 json 文件**，避免误提交测试配置到版本库。

---

## 4. 常见坑点 (Pitfalls)
1.  **BOM 头问题**：某些编辑器保存 JSON 会带 BOM 头，导致解析失败。框架内部 `ConfigCore` 已做自动清洗，但建议统一使用无 BOM UTF-8。
2.  **初始化时机**：`GetUrl` 之前必须确保 `UrlConfig.Init` 已完成，否则会返回空或报错。