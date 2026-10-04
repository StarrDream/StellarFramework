using System;
using System.Text;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>从安全的外部来源解析发布凭证；调用方不得记录或持久化返回的 Secret。</summary>
    public interface IHotUpdateCredentialProvider
    {
        bool TryGetSecret(string credentialProfileName, out string secret);
    }

    /// <summary>
    /// V1 credential provider。变量名格式为 STELLAR_HOTUPDATE_ 加大写 profile name；
    /// 例如 ProductionCdn 对应 STELLAR_HOTUPDATE_PRODUCTIONCDN。
    /// </summary>
    public sealed class EnvironmentVariableCredentialProvider : IHotUpdateCredentialProvider
    {
        public const string EnvironmentVariablePrefix = "STELLAR_HOTUPDATE_";

        public bool TryGetSecret(string credentialProfileName, out string secret)
        {
            secret = null;
            if (!TryGetEnvironmentVariableName(credentialProfileName, out string variableName)) return false;

            string value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrEmpty(value)) return false;

            secret = value;
            return true;
        }

        /// <summary>生成环境变量名；非法凭证 profile name 不会进入环境变量查找。</summary>
        public static bool TryGetEnvironmentVariableName(string credentialProfileName, out string variableName)
        {
            variableName = string.Empty;
            if (string.IsNullOrWhiteSpace(credentialProfileName)) return false;

            var normalized = new StringBuilder(credentialProfileName.Length);
            for (int index = 0; index < credentialProfileName.Length; index++)
            {
                char character = credentialProfileName[index];
                bool asciiLetter = (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z');
                bool asciiDigit = character >= '0' && character <= '9';
                if (!asciiLetter && !asciiDigit && character != '_' && character != '-') return false;
                normalized.Append(character == '-' ? '_' : char.ToUpperInvariant(character));
            }

            variableName = EnvironmentVariablePrefix + normalized;
            return true;
        }
    }
}
