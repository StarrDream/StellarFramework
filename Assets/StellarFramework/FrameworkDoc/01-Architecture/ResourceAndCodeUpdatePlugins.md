# ResKit 资源与热更新扩展

StellarFramework 把资源加载、资源内容更新和代码运行时拆成三个可注册的 ResKit 能力。框架提供的默认热更新组合是 **YooAsset + HybridCLR**：YooAsset 准备当前内容版本，HybridCLR 从同一 ResKit 资源 Scope 读取并运行热更代码。项目可以替换任一部分，也可以使用自己的组合。

## 模块边界

| 能力 | ResKit 契约 | 框架实现 | 职责 |
| --- | --- | --- | --- |
| 资源加载 | `IResLoader`、`ResScope` | ResKit.Resources、ResKit.AssetBundle、ResKit.Addressables、ResKit.YooAsset | 通过统一 Scope 加载和释放资源 |
| 资源内容更新 | `IResContentUpdateProvider<TOptions, TProgress, TResult>` | ResKit.ContentUpdate.YooAsset | 检查内容版本、更新清单、下载和缓存资源 |
| 代码运行时 | `IResCodeUpdateProvider<TOptions, TResult>` | `HybridCLRResCodeUpdateProvider` | 从应用传入的 `ResScope` 读取代码载荷，校验、加载并进入热更代码 |
| 构建与发布 | Editor 构建/发布适配器 | HybridCLR 代码导出 + YooAsset 内容构建 | 生成与所选运行时格式匹配的制品 |

`ResKit.Core` 只定义中立契约和注册入口，不带 Resources、AssetBundle、Addressables 或 YooAsset 的加载器实现。每个加载后端都是单独导出的 Adapter；YooAsset 内容更新 Provider 和 HybridCLR 代码更新 Provider 也各自独立。项目可以只选一种后端，或把多个后端组合进同一导出包。

## 默认组合的启动流程

常规项目在启动层显式安排顺序。资源更新 Provider 不会自动运行，HybridCLR Provider 也不会自行选 Loader 或创建 Scope。

```text
注册 YooAsset 加载器和内容更新 Provider
  -> ResKit 获取 YooAsset 内容更新 Provider
  -> 更新 ResourcePackage 内容
  -> 项目创建 YooAsset ResScope
  -> ResKit 获取 HybridCLR 代码 Provider
  -> Provider 经该 Scope 读取 Manifest / DLL / AOT metadata
  -> 校验并进入热更程序集
```

### YooAsset + HybridCLR 示例

项目通常将这一段放在自己的 `GameStartup`、`GameUpdateService` 或首屏流程中。网络地址、弹窗、失败重试与离线策略由项目决定。

```csharp
using StellarFramework.Res.CodeUpdate.HybridCLR;
using StellarFramework.Res;

YooAssetResKitInstaller.InstallLoader();
YooAssetContentUpdateInstaller.Install();

var contentUpdater = ResKit.GetContentUpdateProvider<
    YooAssetContentUpdateOptions,
    YooAssetContentUpdateProgress,
    YooAssetContentUpdateResult>(YooAssetResContentUpdateProvider.ProviderId);

YooAssetContentUpdateResult content = await contentUpdater.UpdateAsync(
    new YooAssetContentUpdateOptions
    {
        PackageName = "DefaultPackage",
        MainHostServer = "https://cdn.example.com/game/android/DefaultPackage",
        FallbackHostServer = "https://backup.example.com/game/android/DefaultPackage"
    },
    downloadProgress,
    cancellationToken);

if (!content.Success)
{
    // 项目决定重试、进入离线模式或停止启动。
    return;
}

HotUpdateSettings settings = HotUpdateSettings.LoadOrCreateDefault();
var codeUpdater = ResKit.GetCodeUpdateProvider<HotUpdateSettings, HybridCLRUpdateResult>(HybridCLRResCodeUpdateProvider.ProviderId);

using ResScope codeAssets = ResKit.CreateCustomScope(
    YooAssetResKitInstaller.LoaderKey,
    "Startup.CodeUpdate");

HybridCLRUpdateResult code = await codeUpdater.RunAsync(
    settings,
    codeAssets,
    codeProgress,
    cancellationToken);

if (!code.Success)
{
    // 项目决定展示错误、重试或退出。
    return;
}
```

