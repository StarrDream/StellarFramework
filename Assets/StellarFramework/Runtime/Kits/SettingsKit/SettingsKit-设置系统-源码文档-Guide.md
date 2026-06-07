# SettingsKit / 设置系统源码文档

## 模块职责

`SettingsKit` 负责把“设置项定义、设置页组织、运行时值管理、应用策略、持久化存储”收敛成一套统一流程。

模块拆分成五层：

- 门面层：`SettingsKit`
- 管理层：`SettingsManager`
- 注册层：`SettingsRegistry`
- 定义层：`SettingDefinition` 及各具体定义类型
- 存储 / 应用层：`ISettingsStorage`、`ISettingApplyStrategy`、各类 Adapter 和 Provider

## 源码文件

- `Runtime/Kits/SettingsKit/Core/SettingsKit.cs`
- `Runtime/Kits/SettingsKit/Core/SettingsManager.cs`
- `Runtime/Kits/SettingsKit/Core/SettingsRegistry.cs`
- `Runtime/Kits/SettingsKit/Core/SettingDefinitions.cs`
- `Runtime/Kits/SettingsKit/Core/SettingsContracts.cs`
- `Runtime/Kits/SettingsKit/Core/SettingsEntry.cs`
- `Runtime/Kits/SettingsKit/Core/PlayerPrefsSettingsStorage.cs`
- `Runtime/Kits/SettingsKit/Core/SettingsMenuOverlay.cs`
- `Runtime/Kits/SettingsKit/Core/SettingsKitSingletonRegister.cs`
- `Runtime/Kits/SettingsKit/Providers/BuiltinSettingsProviders.cs`
- `Runtime/Kits/SettingsKit/Adapters/SettingsAdapters.cs`

## 总体结构

```text
SettingsKit
└─ SettingsManager.Instance
   ├─ _registry
   ├─ _entries
   ├─ _providers
   └─ _storage

SettingsRegistry
├─ _pages
└─ _settings

SettingDefinition
├─ BoolSettingDefinition
├─ FloatSettingDefinition
├─ IntSettingDefinition
├─ StringSettingDefinition
└─ ChoiceSettingDefinition

SettingsContracts
├─ SettingsPageDefinition
├─ SettingChoiceOption
├─ ISettingApplyStrategy
├─ ISettingsStorage
└─ ISettingsPageProvider
```

## 运行时调用链

### 初始化

1. 业务调用 `SettingsKit.ConfigureStorage(...)`
2. 业务调用 `SettingsKit.RegisterProvider(...)` 或 `InstallDefaultProviders(...)`
3. 业务调用 `SettingsKit.Init()`
4. `SettingsManager.Init()`
5. `EnsureEntriesForDefinitions()` 根据 `SettingsRegistry` 构建运行时条目
6. 从 `ISettingsStorage` 读取原始字符串
7. 通过 `SettingDefinition.TryDeserialize(...)` 恢复值
8. `ApplyAllCurrentValues()` 把当前值应用到运行时系统

### 修改值

1. 业务调用 `TrySetValue(key, rawValue, out error)`
2. `SettingsManager` 找到 `SettingEntry`
3. `SettingDefinition.TryNormalize(...)` 把输入归一化成合法值
4. 若设置项是 `ApplyImmediately`，则立刻调用 `TryApplyValue(...)`
5. 更新 `SettingEntry.CurrentValue`
6. 触发 `SettingChanged`

### 保存

1. 业务调用 `Save(out error)`
2. `ApplyPending(out error)` 先应用所有未应用脏数据
3. 遍历 `_entries`
4. `SettingDefinition.Serialize(...)` 把值写成字符串
5. `_storage.Save(key, rawValue)`
6. `_storage.Flush()`
7. 每个条目 `MarkSaved()`

### 回滚

1. 业务调用 `RevertPending(out error)`
2. 遍历所有 dirty 条目
3. 对 `SavedValue` 再执行一次 `TryApplyValue(...)`
4. 把 `CurrentValue` 回退成 `SavedValue`
5. 清理错误并触发 `SettingChanged`

## 类型详解

## `SettingValueKind`

### 作用

