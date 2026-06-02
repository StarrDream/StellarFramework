# Generated / 源码文档

Generated 目录存放工具生成的 C# 代码。这里的文件服务于运行时，但来源是 Editor 工具或构建前处理器。原则是：可以读，不要手改。

## 源码位置

- `Generated/AssetMap/AssetMap.cs`：AssetBundle 工具生成的资源路径到 bundle 名映射。
- `Generated/SingletonRegister/SingletonRegister.cs`：SingletonKit 生成的单例元数据注册代码。
- `Editor/StellarToolsHub/Modules/AssetBundleToolModule.cs`：生成 AssetMap 的工具。
- `Runtime/Kits/SingletonKit/Editor/SingletonGenerator.cs`：生成 SingletonRegister 的构建前处理器。

## 核心类型

- `AssetMap`：提供资源路径到 AssetBundle 名称的查询数据。
- `AssetMap.Bundles`：按 bundle 名称组织的生成常量或集合。
- `SingletonRegister`：把带 `SingletonAttribute` 的类型注册到 `SingletonFactory`。
- `SingletonMetadata`：描述单例生命周期、场景对象策略和创建方式。

## 关键方法

- `AssetMap.GetMap()`：返回资源路径和 bundle 名称映射。
- `SingletonRegister.Register()`：把生成的单例元数据写入 `SingletonFactory`。
- `SingletonGenerator.Generate()`：扫描单例类型并重写生成文件。
- `AssetBundleToolModule` 的生成逻辑：在 AB 构建后更新 AssetMap。

## 数据流

1. 开发者在 ToolsHub 执行 AB 构建或 Unity 触发构建前处理。
2. Editor 工具扫描资产或类型。
3. 工具把结果写入 `Generated` 目录。
4. Runtime 代码读取生成类，避免运行时反射或扫描。
5. 下一次生成会覆盖旧文件。

## 依赖关系

- AssetMap 依赖 AB 构建规则和资源路径。
- SingletonRegister 依赖 `SingletonKit` 的特性和工厂。
- 生成代码参与 Runtime 编译，因此生成错误会直接造成 C# 编译错误。

## 扩展点

- 新增生成代码时，优先放入独立子目录。
- 生成文件顶部应注明来源和不要手改。
- 生成逻辑必须保证幂等，多次执行结果稳定。
- 修改生成格式后，更新对应源码文档和策略测试。

## 测试入口

- AB 相关生成：运行 ToolsHub `资源打包 (AssetBundle)` 并确认 `AssetMap` 编译通过。
- 单例生成：运行 SingletonKit 相关测试或触发构建前处理。
- 文档策略：README 和源码文档需要指向 Generated 的约束。
