using System;
using System.Text;

namespace StellarFramework.Localization
{
    /// <summary>
    /// 规范化后的语言/地区标识，例如 zh-CN、en-US。
    /// </summary>
    /// <remarks>
    /// 创建时会稳定规范大小写：language 小写、2 位 region 大写、4 位 script 首字母大写。
    /// 该结构使用稳定哈希，适合作为 Catalog/Dictionary Key。
    /// </remarks>
    public readonly struct LocaleId : IEquatable<LocaleId>, IComparable<LocaleId>
    {
        /// <summary>Locale 字符串允许的最大长度。</summary>
        public const int MaxLength = 64;
        private readonly string _value;

        /// <summary>规范化后的字符串；default(LocaleId) 返回空字符串。</summary>
        public string Value => _value ?? string.Empty;
        /// <summary>是否包含有效 Locale。</summary>
        public bool IsValid => !string.IsNullOrEmpty(_value);

        private LocaleId(string value) => _value = value;

        /// <summary>
        /// 创建并规范化 LocaleId；输入非法时抛出 <see cref="ArgumentException"/>。
        /// </summary>
        public static LocaleId From(string value)
        {
            if (!TryCreate(value, out LocaleId result, out string error))
                throw new ArgumentException(error, nameof(value));
            return result;
        }

        /// <summary>
        /// 尝试验证并规范化 Locale 字符串。
        /// </summary>
        public static bool TryCreate(string value, out LocaleId result, out string error)
        {
            result = default(LocaleId);
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Locale ID cannot be empty.";
                return false;
            }
            if (value.Length > MaxLength)
            {
                error = "Locale ID exceeds max length " + MaxLength + ".";
                return false;
            }
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                error = "Locale ID cannot contain leading or trailing whitespace.";
                return false;
            }

            string[] segments = value.Split('-');
            if (segments.Length == 0 || segments[0].Length < 2 || segments[0].Length > 8 ||
                !LocalizationStableIdUtility.IsAsciiLetters(segments[0]))
            {
                error = "Locale language segment must contain 2-8 ASCII letters.";
                return false;
            }

            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (segment.Length == 0 || segment.Length > 8 ||
                    !LocalizationStableIdUtility.IsAsciiAlphaNumeric(segment))
                {
                    error = "Locale segments after the language must contain 1-8 ASCII letters or digits.";
                    return false;
                }
            }

            result = new LocaleId(Canonicalize(segments));
            return true;
        }

        public bool Equals(LocaleId other) =>
            string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LocaleId other && Equals(other);
        public override int GetHashCode() => LocalizationStableIdUtility.GetStableHashCode(_value);
        public int CompareTo(LocaleId other) => string.Compare(_value, other._value, StringComparison.Ordinal);
        public override string ToString() => Value;
        public static bool operator ==(LocaleId left, LocaleId right) => left.Equals(right);
        public static bool operator !=(LocaleId left, LocaleId right) => !left.Equals(right);

        private static string Canonicalize(string[] segments)
        {
            var builder = new StringBuilder(segments.Length * 8);
            builder.Append(segments[0].ToLowerInvariant());
            for (int i = 1; i < segments.Length; i++)
            {
                builder.Append('-');
                string segment = segments[i];
                if (segment.Length == 2 && LocalizationStableIdUtility.IsAsciiLetters(segment))
                {
                    builder.Append(segment.ToUpperInvariant());
                }
                else if (segment.Length == 4 && LocalizationStableIdUtility.IsAsciiLetters(segment))
                {
                    builder.Append(char.ToUpperInvariant(segment[0]));
                    builder.Append(segment.Substring(1).ToLowerInvariant());
                }
                else
                {
                    builder.Append(segment.ToLowerInvariant());
                }
            }
            return builder.ToString();
        }
    }

    /// <summary>
    /// 本地化文本的稳定键，例如 menu.start、architecture.panel.coin。
    /// </summary>
    /// <remarks>
    /// Key 区分大小写，只允许 ASCII 字母、数字以及 . _ - / :。
    /// 项目建议统一使用小写分段命名，避免内容侧出现仅大小写不同的重复键。
    /// </remarks>
    public readonly struct LocalizationKey : IEquatable<LocalizationKey>, IComparable<LocalizationKey>
    {
        /// <summary>Key 最大长度。</summary>
        public const int MaxLength = 256;
        private readonly string _value;

        /// <summary>Key 字符串；default(LocalizationKey) 返回空字符串。</summary>
        public string Value => _value ?? string.Empty;
        /// <summary>是否包含有效 Key。</summary>
        public bool IsValid => !string.IsNullOrEmpty(_value);

        private LocalizationKey(string value) => _value = value;

        /// <summary>
        /// 创建 LocalizationKey；输入非法时抛出 <see cref="ArgumentException"/>。
        /// </summary>
        public static LocalizationKey From(string value)
        {
            if (!TryCreate(value, out LocalizationKey result, out string error))
                throw new ArgumentException(error, nameof(value));
            return result;
        }

        /// <summary>
        /// 尝试验证 Key 的字符集合、长度与首尾空白。
        /// </summary>
        public static bool TryCreate(string value, out LocalizationKey result, out string error)
        {
            result = default(LocalizationKey);
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Localization key cannot be empty.";
                return false;
            }
            if (value.Length > MaxLength)
            {
                error = "Localization key exceeds max length " + MaxLength + ".";
                return false;
            }
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                error = "Localization key cannot contain leading or trailing whitespace.";
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool allowed =
                    (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '.' || c == '_' ||
                    c == '-' || c == '/' || c == ':';
                if (!allowed)
                {
                    error = "Localization key contains unsupported character '" + c + "'.";
                    return false;
                }
            }

            result = new LocalizationKey(value);
            return true;
        }

        public bool Equals(LocalizationKey other) =>
            string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LocalizationKey other && Equals(other);
        public override int GetHashCode() => LocalizationStableIdUtility.GetStableHashCode(_value);
        public int CompareTo(LocalizationKey other) => string.Compare(_value, other._value, StringComparison.Ordinal);
        public override string ToString() => Value;
        public static bool operator ==(LocalizationKey left, LocalizationKey right) => left.Equals(right);
        public static bool operator !=(LocalizationKey left, LocalizationKey right) => !left.Equals(right);
    }

    internal static class LocalizationStableIdUtility
    {
        internal static bool IsAsciiLetters(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))) return false;
            }
            return true;
        }

        internal static bool IsAsciiAlphaNumeric(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9'))) return false;
            }
            return true;
        }

        internal static int GetStableHashCode(string value)
        {
            if (value == null) return 0;
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }
}
