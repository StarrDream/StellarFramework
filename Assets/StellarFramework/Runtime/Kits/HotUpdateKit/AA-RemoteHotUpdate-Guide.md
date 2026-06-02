# AA 远端热更模式教学

远端热更 AA 模式的定位是：旧 Player 不重打包，通过远端 `HotUpdateManifest.json`、Addressables catalog/hash、bundle 更新资源和 HybridCLR 代码。

远端可以是真 HTTP/CDN，也可以先用本机目录模拟，例如 `file:///D:/HotUpdate/StandaloneWindows64`。

## 什么时候选它

- 你希望旧 Player 不重打包就能更新资源或热更 DLL。
- 资源版本由远端 catalog/hash 管理。
- `HotUpdateManifest.json` 也在远端，负责提供热更 DLL key、SHA256、入口类和 AOT metadata 列表。
- 本地包只带基础资源，后续版本走远端发布目录或服务器。

如果只是 AA 替代 AB、资源随包发布，请选择 `本地内置 AA`。

## 第一次使用 D 盘模拟远端

打开 `StellarFramework Tools -> 热更新 -> AA 配置与发布`。

1. 进入 `配置列表`。
2. 选择默认配置 `远端热更 AA`。
3. 进入 `远端热更 AA` 页签。
4. 确认 `远端发布目录` 是：

```text
D:/HotUpdate/[BuildTarget]
```

展开后实际会指向：

```text
D:/HotUpdate/StandaloneWindows64
```

5. `远端加载路径/URL` 可以留空。

留空时工具会自动推导：

```text
file:///D:/HotUpdate/StandaloneWindows64
```

6. 点击 `一键远端热更发布`。

工具会自动执行：

1. 应用远端 Addressables Profile。
2. 开启远端 catalog。
3. 导出 HybridCLR `.dll.bytes`。
4. 生成 `HotUpdateManifest.json`。
5. Build Addressables。
6. 发布整套 AA 文件到 `D:/HotUpdate/StandaloneWindows64`。
7. 写入运行时 Manifest 地址。
8. 校验 Manifest、catalog、hash、bundle 是否同一批。

## 默认资源组

工程默认只保留三类 Addressables Group：

```text
Built In Data
StellarFramework Local Resources
StellarFramework Hot Update Code
```

远端热更发布时，工具会按当前配置把需要参与构建的 Bundled Group 切到远端路径。日常只需要理解：

- `StellarFramework Local Resources`：普通资源和示例资源。
- `StellarFramework Hot Update Code`：HybridCLR `HotUpdate.dll.bytes` 和 AOT metadata。

如果你新建业务资源组，名字建议按业务写清楚，例如 `Game UI Remote Resources` 或 `Chapter01 Local Resources`。不要再用 `art`、`test`、长串路径名这类新人看不懂的组名。

## D 盘目录应该有什么

发布完成后应该看到：

```text
D:/HotUpdate/StandaloneWindows64/
  HotUpdateManifest.json
  catalog_*.json
  catalog_*.hash
  *.bundle
```

这些文件必须同一批。不要手动只替换其中一部分。

## 切换到 HTTP/CDN

有网站或 CDN 后，把 `远端加载路径/URL` 改成：

```text
https://example.com/hotupdate/[BuildTarget]
```

实际运行时会推导 Manifest 地址：

```text
https://example.com/hotupdate/StandaloneWindows64/HotUpdateManifest.json
```

然后把发布目录中的整套文件上传到服务器对应目录。

## 旧 Player 怎么生效

远端热更的关键是：旧 Player 内置的运行时设置必须知道远端 Manifest 地址。

所以第一次接入远端热更逻辑时，需要打一次带新运行时配置的 Player。之后每次热更只需要：

1. 修改热更代码或资源。
2. 生成 HybridCLR DLL。
3. 点击 `一键远端热更发布`。
4. 重启旧 Player。

旧 Player 会从远端 Manifest 和 Addressables catalog 读取新内容。

## 怎么判断跑的是远端热更

D 盘模拟远端时，日志应该类似：

```text
Manifest=File:file:///D:/HotUpdate/StandaloneWindows64/HotUpdateManifest.json
```

HTTP/CDN 时，日志应该类似：

```text
Manifest=Http:https://example.com/hotupdate/StandaloneWindows64/HotUpdateManifest.json
```

如果看到：

```text
Manifest=StreamingAssets:...
```

说明当前仍在走本地内置或 fallback。请检查：

- 当前 ToolHub 选中的是不是 `远端热更 AA`。
- 是否已经点击 `一键远端热更发布` 写入运行时设置。
- `开发期允许 StreamingAssets 兜底` 是否被打开。
- 旧 Player 是否是在写入远端 Manifest 地址后重新打出来的。

## 常见问题

`Hot update dll SHA256 mismatch`

说明 Manifest 和实际加载到的 `HotUpdate.dll.bytes` 不是同一批。重新点击 `一键远端热更发布`，确保远端目录里 Manifest、catalog、hash、bundle 全部来自同一次构建。

只把 `HotUpdateManifest.json` 放到 D 盘可以吗？

不可以。Manifest 只是入口配置，真正资源和 DLL 仍然由 Addressables catalog/bundle 加载。D 盘远端模拟必须放整套 AA 输出。
