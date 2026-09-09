using System;
using StellarFramework.FlowKit;
using StellarFramework.FlowKit.Unity;
using UnityEngine;

namespace StellarFramework.Samples.FlowKit
{
    /// <summary>
    /// Sample Adapter：把 FlowKit 的 Operation Command 翻译成消防玩法系统调用。
    /// 真实项目应把这里替换为 UI/交互/任务/网络等业务 Service 的适配器。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FireDrillFlowSampleAdapter : MonoBehaviour, IFlowHostConfigurator, IFlowOperationAdapter
    {
        [SerializeField] private FireDrillFlowSample gameplay;

        public void Configure(FlowHostBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (gameplay == null) throw new InvalidOperationException("FireDrillFlowSampleAdapter 缺少 gameplay 引用。");

            for (int i = 0; i < FireDrillSampleIds.Operations.Length; i++)
                builder.RegisterOperation(FireDrillSampleIds.Operations[i], this);
        }

        void IFlowOperationAdapter.Start(
            in FlowOperationContext context,
            in FlowOperationRequest request,
            FlowOperationHandle handle,
            Action<FlowOperationResult> complete)
        {
            if (complete == null) throw new ArgumentNullException(nameof(complete));

            if (gameplay.ShouldFailOperation(request.OperationId, out string failureReason))
            {
                gameplay.ReceiveOperationFailure(request.OperationId, failureReason);
                complete(FlowOperationResult.Failure(failureReason));
                return;
            }

            gameplay.ReceiveOperationCommand(request.OperationId, request);
            complete(FlowOperationResult.Success());
        }

        void IFlowOperationAdapter.Cancel(in FlowOperationContext context, FlowOperationHandle handle)
        {
            // 本 Sample 的 Operation 都是主线程即时命令，因此没有长期持有的外部异步句柄。
            // 真实 Adapter 若启动了 SDK/网络/动画任务，应在这里取消对应外部操作。
        }
    }
}
