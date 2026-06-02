# AA 本地内置模式教学

本地内置 AA 模式的定位是：用 Addressables 替代传统 AssetBundle，资源、catalog、bundle、`HotUpdateManifest.json` 都随 Player 放进 `StreamingAssets`。

这个模式不做远端版本对比，也不从服务器下载资源。它适合随包资源管理，也适合开发期快速验证 HybridCLR `.dll.bytes` 和 Manifest 是否同一批。

## 什么时候选它

- 你只是想用 AA 替代 AB，统一资源加载。
- 资源和热更 DLL 随 Player 一起发布。
- 正式内容变化时，可以重新打 Player。
- 开发期想把编辑器新构建的 AA 覆盖到旧 Player 的 `StreamingAssets` 里做验证。

如果目标是“旧 Player 不重打包，从 D 盘、HTTP、CDN 拉新资源”，请选择 `远端热更 AA`。

## 第一次使用

打开 `StellarFramework Tools -> 热更新 -> AA 配置与发布`。

1. 进入 `配置列表`。
2. 选择默认配置 `本地内置 AA`。
3. 进入 `本地内置 AA` 页签。
4. 确认 `本地 AA 输出目录` 是：

```text
[StreamingAssets]/aa
```

展开后实际会指向：

```text
Assets/StreamingAssets/aa
```

5. 点击 `一键本地内置构建`。

工具会自动执行：

1. 应用本地 Addressables Profile。
2. 导出 HybridCLR `.dll.bytes`。
3. 生成 `HotUpdateManifest.json`。
4. Build Addressables。
5. 写入运行时设置，让 Manifest 从 StreamingAssets 加载。
6. 校验输出目录里的 Manifest、settings、catalog、bundle 是否齐全。

## 默认资源组

工程默认只保留三类 Addressables Group，方便新手判断资源应该放哪：

```text
Built In Data
StellarFramework Local Resources
StellarFramework Hot Update Code
```

`StellarFramework Local Resources` 用来放普通随包资源和示例资源。当前示例里保留了：

```text
Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle/TestCapsule_AB.prefab
Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Addressables/TestSphere_AA.prefab
Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Resources/ResKitTest/TestCube_Res.prefab
```

`StellarFramework Hot Update Code` 只放 HybridCLR 热更代码和 AOT metadata，例如：

```text
Assets/GameHotUpdate/Code/HotUpdate.dll.bytes
Assets/GameHotUpdate/Metadata/*.dll.bytes
```

普通资源不要放进热更代码组，热更 DLL 也不要放进本地资源组。这样构建和排查时更容易看清楚。

## 打 Player

本地内置 AA 构建完成后，再打 Player。

Player 里应该包含：

```text
<Game>_Data/StreamingAssets/aa/
  HotUpdateManifest.json
  Windows/
    settings.json
    catalog.json
    StandaloneWindows64/
      *.bundle
```

正式发布时，如果这些内容发生变化，正常流程是重新发布 Player。

## 覆盖测试 Player

这个功能只用于开发期测试，不等同于正式远端热更。

流程：

1. 修改热更代码或资源。
2. 重新点击 `一键本地内置构建`。
3. 在 `测试 Player 根目录` 选择已经打出来的 Player 根目录。
4. 点击 `覆盖测试 Player`。
5. 重启旧 Player。

工具只会覆盖：

```text
<PlayerRoot>/<Game>_Data/StreamingAssets/aa/
```

覆盖时会复制整套 AA 输出，包括：

```text
HotUpdateManifest.json
Windows/settings.json
Windows/catalog.json
Windows/StandaloneWindows64/*.bundle
```

不要只复制 bundle 或只复制 Manifest，否则容易出现 SHA mismatch。

## 怎么判断跑的是本地内置

Player 日志里看到类似内容：

```text
Manifest=StreamingAssets:<Player>/StreamingAssets/aa/HotUpdateManifest.json
```

说明当前走的是本地内置 AA 模式。

## 常见问题

`Hot update dll SHA256 mismatch`

说明 Manifest 和实际加载到的 `HotUpdate.dll.bytes` 不是同一批。重新点击 `一键本地内置构建`，然后用 `覆盖测试 Player` 覆盖整套目录。

`Manifest=File:file:///D:/...`

说明当前不是本地内置模式，而是远端模拟模式。请检查 `AA 配置与发布` 当前选中的配置，以及运行时设置里的 Manifest 地址。
