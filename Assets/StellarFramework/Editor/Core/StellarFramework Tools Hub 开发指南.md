# StellarFramework Tools Hub 开发指南

**版本**: v2.4 Integrated  
**适用框架**: StellarFramework  
**核心理念**: 开闭原则 (Open/Closed Principle) - 对扩展开放，对修改关闭。

---

## 1. 简介 (Introduction)

`StellarFrameworkTools` 是框架的统一编辑器入口（Hub）。
在 v2.3 版本中，我们对其进行了重构，从“硬编码列表”升级为**“反射 + 特性”**的自动注册机制。

这意味着：**你不再需要修改 `StellarFrameworkTools.cs` 的源码来添加新工具。** 只需要新建一个脚本，它就会自动出现在面板上。

---

## 2. 如何添加新工具 (How to Extend)

只需简单的 3 步即可创建一个新工具。

### 第一步：创建脚本
在项目的任意 `Editor` 目录下（建议在 `Assets/StellarFramework/Editor/Modules/`）创建一个新的 C# 脚本。

### 第二步：继承与标记
1.  继承 `ToolModule` 基类。
2.  添加 `[StellarTool]` 特性。
3.  重写 `OnGUI` 方法。

### 代码示例

```csharp
using StellarFramework.Editor;
using UnityEditor;
using UnityEngine;

// 参数说明: [标题, 分组名, 排序权重(越小越靠前)]
[StellarTool("我的自定义工具", "项目专用", 100)]
public class MyCustomTool : ToolModule
{
    // 可选：重写图标 (Unity 内置图标名)
    public override string Icon => "d_Favorite Icon";
    
    // 可选：工具描述
    public override string Description => "这是一个演示如何扩展面板的测试工具。";

    // 必须：绘制 UI
    public override void OnGUI()
    {
        // 1. 使用框架封装的样式方法
        Section("基础功能");
        
        if (PrimaryButton("点击执行逻辑"))
        {
            Debug.Log("Hello Stellar Framework!");
        }

        // 2. 也可以混用原生 GUILayout
        GUILayout.Space(10);
        GUILayout.Label("原生 Label 测试");
        
        if (DangerButton("危险操作 (红色按钮)"))
        {
            if (EditorUtility.DisplayDialog("警告", "确定要删除吗？", "确定", "取消"))
            {
                Debug.Log("Deleted!");
            }
        }
    }
    
    // 可选：生命周期
    public override void OnEnable() { /* 面板打开或切换到此工具时调用 */ }
    public override void OnDisable() { /* 面板关闭或切出此工具时调用 */ }
}
```

### 第三步：查看效果
回到 Unity，等待编译完成。打开 `Tools Hub` (快捷键 `Ctrl/Cmd + Shift + T`)，你会发现侧边栏多了一个 "项目专用" 分组，里面有你的工具。

---

## 3. API 速查 (API Reference)

`ToolModule` 基类提供了一些封装好的样式方法，帮助你保持 UI 风格统一。

| 方法名 | 描述 | 备注 |
| :--- | :--- | :--- |
| `Section(string title)` | 绘制一个小标题 | 带有蓝色强调色，用于区分功能块。 |
| `PrimaryButton(string label)` | 绘制主按钮 (蓝色) | 用于主要操作，返回 `bool` (是否点击)。 |
| `DangerButton(string label)` | 绘制危险按钮 (红色) | 用于删除、清空等高风险操作。 |
| `Window` | 访问主窗口实例 | 可以通过 `Window.ShowNotification()` 发送弹窗提示。 |

当然，你完全可以使用 `EditorGUILayout`、`GUILayout` 或 `EditorGUI` 编写任意复杂的编辑器界面。

---

## 4. 设计理念 (Why we do this)

### 为什么不直接写在 `StellarFrameworkTools.cs` 里？
在旧版本中，每增加一个工具，都需要去修改主窗口的 `InitializeModules` 方法。这导致：
1.  **核心代码膨胀**：主窗口文件会越来越大，难以维护。
2.  **耦合严重**：业务逻辑（如“生成配置表”）和框架逻辑（“显示窗口”）混在一起。
3.  **难以升级**：如果用户想升级框架核心，必须小心翼翼地保留自己添加的代码。

### 现在的做法有什么好处？
1.  **解耦**：主窗口只负责“显示”，不负责“逻辑”。
2.  **热插拔**：删掉某个工具的脚本，面板上就自动消失；加上脚本，自动出现。
3.  **易于分发**：你可以把一个工具写成一个独立的 `.cs` 文件发给同事，他拖进工程就能用，不需要教他改代码。

---

## 5. 常见坑点 (Pitfalls)

### Q1: 写了脚本，但在面板里找不到？
*   **检查 1**：是否继承了 `ToolModule`？
*   **检查 2**：是否添加了 `[StellarTool(...)]` 特性？
*   **检查 3**：脚本是否放在了 `Editor` 文件夹内？（或者放在了包含 Editor 平台的 Assembly Definition 中）。如果在 Runtime 程序集里，`UnityEditor` 命名空间会报错。

### Q2: 排序是怎么排的？
*   首先按 **Group (分组名)** 聚类。
*   分组的显示顺序取决于该组内 **Order 最小** 的那个工具。
*   组内工具按 `Order` 从小到大排序。

### Q3: 报错 `The type or namespace name 'ToolModule' could not be found`
*   **原因**：你的脚本可能没有引用命名空间。
*   **解决**：在文件头部添加 `using StellarFramework.Editor;`。

### Q4: 按钮点击没反应？
*   **原因**：`OnGUI` 是每帧调用的。
*   **解决**：确保你的逻辑写在 `if (GUILayout.Button(...))` 的大括号内部。

---

## 6. 最佳实践 (Best Practices)

1.  **文件归档**：建议将所有扩展工具放在 `Assets/Editor/Tools/` 或类似目录下，保持项目整洁。
2.  **分组命名**：尽量复用现有的分组（如“常用工具”、“生产力”），避免侧边栏分组过多导致杂乱。
3.  **耗时操作**：如果在 `OnGUI` 里做耗时操作（如遍历几万个文件），会导致编辑器卡顿。请配合 `EditorUtility.DisplayProgressBar` 使用。
