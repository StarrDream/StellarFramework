# ConfigKit 使用手册

**适用场景**: 全局配置管理、用户存档、多环境网络地址管理。

---

## 1. 核心理念 (Design Philosophy)

ConfigKit 旨在解决商业化项目中配置管理的三个核心痛点：

1.  **热更新与存档合并 (Overlay Pattern)**
    *   **问题**：游戏发布后，如何不重新发包就能修改配置？用户的设置（如音量）如何与默认配置共存？
    *   **方案**：采用“双层加载机制”。优先读取沙盒目录 (`PersistentDataPath`)，如果文件不存在，则回退读取包内目录 (`StreamingAssets`)。
    *   **效果**：
        *   **热更**：只需下载新的 JSON 到沙盒目录，即可覆盖包内配置。
        *   **存档**：用户修改配置后保存到沙盒，下次启动自动覆盖默认值。

2.  **多环境切换 (Environment Switching)**
    *   **问题**：开发阶段需要频繁在 Dev（内网）、Test（测试服）、Release（正式服）之间切换 API 地址。
    *   **方案**：`UrlConfig` 支持环境配置，一键切换，且不修改本地文件（避免误提交测试配置到版本库）。

3.  **极致性能 (High Performance)**
    *   **问题**：高频读取配置（如每帧检查开关）或拼接 URL 会产生大量 GC。
    *   **方案**：
        *   **AppConfig**：使用 `Dictionary` 缓存解析后的值，读取复杂度 O(1)，无反射开销。
        *   **UrlConfig**：使用 `Span<T>` 和 `ThreadStatic StringBuilder` 进行 URL 拼接，实现 **零 GC (Zero Allocation)**。

---

## 2. AppConfig (应用配置)

用于管理全局开关、版本号、以及用户设置（音量、语言）。

### 2.1 初始化
必须在游戏启动流程（如 `GameEntry`）中调用。

```csharp
// 异步初始化
AppConfig.Init((success) => {
    if (success) Debug.Log("AppConfig 加载完成");
    else Debug.LogError("AppConfig 加载失败");
});
```

### 2.2 读取配置
支持基础类型和对象反序列化。

```csharp
// 1. 读取基础类型 (第二个参数为默认值)
string lang = AppConfig.GetString("GameSettings.Language", "en");
bool showFPS = AppConfig.GetBool("Features.ShowFPS", false);
float volume = AppConfig.GetFloat("GameSettings.MasterVolume", 1.0f);

// 2. 读取复杂对象 (自动 JSON 反序列化)
// 假设 JSON: { "UserData": { "Name": "Player", "Level": 10 } }
var userData = AppConfig.GetVal<UserData>("UserData");
```

### 2.3 修改与保存 (用户存档)
当用户修改设置时调用。

```csharp
// 1. 修改内存值
AppConfig.Set("GameSettings.MasterVolume", 0.5f);

// 2. 写入磁盘
// 注意：Save 是异步操作，不会卡顿主线程
AppConfig.Save();
```

### 2.4 编辑器工具
*   **生成默认配置**：`Tools -> AppConfig -> 生成默认配置`。会在 `StreamingAssets` 生成 `appConfig.json` 模板。
*   **清除存档**：`Tools -> AppConfig -> 清除本地存档`。删除沙盒中的文件，恢复为包内默认状态。

---

## 3. UrlConfig (网络地址管理)

用于管理 API 接口地址，支持参数拼接和环境切换。

### 3.1 配置文件结构 (`urlConfig.json`)

```json
{
  "ActiveProfile": "Dev", // 默认环境
  "Environments": {
    "Dev": { 
        "GameSvc": "http://127.0.0.1:8080", // 游戏服根地址
        "ChatSvc": "http://127.0.0.1:9090"  // 聊天服根地址
    },
    "Release": { 
        "GameSvc": "https://api.game.com",
        "ChatSvc": "https://chat.game.com"
    }
  },
  "Endpoints": {
    // 模块.功能 (推荐命名规范)
    "User.Login": { 
        "Service": "GameSvc", // 指向 Environments 中的 Key
        "Path": "/api/login" 
    },
    "Item.Detail": { 
        "Service": "GameSvc", 
        "Path": "/api/item/{id}" // 支持参数
    }
  }
}
```

### 3.2 代码调用

