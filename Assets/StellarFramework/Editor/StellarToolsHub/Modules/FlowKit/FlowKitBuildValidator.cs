using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;

namespace StellarFramework.Editor.Modules.FlowKit
{
    internal sealed class FlowKitBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -900;

        public void OnPreprocessBuild(BuildReport report)
        {
            FlowBuildValidationResult result = ValidateProject();
            if (!result.Succeeded)
                throw new BuildFailedException(result.CreateSummary());
        }

        internal static FlowBuildValidationResult ValidateProject()
        {
            FlowNodeRegistry registry = FlowKitEditorRegistry.Create(out IReadOnlyList<string> registryIssues);
            var result = new FlowBuildValidationResult();
            for (int i = 0; i < registryIssues.Count; i++)
                result.Errors.Add("Registry: " + registryIssues[i]);

            FlowContractCatalogSnapshot contracts = FlowKitContractValidator.BuildSnapshot(result);
            string[] files = Directory.GetFiles(Application.dataPath, "*.flow.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            result.GraphCount = files.Length;

            for (int i = 0; i < files.Length; i++)
                ValidateFile(files[i], registry, contracts, result);

            return result;
        }

        private static void ValidateFile(
            string absolutePath,
            FlowNodeRegistry registry,
            FlowContractCatalogSnapshot contracts,
            FlowBuildValidationResult result)
        {
            string assetPath = ToAssetPath(absolutePath);
            try
            {
                FlowGraphData graph = FlowGraphJson.FromJson(File.ReadAllText(absolutePath));
                FlowCompileResult compile = FlowCompiler.Compile(graph, registry);
                if (!compile.Succeeded)
                {
                    for (int i = 0; i < compile.Issues.Count; i++)
                        if (compile.Issues[i].IsError)
                            result.Errors.Add($"{assetPath}: {compile.Issues[i]}");
                }

                FlowKitContractValidator.ValidateGraph(assetPath, graph, contracts, result);
            }
            catch (Exception exception)
            {
                result.Errors.Add($"{assetPath}: {exception.GetBaseException().Message}");
            }
        }

        private static string ToAssetPath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            string assets = Application.dataPath.Replace('\\', '/');
            return normalized.StartsWith(assets, StringComparison.OrdinalIgnoreCase)
                ? "Assets" + normalized.Substring(assets.Length)
                : normalized;
        }
    }

    internal sealed class FlowBuildValidationResult
    {
        public int GraphCount;
        public int ContractCatalogCount;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool Succeeded => Errors.Count == 0;

        public string CreateSummary()
        {
            if (Succeeded)
            {
                string warningText = Warnings.Count == 0
                    ? string.Empty
                    : $" Warnings={Warnings.Count}.";
                return $"FlowKit build validation passed ({GraphCount} graphs, {ContractCatalogCount} contract catalogs).{warningText}";
            }

            string summary = "FlowKit build validation failed:\n - " + string.Join("\n - ", Errors);
            if (Warnings.Count > 0)
                summary += "\nWarnings:\n - " + string.Join("\n - ", Warnings);
            return summary;
        }
    }
}
