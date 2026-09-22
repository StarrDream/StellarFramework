# LocalizationKit / 本地化系统

## 中文

### 定位

LocalizationKit.Core 是 `foundation / data`，负责稳定 Locale/Key、不可变 Table/Catalog、显式 fallback、lookup、语言切换事件和命名参数格式化。

Core `references=[]`、`noEngineReferences=true`，不引用 UnityEngine / UnityEditor / SettingsKit / UIKit / SaveKit，也不使用运行时反射或 assembly scan。

### 怎么导出

如果你的项目已经有自己的 UI、设置、资源系统，只缺本地化领域能力，直接在 `StellarFramework -> Export -> 01 基础功能` 选择 `LocalizationKit.Core` 即可。它不会把任何其他 StellarFramework Kit 或 UPM 带进来。

如果使用 Unity UGUI，需要再选 `LocalizationKit.UnityUGUIAdapter`；它只增加 Core + `com.unity.ugui`。

如果希望得到完整的 Unity 本地化开发体验，直接使用推荐组合 `Localization Complete`。它包含 Core、UGUI authoring/binding、Editor Validator 和 ToolsHub 入口，但仍不会强制引入 SettingsKit、UIKit、ResKit、Addressables 或 HybridCLR。只有当项目本身使用 SettingsKit 管语言设置时，才额外加入 `LocalizationKit.SettingsAdapter`。

完整 ToolsHub 工作流现在还包含：

- `LocalizationSourceRegistry`：持久化 BindingId、Key、Prefab GUID / LocalFileId、当前层级、SourceHash 与状态。
- `Scan & Bind`：扫描 UGUI Prefab，Preview 后再 Apply；Rename / Reparent / Reorder 不会改变既有 BindingId/Key。
- `LocalizationWorkspaceAsset`：配置 Source Locale 与任意数量目标语言 / Table。
- `Translation Matrix`：在 Unity 内查看缺失翻译、搜索并手工编辑目标语言。
- `JSON / CSV Import / Export`：用于外部人工或 AI 翻译。Unity 不调用远程 AI；导入时通过 SourceHash 拒绝已经过期的翻译文件。

扫描生成的初始 Key 采用“可读语义 + 稳定短 ID”，例如 `ui.panel_login.btn_confirm.c4729f11`。SiblingIndex 不进入长期身份；Hierarchy 只作为 Editor 元数据。

### Locale 与 Key

`LocaleId` 采用 BCP-47 风格 ASCII 分段与确定性大小写规范化，但不宣称实现完整 BCP-47 标准验证器。`LocalizationKey` 是稳定、区分大小写的业务文本 Key。

### Table / Catalog

`LocalizationTable` 与 `LocalizationCatalog` 构造后不可变。重复 Key、重复 Locale、空表项等结构问题会显式失败。

正常 lookup 使用 Dictionary，不需要运行时反射。Locale 构造、模板解析与最终字符串格式化允许产生必要字符串分配；它们不应被放在逐帧高频热路径。

### Fallback

Fallback 必须显式配置，且目标 Locale 必须存在于 Catalog。lookup 结果通过 `UsedFallback` 与 `ResolvedLocale` 暴露真实解析来源，不做隐藏兜底。

### Formatting 与 Placeholder Contract

模板使用命名参数，例如 `Remaining {count}`。支持 `{{` / `}}` 字面量大括号。

Core 提供统一模板解析语义；Editor Validator 使用相同 parser 检查：

- 非法/未闭合 placeholder。
- zh-CN / en-US 必需语言 coverage。
- 同一个 Key 在不同必需语言中的 placeholder 集合必须一致。

例如 `zh-CN: 剩余 {count}` 与 `en-US: Remaining {amount}` 会在 Editor 验证阶段直接失败。

### SettingsAdapter

`LocalizationKit.SettingsAdapter` 只实现 SettingsKit 已有 `ILanguageSettingsAdapter`，把语言选项和 ApplyLanguage 桥接到 LocalizationService。Core 不反向引用 SettingsKit。

### UnityUGUIAdapter

`LocalizationKit.UnityUGUIAdapter` 提供：

- `LocalizationTableAsset` / `LocalizationCatalogAsset` ScriptableObject Authoring。
- `LocalizationContext`。
- `LocalizedTextView`。
- `LocalizedButtonLabel`。

ScriptableObject 是 Unity Authoring/Presentation 选项，不是 Core 数据表示要求。

### TextMeshPro Adapter

`LocalizationKit.TMPAdapter` 是可选 presentation adapter，只依赖 Localization Core + `com.unity.textmeshpro`。Runtime 通过 Core 的 `ILocalizationContext` 合约访问 LocalizationService，因此 TMP View 不需要反向依赖 UnityUGUIAdapter。

TMP 工具链独立交付：

- `LocalizationKit.TMPAdapter`：`LocalizedTMPTextView` Runtime。
- `LocalizationKit.TMP.Editor`：TMP Prefab Scanner / Binding。
- `LocalizationKit.TMP.Tools`：ToolsHub 的 `Localization TMP` 扫描入口。