`YooAssetResKitInstaller.InstallLoader()` 只注册 YooAsset Loader；`YooAssetContentUpdateInstaller.Install()` 单独注册内容更新 Provider。内容更新成功后，YooAsset Package 才可供 Loader 加载；项目自行创建 Scope，并在 `using` 作用域结束时释放。HybridCLR 扩展在 Player 启动和 Editor 域加载时向 ResKit 注册代码 Provider。

`Hot Update Full` 是框架提供的完整默认导出组合，包含上述运行时扩展、ResKit 基础设施、AssetsMap、ToolsHub 和对应编辑器工具。只做本地资源加载的项目仍可选择 `ResKit.Core` 或其他单项 Profile。

首次试跑不需要先准备 CDN：热更发布器的“开发（本机）”环境默认将包写入项目内的 `BuildArtifacts/HotUpdate/Local`，并用对应的 `file:///` 本地目录作为读取地址。Unity Editor 或同一台 Windows 电脑上的 Player 可以直接从该磁盘目录读取；Android 设备无法访问开发机的 Windows 文件路径，需要使用设备可达的 HTTP 服务。“预发布”和“正式环境”的地址由项目显式配置；正式环境发布要求 HTTPS。要验证 HTTP Range、断点续传或真实 CDN，再使用验证工程的 HTTP 测试流程。

## 如何替换或增加 Provider

ResKit 按稳定字符串 ID 注册 Provider。项目可先保留默认组合中的其他部分，只替换自己要变化的能力。

### 接入另一种资源内容更新服务

实现 `IResContentUpdateProvider`，再在自有 Installer 中注册：

```csharp
public sealed class CompanyContentProvider :
    IResContentUpdateProvider<CompanyUpdateOptions, float, CompanyUpdateResult>
{
    public UniTask<CompanyUpdateResult> UpdateAsync(
        CompanyUpdateOptions options,
        IProgress<float> progress = null,
        CancellationToken cancellationToken = default)
    {
        // 调用公司 CDN / 内容服务，更新其管理的内容版本。
        return CompanyContentApi.UpdateAsync(options, progress, cancellationToken);
    }
}

public static class CompanyContentInstaller
{
    public static void Install()
    {
        ResKit.RegisterContentUpdateProvider<CompanyUpdateOptions, float, CompanyUpdateResult>(
            "Company.Content",
            new CompanyContentProvider());
    }
}
```

项目启动时改为按 `Company.Content` 获取 Provider。自定义服务可以和 YooAsset Loader 配合，但只有在它能准备 YooAsset 所需的 Package 文件布局和版本数据时，这种组合才成立。

### 接入另一种代码运行时

代码 Provider 必须接收应用持有的 `ResScope`。例如项目可以用自研脚本虚拟机替换 HybridCLR，同时继续用 YooAsset 更新和读取脚本包：

```csharp
public sealed class CompanyScriptProvider :
    IResCodeUpdateProvider<CompanyScriptOptions, CompanyScriptResult>
{
    public async UniTask<CompanyScriptResult> RunAsync(
        CompanyScriptOptions options,
        ResScope resources,
        IProgress<float> progress = null,
        CancellationToken cancellationToken = default)
    {
        TextAsset payload = await resources.Loader.LoadAsync<TextAsset>(
            options.PayloadKey,
            cancellationToken);
        if (payload == null)
            return CompanyScriptResult.Failed("Script payload was not found.");

        return await CompanyScriptRuntime.LoadAsync(payload.bytes, progress, cancellationToken);
    }
}

ResKit.RegisterCodeUpdateProvider<CompanyScriptOptions, CompanyScriptResult>(
    "Company.ScriptRuntime",
    new CompanyScriptProvider());

using ResScope scriptAssets = ResKit.CreateCustomScope("YooAsset", "Startup.ScriptRuntime");
var scriptRuntime = ResKit.GetCodeUpdateProvider<CompanyScriptOptions, CompanyScriptResult>(
    "Company.ScriptRuntime");
CompanyScriptResult script = await scriptRuntime.RunAsync(options, scriptAssets, progress, token);
```

