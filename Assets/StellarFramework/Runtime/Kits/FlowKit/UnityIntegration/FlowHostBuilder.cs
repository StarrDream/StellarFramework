using System;

namespace StellarFramework.FlowKit.Unity
{
    /// <summary>Explicit project extension point applied before a FlowHost becomes initialized.</summary>
    public interface IFlowHostConfigurator
    {
        void Configure(FlowHostBuilder builder);
    }

    /// <summary>Builds one host runtime without assembly scans or hidden service discovery.</summary>
    public sealed class FlowHostBuilder
    {
        public FlowNodeRegistry Nodes { get; }
        public FlowOperationRegistry Operations { get; }
        public FlowCapabilitySet Capabilities { get; }
        public IFlowAssetResolver Assets { get; private set; }
        public IFlowTraceSink Trace { get; private set; }

        public FlowHostBuilder(FlowNodeRegistry nodes = null)
        {
            Nodes = nodes ?? FlowBuiltInNodes.CreateRegistry();
            Operations = new FlowOperationRegistry();
            Capabilities = new FlowCapabilitySet();
        }

        public FlowHostBuilder RegisterNode(FlowNodeDescriptor descriptor, IFlowNodeHandler handler)
        {
            Nodes.Register(descriptor, handler);
            return this;
        }

        public FlowHostBuilder RegisterOperation(string operationId, IFlowOperationAdapter adapter)
        {
            Operations.Register(operationId, adapter);
            return this;
        }

        public FlowHostBuilder AddCapability(FlowCapabilityId capability)
        {
            if (!capability.IsValid) throw new ArgumentException("CapabilityId cannot be empty.", nameof(capability));
            Capabilities.Add(capability);
            return this;
        }

        public FlowHostBuilder SetAssetResolver(IFlowAssetResolver resolver)
        {
            Assets = resolver ?? throw new ArgumentNullException(nameof(resolver));
            return this;
        }

        public FlowHostBuilder SetTraceSink(IFlowTraceSink trace)
        {
            Trace = trace ?? throw new ArgumentNullException(nameof(trace));
            return this;
        }

        internal FlowRuntimeServices BuildServices() => new FlowRuntimeServices(
            operations: Operations,
            capabilities: Capabilities,
            trace: Trace,
            assets: Assets);
    }
}
