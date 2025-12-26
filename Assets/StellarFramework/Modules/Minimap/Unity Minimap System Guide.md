# Unity 小地图系统集成手册 (v3.0)

## 1. 系统简介
本系统是一套高性能、高解耦的生产级小地图解决方案。
*   **特性**: 彻底分离逻辑与表现，支持多对象池管理，支持自定义导航路径。
*   **适用范围**: RPG、MOBA、FPS 等需要小地图指引的项目。

---

## 2. 快速环境搭建 (Setup)

### 2.1 UI 层级结构 (必读)
请严格按照以下层级创建 UGUI 物体，错误的层级会导致遮挡关系失效。

```text
Canvas
└── Minimap Root (空物体/背景板)
    ├── Mask View (Image + Mask)       <-- [交互层] 负责裁剪和接收鼠标事件
    │   └── Content Root (空物体)      <-- [核心容器] 锚点必须居中 (0.5, 0.5)
    │       ├── Map Image (Image)      <-- [地图底图]
    │       ├── Route Line (空物体)    <-- [导航线] 挂载 MinimapRouteManager
    │       └── Markers Container (空) <-- [图标容器] 所有图标会自动生成在这里
    └── Controls (缩放/复位按钮)
```

### 2.2 关键组件检查
*   **Route Line**: 必须挂载 `MinimapRouteManager`。**必须**创建一个材质球（Shader 选 `UI/Default`）并赋值给它的 Image 组件，否则 Mask 遮罩无法对线条生效。
*   **MinimapManager**: 场景单例。需引用上述所有 UI 组件，并设置 `Default Marker Prefab` 防止空引用报错。

---

## 3. 拓展指南：如何添加新图标？ (Extension Guide)

本系统支持为不同的单位（主角、Boss、NPC、宝箱）使用完全不同的图标预制体。

### 场景 A：我只需要图标长得不一样 (纯美术工作)
**需求**: 我想加一个“宝箱怪”，图标是一个金色的箱子，不需要写代码。

1.  **制作 Prefab**:
    *   在 UI Canvas 下创建一个 `Image`。
    *   将图片修改为“金色箱子” Sprite。
    *   调整合适的大小 (例如 30x30)。
    *   挂载 `MapMarker.cs` 脚本。
    *   将此物体拖入 Project 窗口制成 Prefab，命名为 `ChestMarker_Prefab`。
2.  **配置游戏物体**:
    *   在场景中选中“宝箱怪”物体。
    *   挂载 `MapEntity.cs` 脚本。
    *   找到 **Custom Marker Prefab** 属性。
    *   将刚才做的 `ChestMarker_Prefab` 拖进去。
3.  **完成**: 运行游戏，系统会自动为宝箱怪生成金箱子图标。

### 场景 B：我需要图标带动画 (美术+动画)
**需求**: 任务点的图标需要是一个不断跳动的箭头。

1.  **制作 Prefab**:
    *   同上，制作一个 `QuestMarker_Prefab`。
    *   在该物体上添加 Unity 原生的 `Animator` 组件。
    *   制作一个简单的 Animation Clip (比如修改 Scale 从 1.0 变到 1.2)。
2.  **配置**:
    *   同上，将此 Prefab 拖给任务系统的 `QuestNavigator` 或 `MapEntity`。
3.  **原理**: `MapMarker` 脚本只控制位置，不会干扰你挂在 Prefab 上的 Animator，两者完美共存。

### 场景 C：我需要图标有特殊逻辑 (程序工作)
**需求**: Boss 的图标需要根据 Boss 当前血量改变颜色（血越少越红），或者显示一个扇形视野。

1.  **编写脚本**:
    创建一个新脚本 `BossMarker.cs`，继承自 `MapMarker`。
    ```csharp
    using Minimap.Markers;
    public class BossMarker : MapMarker 
    {
        public Image healthBar; // 你的自定义血条UI
        
        // 扩展 Update 逻辑
        private void Update() {
            // 注意：父类 MapMarker 已经处理了位置同步
            // 这里你只需要处理 Boss 独有的逻辑
            if (healthBar != null) {
                // 假设你能获取到 Boss 血量
                healthBar.fillAmount = ...; 
            }
        }
    }
    ```
2.  **制作 Prefab**:
    *   创建一个 UI Prefab。
    *   **不要**挂 `MapMarker`，而是挂载你刚写的 `BossMarker`。
    *   在 Inspector 里把 `iconImage` 等父类引用拖好。
3.  **配置**:
    *   把这个 `BossMarker_Prefab` 拖给 Boss 身上的 `MapEntity`。
4.  **原理**: 系统使用了多态，Manager 会自动识别并运行你的子类逻辑。

---

## 4. 任务与导航 (Navigation)

### 4.1 脚本调用
使用 `QuestNavigator` 组件来控制任务指引。

```csharp
// 引用
public QuestNavigator questNav;

// 1. 绘制自定义路径 (最常用)
// 适用于：服务器下发路径、配置表路径
// 效果：在终点生成任务图标，并画出连线
questNav.SetQuestPath(new Vector3[] { p1, p2, p3 });

// 2. 仅显示目标点
// 效果：只在地图上显示一个图标，不画线
questNav.SetQuestMarker(targetPos);

// 3. 结合 NavMesh
// 效果：利用 Unity 寻路计算路径并显示
var path = new NavMeshPath();
NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path);
questNav.SetQuestPath(path.corners);
```

### 4.2 动态切换图标
如果需要在运行时改变图标（例如怪物进入战斗状态），请在 `MapEntity` 中添加如下方法并调用：

```csharp
// 在 MapEntity.cs 中
public void UpdateIcon(Sprite newIcon, Color newColor) {
    if (_myMarker != null) {
        _myMarker.iconImage.sprite = newIcon;
        _myMarker.iconImage.color = newColor;
    }
}
```

---

## 5. 常见问题排查 (Troubleshooting)

| 现象 | 可能原因 | 解决方案 |
| :--- | :--- | :--- |
| **图标位置不对** | 坐标映射配置错误 | 检查 `MapConfig` 中的 Min/Max XZ 是否与真实场景地形尺寸一致。 |
| **导航线是黑色的** | 材质丢失 | 确保 `Route Line` 的 Image 组件赋值了一个使用 `UI/Default` Shader 的材质球。 |
| **图标旋转方向反了** | 素材朝向问题 | 调整 `MapEntity` 上的 `Rotation Offset`。如果素材箭头朝右，通常填 -90。 |
| **Mask 遮不住线** | 层级或材质问题 | 1. 线条必须是 Mask 的子物体。<br>2. 线条材质必须是 `UI/Default` (不能是 Sprites-Default)。 |
| **报错 NullReference** | 缺少 Prefab | 检查 `MinimapManager` 上的 `Default Marker Prefab` 是否已赋值。 |