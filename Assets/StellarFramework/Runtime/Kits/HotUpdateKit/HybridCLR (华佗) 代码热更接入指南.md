# HybridCLR (华佗) 代码热更接入指南

**适用**: StellarFramework HotUpdate 模块

---

## 1. 核心理念 (Design Philosophy)
为了保持框架的解耦，StellarFramework 提供了 `HybridCLRHook` 工具类。

*   **宏隔离**：通过 `#if HYBRIDCLR_ENABLE` 隔离底层 API，未安装插件时不会导致编译报错。
*   **IO 解耦**：框架不强制指定 DLL 的下载方式。业务层只需将获取字节流的逻辑通过 `Func<string, UniTask<byte[]>>` 委托注入给 Hook。

---

## 2. 环境配置 (Setup)
1.  在项目中安装并配置好 `HybridCLR` 插件。
2.  在 `Project Settings -> Player -> Scripting Define Symbols` 中添加宏：`HYBRIDCLR_ENABLE`。
3.  在 `HybridCLR -> Settings` 中配置热更程序集（如 `HotUpdate.dll`）。
4.  生成 AOT DLL 与热更 DLL，并将它们作为 `TextAsset` 或 `Bytes` 文件打包到资源系统中。

---

## 3. 标准接入工作流 (Workflow)

在游戏的最早入口（如 `GameEntry.cs`，该脚本必须位于 AOT 主工程中），执行以下流程：

### 3.1 编写入口代码
```csharp
using UnityEngine;
using Cysharp.Threading.Tasks;
using StellarFramework.HotUpdate;
using StellarFramework.Res;

public class GameEntry : MonoBehaviour
{
    private IResLoader _loader;

    private async void Start()
    {
        // 1. 初始化资源系统 (以 Addressables 为例)
        _loader = ResKit.Allocate<AddressableLoader>();

        // 2. 检查并下载热更资源 (包含 DLL 和 美术资源)
        await CheckAndDownloadUpdates();

        // 3. 加载 AOT 补充元数据 (解决泛型实例化报错)
        await HybridCLRHook.LoadMetadataForAOTAssembliesAsync(ProvideDllBytesAsync);

        // 4. 读取热更程序集 DLL
        byte[] hotUpdateBytes = await ProvideDllBytesAsync(HybridCLRHook.HotUpdateAssemblyName);

        // 5. 跨域跳转，将控制权移交热更域
        HybridCLRHook.LoadAndStartHotUpdateAssembly(hotUpdateBytes);
    }

    /// <summary>
    /// 核心：提供给 Hook 的委托，根据 DLL 名字获取字节流
    /// </summary>
    private async UniTask<byte[]> ProvideDllBytesAsync(string dllName)
    {
        string address = $"dlls/{dllName}.bytes";
        var textAsset = await _loader.LoadAsync<TextAsset>(address);
        if (textAsset != null)
        {
            return textAsset.bytes;
        }
        Debug.LogError($"无法加载 DLL 资产: {address}");
        return null;
    }

    private async UniTask CheckAndDownloadUpdates()
    {
        // 调用 AddressableHotUpdateManager 检查更新...
        await UniTask.Yield();
    }
}
```

### 3.2 热更域入口规范
在热更工程中，创建对应的入口类。类名和方法名必须与 `HybridCLRHook` 中的配置一致。

```csharp
namespace HotUpdate
{
    public class HotUpdateMain
    {
        // 必须是 public static
        public static void Main()
        {
            Debug.Log("成功进入热更域！");
            // 在这里初始化 StellarFramework 的核心架构
            // GameApp.Interface.Init();
        }
    }
}
```

---

## 4. 进阶配置 (Configuration)
如果热更入口类名不同，或者 AOT 补充元数据列表有变化，可以在调用 Hook 之前修改静态配置：

```csharp
// 修改热更入口配置
HybridCLRHook.HotUpdateAssemblyName = "MyGameLogic.dll";
HybridCLRHook.HotUpdateEntryClass = "MyGameLogic.AppStart";
HybridCLRHook.HotUpdateEntryMethod = "Run";

// 修改 AOT 元数据列表 (根据 HybridCLR 生成的列表填入)
HybridCLRHook.AOTMetaAssemblyFiles = new List<string>
{
    "mscorlib.dll",
    "System.dll",
    "System.Core.dll",
    "UniTask.dll",
    "Newtonsoft.Json.dll" 
};
```

---

## 5. 常见问题 (Troubleshooting)

### Q1: 报错 `ExecutionEngineException: metadata type not match`
*   **原因**：在热更工程中调用了 AOT 工程中未实例化的泛型类。
*   **解决**：确保正确生成了 AOT 补充元数据 DLL，并将其名字加入了 `HybridCLRHook.AOTMetaAssemblyFiles` 列表中，且在跳转前成功执行了 `LoadMetadataForAOTAssembliesAsync`。

### Q2: 找不到入口类或方法
*   **原因**：命名空间、类名或方法名拼写错误；或者方法不是 `public static`。
*   **解决**：检查 `HotUpdateEntryClass` 是否包含了完整的命名空间。