标记设置项值类型。

### 枚举值

- `Bool`
- `Float`
- `Int`
- `String`
- `Choice`

## `SettingsPageDefinition`

### 作用

描述一个设置页。

### 字段 / 属性

- `Id`
  页唯一标识。
- `DisplayName`
  页面显示名称。
- `Description`
  页面描述。
- `Order`
  页面排序。

## `SettingChoiceOption`

### 作用

描述一个可选项。

### 字段 / 属性

- `Value`
  实际存储值。
- `Label`
  UI 显示名称。
- `Description`
  可选说明文本。

## `ISettingApplyStrategy`

### 作用

抽象“把一个设置值应用到真实运行时系统”的行为。

### 成员

- `StrategyName`
- `TryApply(SettingDefinition definition, object value, out string error)`

## `NoopSettingApplyStrategy`

### 作用

空实现策略。

用于：

- 没有真实运行时系统可写入时占位
- 只需要保存、不需要即时应用的设置项

## `DelegateSettingApplyStrategy`

### 作用

用委托快速包装应用逻辑。

### 字段

- `_applyFunc`

### 属性

- `StrategyName`

### 方法

- `TryApply(...)`
  执行委托，若返回的错误字符串为空则视为成功。

## `ISettingsStorage`

### 作用

抽象设置值存储后端。

### 成员

- `TryLoad(string key, out string rawValue)`
- `Save(string key, string rawValue)`
- `Delete(string key)`
- `Flush()`

## `ISettingsPageProvider`

### 作用

向 `SettingsRegistry` 注入设置页和设置项定义。

### 成员

- `ProviderName`
- `Register(SettingsRegistry registry)`

## 各类 Adapter 接口

### `IAudioSettingsAdapter`

负责：

- `MusicVolume`
- `SoundVolume`
- `MusicOn`
- `SoundOn`

### `IGraphicsSettingsAdapter`

负责：

- 分辨率选项与应用
- 画质等级选项与应用
- 全屏开关
- VSync
- 目标帧率

### `ILanguageSettingsAdapter`

负责语言选项与语言应用。

### `IInputBindingAdapter`

负责输入绑定规格列表和应用逻辑。

### `InputBindingSettingSpec`

描述一个输入绑定设置项。

字段：

- `Key`
- `DisplayName`
- `Description`
- `DefaultValue`
- `Options`
- `Order`

## `SettingDefinition`

### 作用

所有设置项定义的抽象基类。

### 核心字段 / 属性

- `Key`
  设置项唯一标识。
- `PageId`
  所属页面 ID。
- `DisplayName`
- `Description`
- `ValueKind`
- `ApplyImmediately`
  是否在 `TrySetValue` 时立即应用。
- `RequiresRestart`
  是否需要重启生效。
- `Order`
  页面内排序。
- `DefaultValue`
  默认值。
- `ApplyStrategy`
  应用策略。

### 抽象方法

- `TryNormalize(object rawValue, out object normalizedValue)`
  把外部输入转成合法的内部值。
- `Serialize(object value)`
  序列化为存储字符串。
- `TryDeserialize(string rawValue, out object value)`
  从存储字符串恢复值。

### 虚方法

- `FormatValue(object value)`
  供 UI 层展示用的格式化字符串。

## `BoolSettingDefinition`

### 作用

布尔设置定义。

### `TryNormalize(...)`

支持以下输入：

- `bool`
- `"true" / "false"`
- `"0" / "1"`
- `int`

### `Serialize(...)`

输出 `"1"` 或 `"0"`。

### `TryDeserialize(...)`

优先解析 `"1" / "0"`，否则回退到 `TryNormalize(...)`。

## `FloatSettingDefinition`

### 作用

浮点设置定义。

### 额外属性

- `MinValue`
- `MaxValue`
- `Step`

### `TryNormalize(...)`

支持：

- `float`
- `double`
- `int`
- `string`

并且会做：

- Clamp 到区间
- 按 `Step` 做量化

### `Serialize(...)`

格式化为文化无关字符串。

### `FormatValue(...)`

输出两位以内小数文本。

## `IntSettingDefinition`

### 作用

整数设置定义。