当前 TMP Editor Scanner 复用既有 `LocalizationTableAsset` 与 `LocalizationSourceRegistry` authoring 链，因此工具闭包会包含 Localization.Editor / UnityUGUI authoring；TMP Runtime 本身不包含 UGUI 依赖。

### Editor Validator

Editor-only Validator 默认按项目 Sample 规范检查 `zh-CN` / `en-US`，但 `Validate(catalog, requiredLocales)` 可传入任意 required locale 集合。LocalizationKit.Core 本身支持任意合法 Locale，不限定中文与英文。

可视化校验入口统一位于 `StellarFramework -> Tools Hub -> Localization 本地化`。Validator API 本身仍属于独立 Editor Profile，不要求 Core 或 UGUI 依赖 ToolsHub。

`Localization Complete` 默认同时包含 UGUI 与 TMP 两套运行时/扫描工具链，适合直接投入普通 Unity UI 项目生产。如果项目只需要某一种文本系统，则从 `01 基础功能 / 03 扩展功能` 按需选择 Core、UnityUGUIAdapter 或 TMPAdapter/TMP.Tools，不必导入 Complete。UGUI 与 TMP 共用 BindingId / Key / Registry / SourceHash 规则。

### Sample 语言按钮规则

语言选择器永远固定显示：

- `中文`：点击后所有可本地化 UI 使用 zh-CN。
- `English`：点击后所有可本地化 UI 使用 en-US。

两个语言按钮自身不进入 Localization Table，不会随着当前语言互相翻译。

### 字体

字体不属于 Core。Samples 默认使用项目内 SHA256 校验的 Adobe Source Han Sans CN Regular，并随 SIL OFL 1.1 LICENSE 一起分发。

## English

### Positioning

LocalizationKit.Core is a `foundation / data` Kit responsible for stable Locale/Key identities, immutable Tables/Catalogs, explicit fallback, lookup, locale-change events, and named formatting.

Core uses `references=[]` and `noEngineReferences=true`. It does not reference UnityEngine, UnityEditor, SettingsKit, UIKit, or SaveKit, and it performs no runtime reflection or assembly scanning.

### Export choices

If an existing project already owns its UI, settings, and resource systems and only needs localization domain logic, export `LocalizationKit.Core` from `StellarFramework -> Export -> 01 Basic Capabilities`. It has no StellarFramework or UPM dependencies.

Add `LocalizationKit.UnityUGUIAdapter` only when Unity UGUI authoring/binding is required. For the normal full Unity workflow, use the `Localization Complete` recommended profile: Core + UGUI + Editor Validator + ToolsHub integration. `LocalizationKit.SettingsAdapter` remains optional and is only needed when the project intentionally uses SettingsKit for language selection.

### Locale and Key

`LocaleId` uses BCP-47-style ASCII segments with deterministic casing. It is intentionally not advertised as a complete BCP-47 standards validator. `LocalizationKey` is a stable, case-sensitive business text key.

### Table / Catalog

`LocalizationTable` and `LocalizationCatalog` are immutable after construction. Duplicate keys, duplicate locales, null tables, and similar structural errors fail explicitly.

Normal lookup uses dictionaries and requires no runtime reflection. Locale construction, template parsing, and final string formatting may allocate the strings they necessarily produce and should not be placed in per-frame hot loops.

### Fallback

Fallback is always explicit and every fallback locale must exist in the Catalog. `UsedFallback` and `ResolvedLocale` expose the actual resolution path; no hidden fallback is performed.

### Formatting and Placeholder Contracts

Templates use named arguments such as `Remaining {count}` and support escaped braces through `{{` and `}}`.

The Editor Validator reuses Core template semantics to validate syntax and cross-locale placeholder contracts. For a required key, `zh-CN: 剩余 {count}` and `en-US: Remaining {amount}` fail validation before runtime.

### SettingsAdapter

`LocalizationKit.SettingsAdapter` only implements the existing SettingsKit `ILanguageSettingsAdapter` and bridges language choices / ApplyLanguage to `LocalizationService`. Core never references SettingsKit back.

### UnityUGUIAdapter

The Unity UGUI Adapter provides ScriptableObject Table/Catalog authoring, `LocalizationContext`, `LocalizedTextView`, and `LocalizedButtonLabel`. ScriptableObject is an authoring/presentation option, not a Core representation requirement.

### TextMeshPro Adapter

`LocalizationKit.TMPAdapter` is an optional presentation adapter depending only on Localization Core and `com.unity.textmeshpro` at runtime. `LocalizedTMPTextView` resolves the Core `ILocalizationContext` contract, while TMP scanning/tooling is delivered through separate optional Editor profiles.

### Editor Validator

The default sample profile requires `zh-CN` and `en-US`, while `Validate(catalog, requiredLocales)` accepts arbitrary required locales. LocalizationKit.Core itself is not limited to Chinese and English.

### Sample Language Selector Rule

Language selectors are permanently labeled `中文` and `English`. Pressing `中文` switches localizable UI to zh-CN; pressing `English` switches it to en-US. The selector labels never localize themselves.

### Fonts

Fonts do not belong to Core. Samples use the project-local SHA256-verified Adobe Source Han Sans CN Regular with its SIL OFL 1.1 license.
