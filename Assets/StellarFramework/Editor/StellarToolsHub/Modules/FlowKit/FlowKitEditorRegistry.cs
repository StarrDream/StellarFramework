using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using StellarFramework.FlowKit;

namespace StellarFramework.Editor.Modules.FlowKit
{
    internal static class FlowKitEditorRegistry
    {
        public static FlowNodeRegistry Create(out IReadOnlyList<string> issues)
        {
            var registry = FlowBuiltInNodes.CreateRegistry();
            var errors = new List<string>();
            TypeCache.TypeCollection providerTypes = TypeCache.GetTypesDerivedFrom<IFlowNodeRegistryProvider>();

            for (int i = 0; i < providerTypes.Count; i++)
            {
                Type type = providerTypes[i];
                if (type == null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) continue;
                try
                {
                    var provider = Activator.CreateInstance(type) as IFlowNodeRegistryProvider;
                    if (provider == null) continue;
                    provider.Register(registry);
                }
                catch (MissingMethodException)
                {
                    errors.Add($"{type.FullName}: IFlowNodeRegistryProvider must have a public parameterless constructor for Editor discovery.");
                }
                catch (Exception exception)
                {
                    errors.Add($"{type.FullName}: {exception.GetBaseException().Message}");
                }
            }

            issues = errors;
            return registry;
        }

        public static FlowNodeRegistry Create()
        {
            FlowNodeRegistry registry = Create(out IReadOnlyList<string> issues);
            for (int i = 0; i < issues.Count; i++)
                Debug.LogError("FlowKit Editor Registry: " + issues[i]);
            return registry;
        }
    }
}
