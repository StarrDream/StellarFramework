# StellarFramework 单包分发设计

## 目标

对外只提供一个 `StellarFramework.unitypackage`。用户在干净 Unity 工程中只需要导入这一个包，然后通过引导窗口一键安装依赖并导入完整 StellarFramework 框架内容。

## 现状

当前分发方式拆成三部分：

- `StellarFramework-Bootstrap.unitypackage`
- `StellarFramework-Base.unitypackage`
- `StellarFramework-FullHotUpdate.unitypackage`

优点是 bootstrap 本身没有外部依赖，可以先进入工程再补 UPM 包依赖。缺点是用户仍然要处理多个包，安装体验不够收口。

## 方案

采用“单包引导 + 内嵌完整框架 payload”方案：

1. 导出流程先生成完整框架 payload 包。
2. 导出流程把 payload 包写入 `Assets/StellarFrameworkBootstrap/Payloads/`，作为 bootstrap 的内嵌资源。
3. 导出流程再生成对外唯一发布包 `StellarFramework.unitypackage`。
4. 用户导入该包后，只会得到 bootstrap 安装器和内嵌 payload 资源。
5. 安装器先安装缺失的 UPM 依赖，再从内嵌 payload 解出临时 `.unitypackage` 并自动导入。

## 安装链路

### 用户视角

1. 导入 `StellarFramework.unitypackage`
2. 打开 `StellarFramework/单包安装器`
3. 点击“一键安装 StellarFramework”
4. 等待依赖安装与框架导入完成

### 编辑器内部顺序

1. 检查并安装：
   - `com.cysharp.unitask`
   - `com.unity.nuget.newtonsoft-json`
   - `com.unity.addressables`
   - `com.code-philosophy.hybridclr`
2. 从 `Assets/StellarFrameworkBootstrap/Payloads` 读取内嵌 payload
3. 写入临时目录，恢复为 `.unitypackage`
4. 调用 `AssetDatabase.ImportPackage`
5. 记录中文状态和错误信息

## 代码边界

### `StellarFrameworkPackagePublisher`

- 负责生成完整 payload
- 负责把 payload 写入 bootstrap 目录
- 负责导出对外唯一总包
- 负责写出中文分发说明文件

### `StellarFrameworkBootstrapInstaller`

- 去掉“基础版 / 完整版”二选一
- 改为统一的一键安装入口
- 新增内嵌 payload 定位、解包、导入逻辑

### `StellarFrameworkBootstrapWindow`

- 菜单、标题、按钮、提示文案全部中文
- 只保留一个安装动作

### `README`

- 改成中文说明
- 强调“只需导入一个包”

## 错误处理

- 缺少 payload 文件：提示重新使用导出器生成单包
- payload 临时写出失败：提示检查磁盘权限
- UPM 依赖安装失败：保留错误信息并停止后续导入
- `.unitypackage` 导入路径无效：提示 payload 损坏或导出流程异常

## 测试

更新 `PackagePublisherPolicyTests` 覆盖：

- 新的唯一包名 `StellarFramework.unitypackage`
- 中文菜单与中文窗口文案
- 不再暴露 `Base` / `FullHotUpdate` 交互入口
- 发布说明文档为中文
- 安装器包含 payload 导入逻辑

## 非目标

- 本次不把整个框架迁移为 UPM 包
- 本次不删除内部的完整 payload 导出能力，因为单包流程仍然依赖它
