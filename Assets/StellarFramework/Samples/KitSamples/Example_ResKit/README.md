# Example ResKit / 资源样例

`Assets/StellarFramework/Samples/KitSamples/Example_ResKit` 是 ResKit 案例目录。

这里集中放了示例脚本、测试资源源文件和运行说明，方便直接定位和构建案例资源。

## 目录

- `Example_ResKit.cs`
  案例脚本。
- `RawTextLoader.cs`
  自定义文本加载器。
- `Resources/ResKitTest/TestCube_Res.prefab`
  `ResourceLoader` 示例资源。
- `Art/AssetBundle/TestCapsule_AB.prefab`
  AssetBundle 示例源资源。
- `Addressables/TestSphere_AA.prefab`
  Addressables 示例源资源。

## 运行时文件

- `Assets/StreamingAssets/StellarFramework/Samples/KitSamples/Example_ResKit/TestText.txt`
  RawText 示例文件。
- `Assets/StreamingAssets/AssetBundles/[Platform]`
  AssetBundle 构建产物输出目录。

## 对应路径

- `Resources` 路径：`ResKitTest/TestCube_Res`
- `AssetBundle` 逻辑 key：
  `Assets/StellarFramework/Samples/KitSamples/Example_ResKit/Art/AssetBundle/TestCapsule_AB.prefab`
- `Addressables` Address Name：`TestSphere_AA`
- `RawText` 路径：
  `StellarFramework/Samples/KitSamples/Example_ResKit/TestText.txt`

## 运行方式

1. 打开 `Assets/StellarFramework/Samples/KitSamples/Scenes/ResKit_Playable.unity`
2. 先初始化 `AB Manager`
3. 依次验证 `AssetBundle / Addressables / Resources / RawText`

## 说明

- `Resources / AssetBundle / RawText` 已经补齐案例资源。
- `Addressables` 仍需要本地安装对应包并完成构建。
- 如果重新生成 `Assets/StellarFramework/Generated/AssetMap/AssetMap.cs` 或调整打包规则，请继续使用当前目录下的资源路径。
