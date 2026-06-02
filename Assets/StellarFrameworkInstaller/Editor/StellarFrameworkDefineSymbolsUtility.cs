using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace StellarFrameworkInstaller
{
    internal static class StellarFrameworkDefineSymbolsUtility
    {
        public static string MergeDefineSymbols(string currentSymbols, params string[] requiredSymbols)
        {
            List<string> symbols = SplitSymbols(currentSymbols);
            if (requiredSymbols == null)
            {
                return string.Join(";", symbols);
            }

            foreach (string symbol in requiredSymbols)
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    continue;
                }

                string trimmed = symbol.Trim();
                if (!symbols.Any(existing => string.Equals(existing, trimmed, StringComparison.Ordinal)))
                {
                    symbols.Add(trimmed);
                }
            }

            return string.Join(";", symbols);
        }

        public static List<string> SplitSymbols(string symbols)
        {
            if (string.IsNullOrWhiteSpace(symbols))
            {
                return new List<string>();
            }

            return symbols
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public static void AddDefinesForSelectedBuildTarget(params string[] requiredSymbols)
        {
            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
#if UNITY_2021_2_OR_NEWER
            UnityEditor.Build.NamedBuildTarget namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            string current = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, MergeDefineSymbols(current, requiredSymbols));
#else
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, MergeDefineSymbols(current, requiredSymbols));
#endif
        }
    }
}
