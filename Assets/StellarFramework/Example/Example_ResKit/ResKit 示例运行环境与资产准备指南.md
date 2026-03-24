# ResKit 真实物理构建与测试指南

为了验证 ResKit 在商业化生产环境中的真实表现，本次测试**严禁使用 Editor 模拟加载**。我们将强制走“构建出物理包 -> 从物理路径读取”的完整闭环。

---

## 1. 准备测试场景与脚本
1. 创建一个新的空场景，命名为 `ResKit_PhysicalTest`。
2. 创建一个空物体，命名为 `ResKitTester`。
3. 将 `Example_ResKit.cs` 挂载到该物体上。

---

## 2. AssetBundle (AB) 物理构建与测试
目标：验证工具链能否正确打包到 `StreamingAssets`，且运行时能否从中读取。

### 2.1 准备资产
1. 在工程中创建目录：`Assets/Art/ResKitTest`。
2. 在场景中创建一个 Capsule (胶囊体)，给它赋予一个**非默认的材质**（例如新建一个红色的 Material 赋给它，这非常重要，用于验证 Shader 是否成功预热且没有变粉）。
3. 将 Capsule 拖入 `Assets/Art/ResKitTest` 制成 Prefab，命名为 `TestCapsule_AB`，然后删除场景中的 Capsule。

### 2.2 执行物理构建
1. 打开顶部菜单：`StellarFramework -> Tools Hub`。
2. 选择左侧的 **资源打包 (AssetBundle)**。
3. 将 `Assets/Art/ResKitTest` 文件夹拖入左侧面板。
4. 点击顶部的 **"应用规则 & 生成代码"**。
    * *验证点：检查 `Assets/StellarFramework/Generated/AssetMap.cs` 是否生成了对应的路径映射。*
5. 点击顶部的 **"增量构建"**。
    * *验证点：打开工程目录 `Assets/StreamingAssets/AssetBundles/[平台名]`，确认里面是否生成了物理文件（包括 `shaders` 包和你的资源包）。*

---

## 3. Addressables (AA) 物理构建与测试
目标：强制 AA 放弃 Editor 模拟，读取本地构建出的真实 Bundle 文件。

### 3.1 准备资产
1. 在工程中创建目录：`Assets/AddressableAssets`。
2. 创建一个 Sphere (球体)，制成 Prefab 命名为 `TestSphere_AA`，删除场景中的 Sphere。
3. 选中该 Prefab，在 Inspector 顶部勾选 `Addressable`。
4. 将其 Address Name 修改为 `TestSphere_AA`（去掉路径前缀和后缀）。

### 3.2 强制物理构建与配置
1. 打开菜单：`Window -> Asset Management -> Addressables -> Groups`。
2. **关键步骤**：在 Groups 窗口顶部，点击 `Play Mode Script`，将其从 `Use Asset Database (fastest)` 改为 **`Use Existing Build`**。
    * *解释：这会强制 Unity 在 Editor 播放时，去读取你真实构建出来的 Bundle，而不是直接读工程资产。*
3. 点击 `Build -> New Build -> Default Build Script` 执行构建。
    * *验证点：构建产物通常会生成在 `Library/com.unity.addressables/` 目录下。*

---

## 4. Resources 与 CustomLoader 准备
1. **Resources**：
    * 创建目录 `Assets/Resources/ResKitTest`。
    * 创建一个 Cube 制成 Prefab，命名为 `TestCube_Res`。
2. **CustomLoader**：
    * 在 `Assets/StreamingAssets` 目录下创建一个文本文件，命名为 `TestText.txt`。
    * 随便输入一些内容，例如：`Hello Physical World!`。

---

## 5. 运行与严格验证
运行 `ResKit_PhysicalTest` 场景，你将看到左上角的交互 GUI。请按以下顺序点击：

1. **点击 [1. 初始化 AB 管理器]**
    * 观察 Console，确认 Manifest 和 Shader 预热成功。
2. **点击 [加载 AssetBundle 资源]**
    * 场景中应出现 Capsule。
    * **核心验证**：检查 Capsule 的材质是否正常（红色）。如果变粉，说明 Shader 剥离或预热失败；如果正常，说明 AB 物理管线完美跑通。
3. **点击 [加载 Addressables 资源]**
    * 场景中应出现 Sphere。
    * **核心验证**：因为我们设置了 `Use Existing Build`，如果能加载出来，说明 AA 的本地物理 Bundle 读取成功。
4. **点击 [加载 Resources 资源]** 与 **[加载 RawText]**
    * 验证常规加载与自定义 IO 流读取。
5. **点击红色的 [销毁实例并回收加载器]**
    * 观察场景中的物体是否消失。
    * **核心验证**：再次点击 [加载 AssetBundle 资源]，如果物体能再次正常出现且材质不丢失，说明底层 `Unload(true)` 的防镜像冗余机制与对象池的回收逻辑完美闭环。