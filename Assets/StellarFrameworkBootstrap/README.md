# StellarFramework 单包安装说明

这个引导包用于干净 Unity 工程。

## 使用步骤

1. 只需导入一个包：`StellarFramework.unitypackage`
2. 打开 `StellarFramework -> 安装 -> 单包安装器`
3. 点击 `一键安装 StellarFramework`
4. 安装器会自动安装依赖，并导入完整 StellarFramework 框架

## 自动处理内容

- 安装 `UniTask`
- 安装 `Newtonsoft.Json`
- 安装 `Addressables`
- 安装 `HybridCLR`
- 自动导入内嵌的完整框架 payload

## 兼容性

- Unity 版本：面向 Unity 2022.3 LTS 和 Unity 6000.x
- 渲染管线：框架 Runtime 支持 Built-in、URP、HDRP
- 文档：安装完成后通过 `FrameworkDoc` 和 Tools Hub 的 Quick Start 按需查阅各 Kit 的完整接入说明