```csharp
// 1. 初始化
yield return UrlConfig.Init();

// 2. 获取普通 URL
// 输出: http://127.0.0.1:8080/api/login
string url = UrlConfig.GetUrl("User.Login");

// 3. 获取带参数 URL (高性能拼接)
// 输出: http://127.0.0.1:8080/api/item/1001
string itemUrl = UrlConfig.GetUrl("Item.Detail", ("id", 1001));
```

### 3.3 核心概念：Service (服务节点)
*   **定义**：`Service` 是服务器根地址的别名。
*   **作用**：解耦“接口路径”和“域名”。
*   **场景**：当后端更换域名时，只需在 `Environments` 里修改 `GameSvc` 的值，所有引用该 Service 的接口都会自动生效。

### 3.4 环境切换
*   **菜单**：`Tools -> UrlConfig -> Switch to Dev/Release`。
*   **原理**：此操作仅修改 Unity 的 `EditorPrefs`，**不会修改 JSON 文件**。
*   **好处**：避免开发者在本地测试时将 `ActiveProfile: Dev` 误提交到 SVN/Git，导致线上版本连接到内网。

---

## 4. 底层原理 (Under the Hood)

### 4.1 平台路径适配 (`ConfigCore.cs`)
Unity 在 Android 平台下的 `StreamingAssets` 位于 Jar 包内，无法使用 `File.ReadAllText` 读取。
*   **解决方案**：ConfigKit 内部统一使用 `UnityWebRequest` 读取配置。
*   **路径处理**：自动识别平台，Android 下使用 `jar:file://` 协议，iOS/PC 使用 `file://` 协议。

### 4.2 BOM 头清洗
*   **问题**：Windows 下某些文本编辑器保存 UTF-8 时会带 BOM (Byte Order Mark) 头，导致 JSON 解析库在解析第一个字符时报错。
*   **处理**：加载流程中自动检测并移除 `\uFEFF` 字符，增强健壮性。

### 4.3 零 GC URL 拼接 (`UrlConfig.cs`)
*   **传统做法**：使用 `string.Replace` 或 `string.Format` 拼接 URL 参数。这会产生大量临时字符串，导致 GC。
*   **本框架做法**：
    1.  使用 `ThreadStatic StringBuilder` 复用内存缓冲区。
    2.  使用 `Span<T>` (C# 7.2+) 对字符串进行切片操作，完全在栈内存上处理，不产生堆内存分配。

---

## 5. 常见问题与避坑 (Troubleshooting)

### Q1: 为什么 `UrlConfig.GetUrl` 返回空字符串？
*   **原因**：未调用 `UrlConfig.Init()` 或初始化未完成。
*   **解决**：确保在游戏入口流程中，`yield return UrlConfig.Init()` 执行完毕后再进入业务逻辑。

### Q2: 接口名冲突怎么办？
*   **现象**：`urlConfig.json` 中有两个 `"Login"`，读取时只能读到最后一个。
*   **解决**：JSON 的 Key 必须唯一。建议使用 **"模块.功能"** 的命名规范。
    *   ❌ `Login`
    *   ✅ `Game.Login`
    *   ✅ `Chat.Login`

### Q3: 修改了配置，但运行游戏没变化？
*   **原因**：你可能之前调用过 `AppConfig.Save()`，导致沙盒目录 (`PersistentDataPath`) 下生成了旧配置的存档。
*   **机制**：框架优先读取沙盒存档。
*   **解决**：点击菜单 `Tools -> AppConfig -> 清除本地存档`，强制游戏读取最新的 `StreamingAssets` 配置。

### Q4: Addressables 模式下配置怎么加载？
*   **说明**：ConfigKit 目前设计为独立模块，不依赖 Addressables。它总是使用 `UnityWebRequest` 读取。这是为了保证在资源系统初始化之前，配置系统就能先运行起来（比如 UrlConfig 可能包含资源服务器的地址）。

---

## 6. 扩展指南

### 加密支持
如果项目对安全性有要求，可以在 `ConfigCore.cs` 中扩展解密逻辑。

```csharp
// ConfigCore.cs -> LoadConfigProcess
// ... 获取 json 文本后 ...

// [扩展点] 如果是加密文件，在此处解密
if (IsEncrypted(json)) {
    json = AES.Decrypt(json, "SECRET_KEY");
}

JObject data = JObject.Parse(json);
```