# StellarToolsHub / 扩展开发手册

这份文档面向要新增编辑器工具的维护者。普通使用者请读 [ToolsHub 使用手册](StellarToolsHub-使用手册-Guide.md)，源码阅读请读 [ToolsHub 源码文档](StellarToolsHub-源码文档-Guide.md)。

## 目标

ToolsHub 的目标是把框架工具集中在一个窗口里，并让新工具通过 `[StellarTool]` 自动注册。新增工具时不需要改主窗口菜单列表，只要新增一个 `ToolModule` 派生类。

## 最小模块

```csharp
using UnityEditor;

namespace StellarFramework.Editor.Modules
{
    [StellarTool("我的工具", "常用工具", 10)]
    public sealed class MyToolModule : ToolModule
    {
        public override string Icon => "d_Toolbar Plus";
        public override string Description => "一句话说明这个工具解决什么问题。";

        public override void OnGUI()
        {
            EditorGUILayout.LabelField("工具内容", EditorStyles.boldLabel);
        }
    }
}
```

## 核心约定

- 继承 `ToolModule`。
- 用 `[StellarTool(displayName, category, order)]` 声明名称、分组和排序。
- `Description` 写给右侧工具说明和搜索理解。
- `OnGUI()` 只画当前工具的界面。
- 有状态的工具在 `OnEnable()` 读取，在修改后保存到 ScriptableObject、EditorPrefs 或项目资产。

## 常用生命周期

- `OnEnable()`：刷新配置、扫描资产、初始化缓存。
- `OnDisable()`：释放临时引用或保存编辑器状态。
- `OnGUI()`：绘制界面。
- `Refresh()` 或自定义按钮：手动重新扫描。

## UI 写法建议

- 主动作放顶部或当前流程块。
- 风险动作加确认框。
- 路径字段旁边提供打开目录或 Ping 资产按钮。
- 长流程要显示最近一次结果，方便新人判断有没有成功。
- 不要把构建、发布、校验塞进一个无说明的小按钮。

## 常见模块类型

- 使用型工具：如 `Quick Start`、`文档中心 (Docs)`、`AA 配置与发布`。
- 构建型工具：如 `资源打包 (AssetBundle)`、`HybridCLR DLL 导出`。
- 诊断型工具：如 `ResKit 资源审计`、`EventKit 链路追踪`。
- 生成型工具：如 UIKit 绑定生成、Singleton 注册表生成。

## 扩展注意事项

- 不要在 `OnGUI()` 中做长耗时扫描，改成按钮触发或缓存结果。
- 不要写死用户本机绝对路径，路径配置应通过资产或工具设置保存。
- 不要在工具里直接修改 Runtime 公共 API。
- 生成代码必须写到 `Generated` 或明确的输出目录。
- 发布、删除、覆盖等动作必须检查路径安全。

## 测试入口

- 文档策略：`Tests/EditMode/FrameworkValidation/QuickStartCatalogPolicyTests.cs`
- 工具入口策略：`Tests/EditMode/FrameworkValidation/OnboardingSurfacePolicyTests.cs`
- AA 发布工具：`Tests/EditMode/FrameworkValidation/AAHotUpdatePublishToolTests.cs`
- 修改 ToolsHub 后至少刷新 Unity，确认 Console 无编译错误，并打开 `StellarFramework -> Tools Hub` 检查工具是否出现。
