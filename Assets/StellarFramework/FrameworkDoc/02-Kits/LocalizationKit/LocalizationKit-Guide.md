# LocalizationKit / 本地化系统

## 中文

### 定位

LocalizationKit.Core 是 `foundation / data`，负责稳定 Locale/Key、不可变 Table/Catalog、显式 fallback、lookup、语言切换事件和命名参数格式化。

Core `references=[]`、`noEngineReferences=true`，不引用 UnityEngine / UnityEditor / SettingsKit / UIKit / SaveKit，也不使用运行时反射或 assembly scan。

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

### Editor Validator

Editor-only Validator 默认按项目 Sample 规范检查 `zh-CN` / `en-US`，但 `Validate(catalog, requiredLocales)` 可传入任意 required locale 集合。LocalizationKit.Core 本身支持任意合法 Locale，不限定中文与英文。

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

### Editor Validator

The default sample profile requires `zh-CN` and `en-US`, while `Validate(catalog, requiredLocales)` accepts arbitrary required locales. LocalizationKit.Core itself is not limited to Chinese and English.

### Sample Language Selector Rule

Language selectors are permanently labeled `中文` and `English`. Pressing `中文` switches localizable UI to zh-CN; pressing `English` switches it to en-US. The selector labels never localize themselves.

### Fonts

Fonts do not belong to Core. Samples use the project-local SHA256-verified Adobe Source Han Sans CN Regular with its SIL OFL 1.1 license.
