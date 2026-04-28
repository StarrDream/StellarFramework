using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace StellarFramework.Settings
{
    public abstract class SettingDefinition
    {
        public string Key { get; }
        public string PageId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public SettingValueKind ValueKind { get; }
        public bool ApplyImmediately { get; }
        public bool RequiresRestart { get; }
        public int Order { get; }
        public object DefaultValue { get; }
        public ISettingApplyStrategy ApplyStrategy { get; }

        protected SettingDefinition(
            string key,
            string pageId,
            string displayName,
            string description,
            SettingValueKind valueKind,
            object defaultValue,
            bool applyImmediately,
            bool requiresRestart,
            int order,
            ISettingApplyStrategy applyStrategy)
        {
            Key = key ?? string.Empty;
            PageId = pageId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            ValueKind = valueKind;
            DefaultValue = defaultValue;
            ApplyImmediately = applyImmediately;
            RequiresRestart = requiresRestart;
            Order = order;
            ApplyStrategy = applyStrategy ?? NoopSettingApplyStrategy.Instance;
        }

        public abstract bool TryNormalize(object rawValue, out object normalizedValue);
        public abstract string Serialize(object value);
        public abstract bool TryDeserialize(string rawValue, out object value);

        public virtual string FormatValue(object value)
        {
            return value?.ToString() ?? string.Empty;
        }
    }

    public sealed class BoolSettingDefinition : SettingDefinition
    {
        public BoolSettingDefinition(
            string key,
            string pageId,
            string displayName,
            string description,
            bool defaultValue,
            bool applyImmediately = false,
            bool requiresRestart = false,
            int order = 0,
            ISettingApplyStrategy applyStrategy = null)
            : base(
                key,
                pageId,
                displayName,
                description,
                SettingValueKind.Bool,
                defaultValue,
                applyImmediately,
                requiresRestart,
                order,
                applyStrategy)
        {
        }

        public override bool TryNormalize(object rawValue, out object normalizedValue)
        {
            switch (rawValue)
            {
                case bool boolValue:
                    normalizedValue = boolValue;
                    return true;
                case string stringValue when bool.TryParse(stringValue, out bool parsedBool):
                    normalizedValue = parsedBool;
                    return true;
                case string stringValue when int.TryParse(
                    stringValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedInt):
                    normalizedValue = parsedInt != 0;
                    return true;
                case int intValue:
                    normalizedValue = intValue != 0;
                    return true;
                default:
                    normalizedValue = DefaultValue;
                    return false;
            }
        }

        public override string Serialize(object value)
        {
            return value is bool boolValue && boolValue ? "1" : "0";
        }

        public override bool TryDeserialize(string rawValue, out object value)
        {
            if (string.IsNullOrEmpty(rawValue))
            {
                value = DefaultValue;
                return false;
            }

            if (rawValue == "1")
            {
                value = true;
                return true;
            }

            if (rawValue == "0")
            {
                value = false;
                return true;
            }

            return TryNormalize(rawValue, out value);
        }
    }

    public sealed class FloatSettingDefinition : SettingDefinition
    {
        public float MinValue { get; }
        public float MaxValue { get; }
        public float Step { get; }

        public FloatSettingDefinition(
            string key,
            string pageId,
            string displayName,
            string description,
            float defaultValue,
            float minValue,
            float maxValue,
            float step = 0.01f,
            bool applyImmediately = false,
            bool requiresRestart = false,
            int order = 0,
            ISettingApplyStrategy applyStrategy = null)
            : base(
                key,
                pageId,
                displayName,
                description,
                SettingValueKind.Float,
                Mathf.Clamp(defaultValue, minValue, maxValue),
                applyImmediately,
                requiresRestart,
                order,
                applyStrategy)
        {
            MinValue = minValue;
            MaxValue = maxValue;
            Step = step <= 0f ? 0.01f : step;
        }

        public override bool TryNormalize(object rawValue, out object normalizedValue)
        {
            float value;
            switch (rawValue)
            {
                case float floatValue:
                    value = floatValue;
                    break;
                case double doubleValue:
                    value = (float)doubleValue;
                    break;
                case int intValue:
                    value = intValue;
                    break;
                case string stringValue when float.TryParse(
                    stringValue,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedFloat):
                    value = parsedFloat;
                    break;
                default:
                    normalizedValue = DefaultValue;
                    return false;
            }

            value = Mathf.Clamp(value, MinValue, MaxValue);
            value = Mathf.Round(value / Step) * Step;
            normalizedValue = value;
            return true;
        }

        public override string Serialize(object value)
        {
            float floatValue = value is float typedValue
                ? typedValue
                : Convert.ToSingle(value, CultureInfo.InvariantCulture);
            return floatValue.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public override bool TryDeserialize(string rawValue, out object value)
        {
            return TryNormalize(rawValue, out value);
        }

        public override string FormatValue(object value)
        {
            float floatValue = value is float typedValue
                ? typedValue
                : Convert.ToSingle(value, CultureInfo.InvariantCulture);
            return floatValue.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    public sealed class IntSettingDefinition : SettingDefinition
    {
        public int MinValue { get; }
        public int MaxValue { get; }

        public IntSettingDefinition(
            string key,
            string pageId,
            string displayName,
            string description,
            int defaultValue,
            int minValue,
            int maxValue,
            bool applyImmediately = false,
            bool requiresRestart = false,
            int order = 0,
            ISettingApplyStrategy applyStrategy = null)
            : base(
                key,
                pageId,
                displayName,
                description,
                SettingValueKind.Int,
                Mathf.Clamp(defaultValue, minValue, maxValue),
                applyImmediately,
                requiresRestart,
                order,
                applyStrategy)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public override bool TryNormalize(object rawValue, out object normalizedValue)
        {
            int value;
            switch (rawValue)
            {
                case int intValue:
                    value = intValue;
                    break;
                case float floatValue:
                    value = Mathf.RoundToInt(floatValue);
                    break;
                case string stringValue when int.TryParse(
                    stringValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedInt):
                    value = parsedInt;
                    break;
                default:
                    normalizedValue = DefaultValue;
                    return false;
            }

            normalizedValue = Mathf.Clamp(value, MinValue, MaxValue);
            return true;
        }

        public override string Serialize(object value)
        {
            int intValue = value is int typedValue
                ? typedValue
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return intValue.ToString(CultureInfo.InvariantCulture);
        }

        public override bool TryDeserialize(string rawValue, out object value)
        {
            return TryNormalize(rawValue, out value);
        }
    }

    public sealed class StringSettingDefinition : SettingDefinition
    {
        public int MaxLength { get; }

        public StringSettingDefinition(
            string key,
            string pageId,
            string displayName,
            string description,
            string defaultValue,
            int maxLength = 128,
            bool applyImmediately = false,
            bool requiresRestart = false,
            int order = 0,
            ISettingApplyStrategy applyStrategy = null)
            : base(
                key,
                pageId,
                displayName,
                description,
                SettingValueKind.String,
                defaultValue ?? string.Empty,
                applyImmediately,
                requiresRestart,
                order,
                applyStrategy)
        {
            MaxLength = maxLength <= 0 ? 128 : maxLength;
        }

        public override bool TryNormalize(object rawValue, out object normalizedValue)
        {
            if (rawValue == null)
            {
                normalizedValue = DefaultValue;
                return false;
            }

            string value = rawValue.ToString() ?? string.Empty;
            if (value.Length > MaxLength)
            {
                value = value.Substring(0, MaxLength);
            }

            normalizedValue = value;
            return true;
        }

        public override string Serialize(object value)
        {
            return value?.ToString() ?? string.Empty;
        }

        public override bool TryDeserialize(string rawValue, out object value)
        {
            return TryNormalize(rawValue ?? string.Empty, out value);
        }
    }

    public sealed class ChoiceSettingDefinition : SettingDefinition
    {
        private readonly Dictionary<string, SettingChoiceOption> _optionLookup;

        public IReadOnlyList<SettingChoiceOption> Options { get; }

        public ChoiceSettingDefinition(
            string key,
            string pageId,
            string displayName,
            string description,
            string defaultValue,
            IReadOnlyList<SettingChoiceOption> options,
            bool applyImmediately = false,
            bool requiresRestart = false,
            int order = 0,
            ISettingApplyStrategy applyStrategy = null)
            : base(
                key,
                pageId,
                displayName,
                description,
                SettingValueKind.Choice,
                ResolveDefaultValue(defaultValue, options),
                applyImmediately,
                requiresRestart,
                order,
                applyStrategy)
        {
            Options = options ?? Array.Empty<SettingChoiceOption>();
            _optionLookup = BuildOptionLookup(Options);
        }

        public override bool TryNormalize(object rawValue, out object normalizedValue)
        {
            string value = rawValue?.ToString() ?? string.Empty;
            if (_optionLookup.ContainsKey(value))
            {
                normalizedValue = value;
                return true;
            }

            normalizedValue = DefaultValue;
            return false;
        }

        public override string Serialize(object value)
        {
            return value?.ToString() ?? string.Empty;
        }

        public override bool TryDeserialize(string rawValue, out object value)
        {
            return TryNormalize(rawValue, out value);
        }

        public override string FormatValue(object value)
        {
            string key = value?.ToString() ?? string.Empty;
            return _optionLookup.TryGetValue(key, out SettingChoiceOption option)
                ? option.Label
                : key;
        }

        private static string ResolveDefaultValue(string defaultValue, IReadOnlyList<SettingChoiceOption> options)
        {
            string value = defaultValue ?? string.Empty;
            if (options == null || options.Count == 0)
            {
                return value;
            }

            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Value, value, StringComparison.Ordinal))
                {
                    return value;
                }
            }

            return options[0].Value ?? string.Empty;
        }

        private static Dictionary<string, SettingChoiceOption> BuildOptionLookup(
            IReadOnlyList<SettingChoiceOption> options)
        {
            if (options == null || options.Count == 0)
            {
                return new Dictionary<string, SettingChoiceOption>();
            }

            return options
                .GroupBy(option => option?.Value ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }
    }
}
