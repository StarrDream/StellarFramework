using System;
using System.Linq;
using UnityEngine;

namespace StellarFramework
{
    public static class StringExtensions
    {
        public static bool CheckCharMatch(this string incomingStr, string targetStr, int needCount)
        {
            if (string.IsNullOrEmpty(incomingStr) || string.IsNullOrEmpty(targetStr)) return false;
            int matchCounter = 0;
            var tempIncoming = incomingStr.ToList();
            foreach (char c in targetStr)
            {
                if (tempIncoming.Contains(c))
                {
                    matchCounter++;
                    tempIncoming.Remove(c);
                    if (matchCounter >= needCount) return true;
                }
            }

            return false;
        }

        public static string FormatExternalText(this string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return string.Empty;
            var formattedText = rawText;
            formattedText = formattedText.Replace("\\n", "\n");
            formattedText = formattedText.Replace("\\r", "\r");
            formattedText = formattedText.Replace("\\t", "\t");
            formattedText = formattedText.Replace("\\\"", "\"");
            formattedText = formattedText.Replace("\\'", "'");
            formattedText = formattedText.Replace("\\\\", "\\");
            return formattedText;
        }

        public static string ConvertTabsToSpaces(this string text, int tabSpaceCount = 4)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var spaces = new string(' ', tabSpaceCount);
            return text.Replace("\t", spaces);
        }

        public static Vector3 ToVector3(this string posStr, char separator = ',')
        {
            if (string.IsNullOrEmpty(posStr)) return Vector3.zero;
            try
            {
                var parts = posStr.Split(separator);
                if (parts.Length >= 3)
                {
                    var x = float.Parse(parts[0].Trim());
                    var y = float.Parse(parts[1].Trim());
                    var z = float.Parse(parts[2].Trim());
                    return new Vector3(x, y, z);
                }
            }
            catch (Exception e)
            {
                LogKit.LogError($"解析坐标失败: {posStr}, 错误: {e.Message}");
            }

            return Vector3.zero;
        }

        #region 富文本颜色

        public static string Red(this string str) => $"<color=#FF0000>{str}</color>";
        public static string Yellow(this string str) => $"<color=#FFFF00>{str}</color>";
        public static string Green(this string str) => $"<color=#00FF00>{str}</color>";

        private static string GetCurrentTime() => DateTime.Now.ToString("HH:mm:ss");

        #endregion
    }
}