# StellarFrameworkInstaller 设计文档

`StellarFrameworkInstaller.unitypackage` 是独立安装器，不是 `Assets/StellarFramework` 框架本体的一部分。它的职责是在干净 Unity 项目里先编译通过，然后安装依赖、导入框架 Core payload，并按需初始化 AA + HybridCLR 热更新能力。

## 边界

- 安装器源码位于 `Assets/StellarFrameworkInstaller`，用于生成独立安装包。
- 框架本体位于 `Assets/StellarFramework`，不依赖安装器。
- 安装器不引用 StellarFramework、UniTask、Newtonsoft.Json、Addressables 或 HybridCLR 程序集。
- 新项目先导入安装器，再通过安装器导入 `StellarFrameworkCore.unitypackage`。
- 安装完成后，项目可以保留安装器用于修复/升级，也可以移除安装器源码。

## 用户入口

```text
StellarFramework -> Installer
```

窗口包含两个主按钮：

- `安装基础框架`
- `安装 AA + HybridCLR 热更新能力`

## 基础框架安装

执行内容：

1. 检查/安装 `com.unity.nuget.newtonsoft-json`。
2. 检查/安装 UniTask。
3. 导入 `Payloads/StellarFrameworkCore.unitypackage`，如果已经存在 `Assets/StellarFramework` 则跳过。
4. 刷新 AssetDatabase。
5. 编译完成后尝试打开 `StellarFramework -> Tools Hub`。

## AA + HybridCLR 热更新能力安装

执行内容：

1. 检查/安装 `com.unity.addressables`。
2. 检查/安装 `com.code-philosophy.hybridclr`。
3. 写入 `UNITY_ADDRESSABLES` 和 `HYBRIDCLR_ENABLE`。
4. 导入 `Payloads/StellarFrameworkHotUpdateAddon.unitypackage`，如果已经存在 `Assets/GameHotUpdate` 则跳过。
5. 创建 `Assets/GameHotUpdate/Code`、`Metadata`、`Manifest`、`Source`。
6. 生成默认 `HotUpdateManifest.json`，已有文件不覆盖。
7. 通过反射创建/修复 `ResKitRuntimeSettings` 和 `AAWorkflowConfigSet`。
8. 如果 Addressables Editor 可用，通过反射应用 AA 工作流默认 Profile/Group 配置。

## 离线包

`OfflinePackages` 用于放置或选择本地依赖包：

- UniTask package directory、`.tgz` 或 `.unitypackage`
- Newtonsoft.Json package directory 或 `.tgz`
- Addressables package directory 或 `.tgz`
- HybridCLR package directory、`.tgz` 或 `.unitypackage`

当前默认优先在线安装；高级区保留本地 unitypackage 导入和备份入口。

## 安全策略

- 默认非破坏性合并：缺什么补什么，已有目录和 Manifest 不覆盖。
- 导入 Core payload 前会检测 `Assets/StellarFramework`。
- 导入 HotUpdate addon 前会检测 `Assets/GameHotUpdate`。
- 高级区提供热更新相关配置备份。

## 导出菜单

```text
StellarFramework -> Installer -> Export Installer Package
StellarFramework -> Installer -> Export Core Payload
StellarFramework -> Installer -> Export HotUpdate Addon Payload
StellarFramework -> Installer -> Build Payloads And Installer Package
```

安装器导出只包含 `Editor`、`Docs`、`Payloads`、`OfflinePackages`，不包含安装器测试。

`Build Payloads And Installer Package` 会先把当前工程的 `Assets/StellarFramework` 导出为 Core payload，再把 `Assets/GameHotUpdate` 导出为热更新 addon payload，最后把安装器自身导出成 `StellarFrameworkInstaller.unitypackage`。
