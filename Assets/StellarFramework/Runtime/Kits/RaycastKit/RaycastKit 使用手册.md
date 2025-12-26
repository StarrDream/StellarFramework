# RaycastKit 使用手册

## 1. 设计理念 (Why)
Unity 的射线检测 API (`Physics.Raycast`) 参数众多且容易产生 GC。
RaycastKit 封装了常用的检测逻辑，并统一处理了 **UI 遮挡** 这一痛点。

### 核心特性
*   **UI 遮挡检测**：统一封装 `EventSystem`，兼容鼠标和触摸，防止点穿 UI。
*   **安全相机获取**：自动缓存 `Camera.main`，并处理场景切换时的引用失效问题。

---

## 2. 快速上手 (Quick Start)

### 2.1 UI 遮挡检测 (最常用)
检查鼠标/手指是否点在了 UI 上（防止点穿 UI 触发 3D 逻辑）。
```csharp
void Update() {
    // 如果点在 UI 上，直接返回，不处理 3D 逻辑
    if (RaycastKit.IsPointerOverUI()) return;

    // 处理 3D 点击...
}
```

### 2.2 屏幕射线检测
```csharp
if (Input.GetMouseButtonDown(0)) {
    // 检测 3D
    if (RaycastKit.Raycast3D(Input.mousePosition, out var hit)) {
        Debug.Log($"Hit 3D: {hit.collider.name}");
    }

    // 检测 2D (Sprite)
    if (RaycastKit.Raycast2D(Input.mousePosition, out var hit2D)) {
        Debug.Log($"Hit 2D: {hit2D.collider.name}");
    }
}
```

### 2.3 视线检测 (LOS)
判断 AI 是否能看到玩家（无障碍物遮挡）。

```csharp
if (RaycastKit.HasLineOfSight(enemy.position, player.transform, out var hit)) {
    Attack();
}
```