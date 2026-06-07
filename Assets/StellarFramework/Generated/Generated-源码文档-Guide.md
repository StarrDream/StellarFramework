# Generated / 源码文档

## 模块职责

`Generated` 目录存放由编辑器工具或构建流程自动生成、并参与运行时编译的 C# 代码。

这些文件的特点是：

- 运行时会真实依赖它们
- 但来源不是手写业务代码
- 修改逻辑应优先修改生成器，不应直接修改生成产物

## 源码文件

当前主要分为两块：

- `Generated/AssetMap`
- `Generated/SingletonRegister`

## 总体结构

```text
Generated
├─ AssetMap
│  └─ 资源路径 -> BundleName 映射
└─ SingletonRegister
   └─ 单例元数据和创建器注册代码
```

## `AssetMap`

### 作用

`AssetMap.cs` 保存“资源路径 -> AssetBundle 名称”的映射关系。

它用于：

- `AssetBundleManager` 根据完整资源路径找到目标 bundle
- 运行时避免扫描目录或依赖运行时反射推断

### 典型结构

```csharp
public static class AssetMap
{
    public static Dictionary<string, string> GetMap() { ... }

    public static class Bundles
    {
        public const string ART = "art";
    }
}
```

### 关键成员

- `GetMap()`
  返回运行时使用的映射字典
- `Bundles`
  对常用 bundle 名称提供常量入口

### 生成来源

- `AssetBundleToolModule`

### 运行时依赖

- `AssetBundleManager.EnsureAssetMap()`
- `AssetBundleManager.LoadAssetSync(...)`
- `AssetBundleManager.LoadAssetAsync(...)`

### 设计约束

- 键通常是完整 `Assets/...` 路径
- 值是构建阶段确定的 bundle 名称
- 生成内容必须稳定，不能每次无意义抖动

## `SingletonRegister`

### 作用

`SingletonRegister.cs` 在运行时启动前把单例元数据和纯 C# 单例创建器注入 `SingletonFactory`。

它的意义是：

- 不在运行时反射扫描 `SingletonAttribute`
- 不在运行时反射创建纯 C# 单例
- 用生成代码把单例注册步骤前置到构建期

### 典型结构

```csharp
internal static class SingletonRegister
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterAll()
    {
        SingletonFactory.ClearMetadata();
        SingletonFactory.ClearPureSingletonCreators();
        ...
    }
}
```

### 关键行为

- 清空旧 metadata
- 清空旧 creator
- 调用 `SingletonFactory.RegisterMetadata(...)`
- 调用 `SingletonFactory.RegisterPureSingletonCreator(...)`

### 生成来源

- `SingletonGenerator`

### 运行时依赖

- `SingletonFactory.GetSingleton<T>()`
- `SingletonFactory.TryGetMetadata(...)`
- 所有 `Singleton<T>` / `MonoSingleton<T>` 体系

## 生成流程

### AssetMap

1. ToolsHub 执行 `资源打包 (AssetBundle)`
2. 收集规则和 bundle 名称
3. 生成 `AssetMap.cs`
4. 运行时按路径查 bundle

### SingletonRegister

1. 构建前或生成流程扫描带 `SingletonAttribute` 的类型
2. 提取生命周期、资源路径、容器挂载策略
3. 生成 `SingletonRegister.cs`
4. 运行时在 `SubsystemRegistration` 阶段自动注册

## 设计约束

- 生成产物不建议手改
- 真正的逻辑修改应该在生成器中完成
- 生成代码必须幂等
- 生成代码必须参与编译，否则运行时行为会直接失效

## 常见误用

- 手动修改 `AssetMap.cs`
- 手动修改 `SingletonRegister.cs`
- 修改生成规则但不重新生成
- 把生成文件当作业务代码入口继续叠逻辑

## 测试与验证

- AssetBundle 构建后验证 `AssetMap` 是否生成且编译通过
- Singleton 相关构建或生成流程后验证 `SingletonRegister` 是否生成且参与编译
- 若修改生成器逻辑，应同时更新对应源码文档和测试
