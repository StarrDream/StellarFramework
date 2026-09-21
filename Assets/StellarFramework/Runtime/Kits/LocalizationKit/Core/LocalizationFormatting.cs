using System;
using System.Collections.Generic;
using System.Text;

namespace StellarFramework.Localization
{
    /// <summary>
    /// 命名模板参数，例如 {count}=10。
    /// </summary>
    /// <remarks>
    /// 参数名只允许 ASCII 字母、数字以及 _ . -，以保证跨平台和内容工具中的稳定性。
    /// </remarks>
    public readonly struct LocalizationFormatArgument
    {
        /// <summary>模板占位符名称，不包含花括号。</summary>
        public string Name { get; }
        /// <summary>替换文本；null 会规范化为空字符串。</summary>
        public string Value { get; }

        /// <summary>创建格式化参数。</summary>
        public LocalizationFormatArgument(string name, string value)
        {
            if (!TryValidateName(name, out string error))
                throw new ArgumentException(error, nameof(name));
            Name = name;
            Value = value ?? string.Empty;
        }

        internal static bool TryValidateName(string name, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Format argument name cannot be empty.";
                return false;
            }
            if (!string.Equals(name, name.Trim(), StringComparison.Ordinal))
            {
                error = "Format argument name cannot contain leading or trailing whitespace.";
                return false;
            }
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool allowed =
                    (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '-';
                if (!allowed)
                {
                    error = "Format argument name contains unsupported character '" + c + "'.";
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 不依赖 CultureInfo 的轻量命名占位符格式化器。
    /// </summary>
    /// <remarks>
    /// 支持 {name} 占位符以及 {{ / }} 转义。
    /// 只做字符串替换，不承担数字、日期、复数规则或 ICU MessageFormat 语义。
    /// </remarks>
    public static class LocalizationTemplateFormatter
    {
        /// <summary>
        /// 扫描模板并把所有占位符名称写入 destination；写入前会 Clear。
        /// </summary>
        public static bool TryGetArgumentNames(
            string template,
            ISet<string> destination,
            out string error)
        {
            error = null;
            if (template == null)
            {
                error = "Localization template cannot be null.";
                return false;
            }
            if (destination == null)
            {
                error = "Argument-name destination cannot be null.";
                return false;
            }

            destination.Clear();
            int index = 0;
            while (index < template.Length)
            {
                char c = template[index];
                if (c == '{')
                {
                    if (index + 1 < template.Length && template[index + 1] == '{')
                    {
                        index += 2;
                        continue;
                    }
                    if (!TryReadPlaceholder(template, index, out string name, out index, out error))
                        return false;
                    destination.Add(name);
                    continue;
                }
                if (c == '}')
                {
                    if (index + 1 < template.Length && template[index + 1] == '}')
                    {
                        index += 2;
                        continue;
                    }
                    error = "Localization template contains an unmatched closing brace.";
                    return false;
                }
                index++;
            }
            return true;
        }

        /// <summary>
        /// 使用命名参数格式化模板。
        /// </summary>
        /// <returns>
        /// 所有占位符均合法且都有唯一对应参数时返回 true；
        /// 缺参数、重复参数或括号语法错误时返回 false。
        /// </returns>
        public static bool TryFormat(
            string template,
            ReadOnlySpan<LocalizationFormatArgument> arguments,
            out string result,
            out string error)
        {
            result = null;
            error = null;
            if (template == null)
            {
                error = "Localization template cannot be null.";
                return false;
            }

            for (int i = 0; i < arguments.Length; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(arguments[i].Name, arguments[j].Name, StringComparison.Ordinal))
                    {
                        error = "Duplicate format argument '" + arguments[i].Name + "'.";
                        return false;
                    }
                }
            }

            var builder = new StringBuilder(template.Length + 16);
            int index = 0;
            while (index < template.Length)
            {
                char c = template[index];
                if (c == '{')
                {
                    if (index + 1 < template.Length && template[index + 1] == '{')
                    {
                        builder.Append('{');
                        index += 2;
                        continue;
                    }
                    if (!TryReadPlaceholder(template, index, out string name, out int nextIndex, out error))
                        return false;
                    if (!TryFind(arguments, name, out string value))
                    {
                        error = "Missing format argument '" + name + "'.";
                        return false;
                    }
                    builder.Append(value);
                    index = nextIndex;
                    continue;
                }
                if (c == '}')
                {
                    if (index + 1 < template.Length && template[index + 1] == '}')
                    {
                        builder.Append('}');
                        index += 2;
                        continue;
                    }
                    error = "Localization template contains an unmatched closing brace.";
                    return false;
                }
                builder.Append(c);
                index++;
            }

            result = builder.ToString();
            return true;
        }

        private static bool TryReadPlaceholder(
            string template,
            int openIndex,
            out string name,
            out int nextIndex,
            out string error)
        {
            name = null;
            nextIndex = openIndex;
            error = null;
            int end = template.IndexOf('}', openIndex + 1);
            if (end < 0)
            {
                error = "Localization template contains an unclosed placeholder.";
                return false;
            }

            name = template.Substring(openIndex + 1, end - openIndex - 1);
            if (!LocalizationFormatArgument.TryValidateName(name, out error))
                return false;
            nextIndex = end + 1;
            return true;
        }

        private static bool TryFind(
            ReadOnlySpan<LocalizationFormatArgument> arguments,
            string name,
            out string value)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i].Name, name, StringComparison.Ordinal))
                {
                    value = arguments[i].Value;
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