这段示例中的 `CompanyScriptRuntime` 和结果类型由接入方实现。ResKit 负责 Scope 与 Provider 生命周期边界，不替自研运行时规定字节码格式或执行策略。

### 接入另一种资源 Loader

实现 `IResLoader` 并注册工厂，之后任一代码 Provider 都可以使用该 Loader 创建的 Scope：

```csharp
ResKit.RegisterLoader("Company.Bundle", request =>
{
    var loader = PoolKit.Allocate<CompanyBundleLoader>();
    loader.Configure(companyBundleConfig);
    return loader;
});

using ResScope codeAssets = ResKit.CreateCustomScope("Company.Bundle", "Startup.CodeUpdate");
```

Loader 的核心工作是实现 ResKit 的加载、释放和批量加载契约。参考 `ResKit.AssetBundle` 或 `ResKit.YooAsset` 的 adapter 结构，为自有 Loader 单独提供程序集、安装器、配置、测试与 Catalog Profile；ResKit.Core 无需增加第三方 SDK 引用或新的后端枚举值。

## 组合约束

Provider 可以自由替换和组合，制品格式仍需匹配：

- 内容更新 Provider 必须把所需文件放到被 Loader 寻址的位置。
- 代码 Provider 的 Manifest、程序集、metadata 格式必须由选定的代码构建/导出工具生成。
- 一个版本中的 Manifest、代码 DLL、AOT metadata 需要作为同一内容版本读取。
- Package、平台、版本、地址规则必须由内容 Provider、Loader、代码 Provider 和 Publisher 共同遵守。

仅替换运行时 Provider 不会自动生成新格式的发布制品。自定义代码格式还要提供匹配的构建、导出和发布验证适配器。

## 单项与组合导出

| 目标 | 选择 |
| --- | --- |
| Resources 加载 | `reskit.resources` |
| AssetBundle（AB）加载 | `reskit.assetbundle` |
| Addressables（AA）加载 | `reskit.addressables` |
| YooAsset 加载 | `reskit.yooasset` |
| YooAsset 资源内容更新 | `reskit.contentupdate.yooasset` |
| HybridCLR 代码运行时 | `reskit.codeupdate.hybridclr` |
| 两类 Provider 的 Editor 工具 | 对应 `.tools` Profile |
| 框架默认完整热更流程 | `hotupdate.full`（Hot Update Full） |

Catalog Profile 通过依赖闭包补齐 ResKit、PoolKit、LogKit 和 ToolsHub。Resources、AB、AA、YooAsset Loader、内容更新 Provider、HybridCLR 代码 Provider 都能分别选择；勾选多个加载 Adapter 时，公共依赖会合并去重。常用 Resources、AB、AA 和 Resources + AB 配置可在 Package Exporter 的 ResKit 推荐组合中直接导出；其他组合也可在 Adapter 列表中多选。

单独使用一个后端时，可以在启动层设为默认后端：

```csharp
// 只使用 AssetBundle：
ResKit.Configure(ResLoadBackend.AssetBundle);

// 只使用 Addressables（AA）：
ResKit.Configure(
    ResLoadBackend.Custom,
    defaultCustomLoaderKey: AddressablesResKitInstaller.LoaderKey);
```

同时导出 Resources + AB 时，两个 Loader 都会注册。可以给不同资源指定不同 Scope：

```csharp
using ResScope resourcesScope = ResKit.CreateScope(ResLoadBackend.Resources, "UI icons");
using ResScope assetBundleScope = ResKit.CreateScope(ResLoadBackend.AssetBundle, "level content");
```

不调用 `Configure` 时，默认资源后端为 Resources；如果只导出 AB 或 AA，请在启动层显式选择上面的后端，或配置 `ResKitRuntimeSettings` 中的默认加载方式。
