using UnityEngine;

namespace StellarFramework
{
    public static class ColorExtensions
    {
        /// <summary>
        /// 返回一个新的颜色，仅修改Alpha值
        /// </summary>
        public static Color WithAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>
        /// 转换为十六进制字符串 (#RRGGBB)
        /// </summary>
        public static string ToHex(this Color color, bool includeAlpha = false)
        {
            return includeAlpha ? ColorUtility.ToHtmlStringRGBA(color) : ColorUtility.ToHtmlStringRGB(color);
        }

        /// <summary>
        /// 从十六进制字符串解析颜色
        /// </summary>
        public static Color ParseHex(this string hex)
        {
            if (!hex.StartsWith("#")) hex = "#" + hex;
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}