### 额外属性

- `MinValue`
- `MaxValue`

### `TryNormalize(...)`

支持：

- `int`
- `float`
- `string`

最后做整数 Clamp。

## `StringSettingDefinition`

### 作用

字符串设置定义。

### 额外属性

- `MaxLength`

### `TryNormalize(...)`

把任意输入转为字符串，并截断到最大长度。

## `ChoiceSettingDefinition`

### 作用

离散选项设置定义。

### 字段 / 属性

- `_optionLookup`
- `Options`

### `TryNormalize(...)`

只接受 `Options` 中出现过的 `Value`。

### `FormatValue(...)`

若命中选项，返回 `Label`，否则返回原始 key。

### 关键私有方法

- `ResolveDefaultValue(...)`
  若传入默认值不合法，则回退到第一个选项。
- `BuildOptionLookup(...)`
  构建选项值查找表。

## `SettingEntry`

### 作用

描述某个设置项在运行时的当前状态。

### 核心字段 / 典型职责

通常承载：

- `Definition`
- `CurrentValue`
- `SavedValue`
- `Error`
- `IsDirty`

### 运行时语义

- `CurrentValue`
  当前编辑值 / 当前应用值
- `SavedValue`
  已保存值
- `IsDirty`
  当前值与已保存值不一致

## `SettingsRegistry`

### 作用

维护页面定义和设置定义的注册表。

### 核心字段

- `_pages : Dictionary<string, SettingsPageDefinition>`
- `_settings : Dictionary<string, SettingDefinition>`

### 属性

- `Pages`
- `Settings`

### 关键方法

#### `RegisterPage(SettingsPageDefinition page)`

注册页面定义。

约束：

- `page != null`
- `page.Id` 不为空

#### `RegisterSetting(SettingDefinition definition)`

注册设置项定义。

职责：

- 校验定义不为空
- 校验 `Key` 和 `PageId`
- 若 `PageId` 对应页面不存在，则自动创建占位页面
- 同 key 重复注册时后者覆盖前者

#### `TryGetSetting(...)`
- 查找设置定义。

#### `TryGetPage(...)`
- 查找页面定义。

#### `GetSortedPages()`

按：

1. `Order`
2. `DisplayName`

返回排序后的页面列表。

#### `GetSortedSettingsForPage(pageId)`

按：

1. `Order`
2. `DisplayName`

返回某页面下排序后的设置项定义。

## `SettingsManager`

### 作用

运行时设置系统核心管理器。

### 继承关系

- `Singleton<SettingsManager>`

### 核心字段

- `_registry`
  注册表。
- `_entries`
  运行时条目表，key 为设置 key。
- `_providers`
  已注册页面提供器。
- `_storage`
  当前存储后端。
- `_isInitialized`
  是否已初始化。
- `_defaultProvidersInstalled`
  是否已安装内置 provider。

### 事件

- `SettingChanged`
  某个条目成功修改后触发。

### 属性

- `IsInitialized`
- `HasDirtySettings`
  通过扫描 `_entries.Values.Any(entry => entry.IsDirty)` 得到。

### 生命周期

#### `OnSingletonInit()`

初始化默认存储为 `PlayerPrefsSettingsStorage`。

### 关键方法

#### `Configure(ISettingsStorage storage)`

在初始化前替换存储后端。

若已经初始化，则只记录警告，不允许重新配置。

#### `RegisterProvider(ISettingsPageProvider provider)`

职责：

- 校验 provider 非空
- 防止重复加入 `_providers`
- 调用 `provider.Register(_registry)`
- 若系统已经初始化，则补齐新 provider 带来的条目，并立即应用当前值

#### `InstallDefaultProviders(DefaultSettingsInstallOptions options)`

安装内置 provider，只执行一次。

#### `Init()`

初始化入口。

职责：

- 确保 `_storage` 存在
- `EnsureEntriesForDefinitions()`
- `ApplyAllCurrentValues()`
- 标记 `_isInitialized = true`

#### `GetPages()`

返回排序后的页面定义。

#### `GetEntriesForPage(pageId)`

根据页面定义顺序，从 `_entries` 中找出对应运行时条目。

