using System;

namespace StellarFramework.FlowKit
{
    /// <summary>
    /// Explicit reusable registration unit for project-defined Flow nodes.
    /// Runtime never scans assemblies for this interface; hosts install providers explicitly.
    /// Editor tooling may discover providers with UnityEditor.TypeCache.
    /// </summary>
    public interface IFlowNodeRegistryProvider
    {
        void Register(FlowNodeRegistry registry);
    }

    public static class FlowNodeRegistryProviderExtensions
    {
        public static FlowNodeRegistry Install(
            this FlowNodeRegistry registry,
            IFlowNodeRegistryProvider provider)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            provider.Register(registry);
            return registry;
        }
    }
}
