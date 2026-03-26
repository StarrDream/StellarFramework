using System;
using UnityEngine;

namespace StellarFramework
{
    public static class StringExtensions
    {
        /// <summary>
        /// 检查两个字符串的字符匹配数量是否达到指定值
        /// 我使用固定计数数组替代 ToList 与 Remove，避免额外 GC 与 O(N²) 级别字符删除开销。
        /// 这里按 char 逐个匹配，适合常规 ASCII 与大多数短文本场景，不承担复杂自然语言分词职责。
        /// </summary>
        public static bool CheckCharMatch(this string incomingStr, string targetStr, int needCount)
        {
            if (needCount <= 0)
            {
                LogKit.LogError(
                    $"[StringExtensions] CheckCharMatch 失败: needCount 非法, NeedCount={needCount}, Incoming={incomingStr}, Target={targetStr}");
                return false;
            }

            if (string.IsNullOrEmpty(incomingStr) || string.IsNullOrEmpty(targetStr))
            {
                return false;
            }

            if (incomingStr.Length < needCount || targetStr.Length < needCount)
            {
                return false;
            }

            int[] counts = new int[char.MaxValue + 1];
            for (int i = 0; i < incomingStr.Length; i++)
            {
                counts[incomingStr[i]]++;
            }

            int matchCounter = 0;
            for (int i = 0; i < targetStr.Length; i++)
            {
                char c = targetStr[i];
                if (counts[c] <= 0)
                {
                    continue;
                }

                counts[c]--;
                matchCounter++;
                if (matchCounter >= needCount)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 格式化外部文本中的转义字符
        /// 我只做字面级替换，不尝试承担 JSON 反序列化或脚本解释器职责。
        /// </summary>
        public static string FormatExternalText(this string rawText)
        {
            if (string.IsNullOrEmpty(rawText))
            {
                return string.Empty;
            }

            string formattedText = rawText;
            formattedText = formattedText.Replace("\\n", "\n");
            formattedText = formattedText.Replace("\\r", "\r");
            formattedText = formattedText.Replace("\\t", "\t");
            formattedText = formattedText.Replace("\\\"", "\"");
            formattedText = formattedText.Replace("\\'", "'");
            formattedText = formattedText.Replace("\\\\", "\\");
            return formattedText;
        }

        /// <summary>
        /// 将 Tab 转换为空格
        /// </summary>
        public static string ConvertTabsToSpaces(this string text, int tabSpaceCount = 4)
        {
            if (tabSpaceCount <= 0)
            {
                LogKit.LogError(
                    $"[StringExtensions] ConvertTabsToSpaces 失败: tabSpaceCount 非法, TabSpaceCount={tabSpaceCount}, TextLength={(text == null ? -1 : text.Length)}");
                return text ?? string.Empty;
            }

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string spaces = new string(' ', tabSpaceCount);
            return text.Replace("\t", spaces);
        }

        /// <summary>
        /// 将字符串解析为 Vector3
        /// 我使用 TryParse 显式处理失败路径，拒绝通过 try-catch 掩盖普通格式错误。
        /// </summary>
        public static Vector3 ToVector3(this string posStr, char separator = ',')
        {
            if (string.IsNullOrWhiteSpace(posStr))
            {
                LogKit.LogError("[StringExtensions] ToVector3 失败: posStr 为空");
                return Vector3.zero;
            }

            string[] parts = posStr.Split(separator);
            if (parts == null || parts.Length < 3)
            {
                LogKit.LogError(
                    $"[StringExtensions] ToVector3 失败: 字段数量不足, Raw={posStr}, Separator={separator}, PartsCount={(parts == null ? -1 : parts.Length)}");
                return Vector3.zero;
            }

            if (!float.TryParse(parts[0].Trim(), out float x))
            {
                LogKit.LogError($"[StringExtensions] ToVector3 失败: X 解析失败, Raw={posStr}, XPart={parts[0]}");
                return Vector3.zero;
            }

            if (!float.TryParse(parts[1].Trim(), out float y))
            {
                LogKit.LogError($"[StringExtensions] ToVector3 失败: Y 解析失败, Raw={posStr}, YPart={parts[1]}");
                return Vector3.zero;
            }

            if (!float.TryParse(parts[2].Trim(), out float z))
            {
                LogKit.LogError($"[StringExtensions] ToVector3 失败: Z 解析失败, Raw={posStr}, ZPart={parts[2]}");
                return Vector3.zero;
            }

            return new Vector3(x, y, z);
        }

        #region 富文本颜色

        public static string Red(this string str)
        {
            if (str == null)
            {
                LogKit.LogError("[StringExtensions] Red 失败: str 为空");
                return string.Empty;
            }

            return $"<color=#FF0000>{str}</color>";
        }

        public static string Yellow(this string str)
        {
            if (str == null)
            {
                LogKit.LogError("[StringExtensions] Yellow 失败: str 为空");
                return string.Empty;
            }

            return $"<color=#FFFF00>{str}</color>";
        }

        public static string Green(this string str)
        {
            if (str == null)
            {
                LogKit.LogError("[StringExtensions] Green 失败: str 为空");
                return string.Empty;
            }

            return $"<color=#00FF00>{str}</color>";
        }

        /// <summary>
        /// 获取当前时间字符串
        /// 我保留这个私有方法供后续扩展使用，但不主动暴露到公共 API，避免无明确用途的工具污染接口面。
        /// </summary>
        private static string GetCurrentTime()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        #endregion
    }
}