#### `TryGetEntry(key, out entry)`

按 key 读取条目。

#### `GetValue<T>(key, fallback)`

按泛型读取 `CurrentValue`，类型不匹配则返回 fallback。

#### `TrySetValue(key, rawValue, out error)`

设置值主入口。

职责：

- 查找条目
- 归一化 rawValue
- 若值未变则直接成功返回
- 若 `ApplyImmediately`，立即执行应用策略
- 更新 `CurrentValue`
- 清理错误
- 触发 `SettingChanged`

失败分支：

- key 不存在
- 归一化失败
- 即时应用失败

#### `ApplyPending(out error)`

遍历所有 dirty 条目，对非 `ApplyImmediately` 项执行应用策略。

#### `Save(out error)`

职责：

- 先执行 `ApplyPending`
- 序列化每个条目
- `_storage.Save(...)`
- `_storage.Flush()`
- `MarkSaved()`

#### `RevertPending(out error)`

职责：

- 遍历所有 dirty 条目
- 把 `SavedValue` 重新应用到系统
- 把 `CurrentValue` 回退到 `SavedValue`
- 清理错误
- 触发 `SettingChanged`

#### `ResetPage(pageId)`

把某一页的所有条目重置为默认值。

#### `ResetAll()`

把所有条目重置为默认值。

### 核心私有方法

#### `EnsureEntriesForDefinitions()`

根据 `SettingsRegistry.Settings` 构建 `_entries`。

流程：

- 遍历定义
- 若条目已存在则跳过
- 先取默认值
- 若 `_storage.TryLoad(...)` 成功且能反序列化，则使用存档值
- 创建 `SettingEntry`

#### `ApplyAllCurrentValues()`

初始化后将所有当前值写入运行时系统。

若应用失败：

- 记录警告
- 回退到默认值再尝试应用
- 若默认值也失败，则记录错误

#### `TryApplyValue(...)`

统一执行应用策略，并捕获策略抛出的异常。

#### `EnsureInitializedForUsage()`

惰性保证已初始化；若未初始化则自动 `Init()`。

## `SettingsKit`

### 作用

对外静态门面。

### 典型职责

- 转发到 `SettingsManager`
- 隐藏单例访问细节
- 给业务层提供更短、更稳定的调用入口

### 常见公开 API

- `ConfigureStorage(...)`
- `RegisterProvider(...)`
- `InstallDefaultProviders(...)`
- `Init()`
- `GetPages()`
- `GetEntriesForPage(...)`
- `TrySetValue(...)`
- `ApplyPending(...)`
- `Save(...)`
- `RevertPending(...)`
- `ResetPage(...)`
- `ResetAll()`

## 内置 Provider 与 Adapter

### `BuiltinSettingsProviders.cs`

提供：

- `SettingsPageIds`
- `SettingsKeys`
- `DefaultSettingsInstallOptions`
- `DefaultSettingsInstaller`
- 若干默认页面 provider

### `SettingsAdapters.cs`

提供默认运行时适配器：

- `AudioKitSettingsAdapter`
- `UnityGraphicsSettingsAdapter`
- `SimpleLanguageSettingsAdapter`
- `SimpleInputBindingAdapter`

它们的职责是把 SettingsKit 的通用设置定义映射到真实系统：

- `AudioKit`
- Unity 图形设置
- 语言系统
- 输入绑定系统

## 设计约束

- 设置定义和运行时条目分离
- pending 值和 saved 值分离
- provider 只负责定义，不负责存储
- apply strategy 只负责把值写进真实系统
- storage 只负责字符串级持久化

## 常见误用

- 初始化后再替换 `_storage`
- 直接改 `SettingEntry.CurrentValue`，绕过 `TrySetValue`
- 以为 `Save()` 会自动帮你处理所有非法值
- provider 注册后忘记对应运行时适配器和策略实现

## 测试建议

- provider 注册
- 页面排序和设置项排序
- 各种 `SettingDefinition` 的 normalize / serialize / deserialize
- `TrySetValue / ApplyPending / Save / RevertPending`
- 默认 provider 安装
- adapter 映射到真实系统的成功与失败分支
