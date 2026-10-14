# 按需导出 Kit

从 Unity 菜单 **StellarFramework → Export** 打开导出窗口。所有已安装的 Kit、资源后端、适配层与编辑器工具使用同一份选择清单，可以跨 Kit 自由组合。

1. 展开左侧 Kit，勾选需要的功能。搜索框可以查找 Kit 名称、Resources、热更等关键词。
2. 查看右侧清单：“你选择的功能”是主动选择项，“自动补齐的必需依赖”是这些功能运行所必需的项。被自动选中的依赖不能单独取消；移除依赖它的功能后即可取消。
3. 编辑器工具在每个 Kit 下单独勾选。未勾选时不会为了“完整体验”自动带入 ToolsHub。
4. 使用文档可以单独决定是否附带。点击“选择保存位置并导出”，指定文件位置和名称。
5. 在业务项目中导入 unitypackage。安装器先安装清单中的第三方包，再导入 Kit 源码。等待 Unity 编译完成后使用相应 Kit 的文档接入。

快捷方案只是替你勾选一组功能。选择“使用方案”会替换当前选择，“加入当前选择”会叠加；应用后仍然可以继续修改组合。

| 需求 | 勾选内容 |
| --- | --- |
| 只用 Resources | Resources 资源加载 |
| 只用 AssetBundle | AssetBundle 资源加载；构建工具按需另选 |
| 只用 Addressables | Addressables 资源加载 |
| Resources 与 AssetBundle 同时使用 | 同时勾选两个加载后端 |
| YooAsset 加载与资源热更 | YooAsset 资源加载、资源热更（YooAsset） |
| 只需要构建 YooAsset 资源 | YooAsset 资源构建工具；运行时加载和内容更新按需另选 |
| 再加入代码热更 | 在所需资源后端上加入代码热更（HybridCLR） |
| HTTP 请求和图片下载，不安装 UGUI | HttpKit 请求与图片下载 |
| 给 Image / RawImage 绑定下载图片 | 再勾选 UGUI 图片绑定 |

源码、文档和真实依赖按选择合并去重。框架测试代码、验证场景、测试 DLL 和维护工具不进入 Kit 安装包。当前固定版本的 YooAsset / Addressables 编辑器会引用 NUnit，对应安装包会自动安装最小 NUnit 兼容依赖，不安装框架 Test Runner。

“单文件源码与完整框架安装包”提供 Architecture.cs、Extensions.cs 与完整安装包入口。单文件架构导出不依赖任何 Kit。
