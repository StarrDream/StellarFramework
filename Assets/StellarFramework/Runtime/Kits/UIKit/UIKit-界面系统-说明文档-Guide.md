# UIKit / 界面系统说明文档

UIKit 是 UI 唯一入口，负责面板加载、打开、关闭、预加载、页面栈、层级和运行时快照。业务层不直接 Instantiate UI Prefab，而是通过 UIKit 统一打开。

## 入口 API

- `UIKit.Instance.InitAsync()`：初始化 UI 根节点、设置和加载策略。
- `UIKit.Open<TPanel>(data)` / `OpenAsync<TPanel>(data)`：打开普通面板。
- `UIKit.Push<TPanel>(data)` / `PushAsync<TPanel>(data)`：压入页面栈。
- `UIKit.Pop()`、`PopTo<TPanel>()`、`ClearStack()`：页面栈返回。
- `UIKit.Close<TPanel>()`、`CloseAllPanels()`、`DestroyAllPanels()`：关闭面板。
- `UIKit.Preload<TPanel>()` / `PreloadAsync<TPanel>()`：预加载面板。
- `UIKit.TakeSnapshot()`、`LogSnapshot()`：运行时审计。
- `UIPanelBase`：面板基类。
- `UIPanelDataBase`：面板数据基类。

## 使用模板

```csharp
using Cysharp.Threading.Tasks;
using StellarFramework.UI;
using UnityEngine;

public sealed class ShopPanelData : UIPanelDataBase
{
    public int TabIndex;
}

public sealed class ShopPanel : UIPanelBase
{
    public override void OnOpen(UIPanelDataBase data)
    {
        if (TryGetPanelData<ShopPanelData>(data, out ShopPanelData shopData))
        {
            Debug.Log(shopData.TabIndex);
        }
    }
}

public sealed class UIEntry : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        await UIKit.Instance.InitAsync();
        await UIKit.OpenAsync<ShopPanel>(new ShopPanelData { TabIndex = 1 });
    }
}
```

## 页面栈

```csharp
await UIKit.PushAsync<MainMenuPanel>();
await UIKit.PushAsync<InventoryPanel>();
UIKit.Pop();
UIKit.PopTo<MainMenuPanel>();
UIKit.ClearStack();
```

## ToolsHub 关联

- `UIKit 工具`：UI 工作区、绑定代码生成、样例修复。
- `文档中心 (Docs)`：阅读 UIKit 说明和源码文档。

## 常见问题

- 面板打不开：检查 `UIKitSettings`、UIRoot、Prefab 路径和加载策略。
- Full Screen 页面下层没显示：这是预期，下层被暂停和隐藏。
- 热更 UI 加载失败：优先使用异步接口，并确认 ResKit/AA 地址可加载。

## 源码阅读

见 [UIKit 源码文档](UIKit-界面系统-源码文档-Guide.md)。
