using System;
using System.Collections.Generic;
using StellarFramework.FlowKit;

namespace StellarFramework.Editor.Modules.FlowKit
{
    internal static class FlowKitEditorLocalization
    {
        private static readonly Dictionary<string, string> NodeNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "flow.entry", "流程入口" },
            { "flow.pass", "继续" },
            { "flow.complete", "结束流程" },
            { "flow.fail", "流程失败" },
            { "flow.branch.bool", "布尔分支" },
            { "flow.branch.condition", "条件分支" },
            { "flow.delay", "延时" },
            { "flow.wait.signal", "等待信号" },
            { "flow.wait.state", "等待状态" },
            { "flow.stable.for", "状态持续稳定" },
            { "flow.wait.blackboard", "等待黑板值" },
            { "flow.set.blackboard", "设置黑板值" },
            { "flow.increment.blackboard", "累加黑板数值" },
            { "flow.emit.signal", "发送信号" },
            { "flow.operation", "调用外部操作" },
            { "flow.parallel", "并行" },
            { "flow.race", "竞速" },
            { "flow.join", "汇合" }
        };

        private static readonly Dictionary<string, string> PropertyNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "value", "值" },
            { "blackboardKey", "黑板键" },
            { "seconds", "秒数" },
            { "timeDomain", "时间域" },
            { "signal", "信号 ID" },
            { "scope", "作用域" },
            { "sourceKey", "来源键" },
            { "state", "状态 ID" },
            { "expected", "期望值" },
            { "waitMode", "等待模式" },
            { "key", "键" },
            { "persistence", "持久化" },
            { "amount", "增量" },
            { "payload", "载荷" },
            { "operation", "操作 ID" },
            { "message", "失败原因" }
        };

        private static readonly Dictionary<string, string> PortNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "in", "进入" }, { "next", "下一步" }, { "completed", "完成" },
            { "true", "是" }, { "false", "否" }, { "received", "已收到" },
            { "changed", "已满足" }, { "stable", "已稳定" }, { "succeeded", "成功" },
            { "failed", "失败" }, { "cancelled", "已取消" }, { "branch", "分支" },
            { "joined", "已汇合" }
        };

        public static string NodeName(FlowNodeDescriptor descriptor)
        {
            if (descriptor == null) return "未知节点";
            return NodeNames.TryGetValue(descriptor.TypeId.Value, out string value)
                ? value
                : descriptor.DisplayName;
        }

        public static string Category(FlowNodeDescriptor descriptor)
        {
            if (descriptor == null) return "其他";
            string typeId = descriptor.TypeId.Value;
            if (typeId == "flow.entry" || typeId == "flow.pass" || typeId == "flow.complete" || typeId == "flow.fail" ||
                (typeId == "flow.branch.bool" || typeId == "flow.branch.condition") || typeId == "flow.parallel" || typeId == "flow.race" || typeId == "flow.join")
                return "流程控制";
            if (typeId == "flow.delay" || typeId.StartsWith("flow.wait.", StringComparison.Ordinal) || typeId == "flow.stable.for")
                return "等待";
            if (typeId.Contains("blackboard")) return "流程数据";
            if (typeId == "flow.operation" || typeId == "flow.emit.signal") return "外部集成";
            return string.IsNullOrEmpty(descriptor.Category) ? "其他" : descriptor.Category;
        }

        public static string PropertyName(FlowPropertyDescriptor descriptor)
        {
            if (descriptor == null) return string.Empty;
            return PropertyNames.TryGetValue(descriptor.Key, out string value) ? value : descriptor.DisplayName;
        }

        public static string PortName(string portId) =>
            PortNames.TryGetValue(portId ?? string.Empty, out string value) ? value : portId;

        public static string ValueKind(FlowValueKind kind)
        {
            switch (kind)
            {
                case FlowValueKind.None: return "无";
                case FlowValueKind.Any: return "任意值";
                case FlowValueKind.Bool: return "布尔";
                case FlowValueKind.Int: return "整数";
                case FlowValueKind.Long: return "长整数";
                case FlowValueKind.Float: return "浮点数";
                case FlowValueKind.Double: return "双精度";
                case FlowValueKind.String: return "文本";
                case FlowValueKind.Vector2: return "二维向量";
                case FlowValueKind.Vector3: return "三维向量";
                case FlowValueKind.Asset: return "资源引用";
                case FlowValueKind.Enum: return "枚举 ID";
                case FlowValueKind.BindingReference: return "场景绑定";
                case FlowValueKind.Binding: return "运行时绑定句柄";
                default: return kind.ToString();
            }
        }

        public static string ValidationCode(FlowValidationErrorCode code)
        {
            switch (code)
            {
                case FlowValidationErrorCode.MissingFlowId: return "缺少 FlowId";
                case FlowValidationErrorCode.InvalidSchemaVersion: return "Schema 版本无效";
                case FlowValidationErrorCode.MissingEntry: return "缺少入口节点";
                case FlowValidationErrorCode.DuplicateNodeId: return "节点 ID 重复";
                case FlowValidationErrorCode.UnknownTypeId: return "未知节点类型";
                case FlowValidationErrorCode.MissingRequiredProperty: return "缺少必填参数";
                case FlowValidationErrorCode.InvalidPropertyType: return "参数类型错误";
                case FlowValidationErrorCode.InvalidCondition: return "条件配置无效";
                case FlowValidationErrorCode.InvalidEdgeNode: return "连线节点无效";
                case FlowValidationErrorCode.UnknownPort: return "端口不存在";
                case FlowValidationErrorCode.DuplicateEdge: return "重复连线";
                case FlowValidationErrorCode.ImmediateCycle: return "即时循环";
                case FlowValidationErrorCode.MissingCapability: return "缺少能力";
                case FlowValidationErrorCode.MissingBinding: return "缺少场景绑定";
                case FlowValidationErrorCode.MissingAsset: return "缺少资源";
                case FlowValidationErrorCode.UnroutedRecommendedOutput: return "建议处理失败分支";
                default: return code.ToString();
            }
        }

        public static string ConditionKind(FlowConditionKind kind)
        {
            switch (kind)
            {
                case FlowConditionKind.Constant: return "常量";
                case FlowConditionKind.Blackboard: return "黑板值";
                case FlowConditionKind.State: return "状态值";
                case FlowConditionKind.Compare: return "比较";
                case FlowConditionKind.Not: return "取反（NOT）";
                case FlowConditionKind.All: return "全部满足（AND）";
                case FlowConditionKind.Any: return "任意满足（OR）";
                default: return kind.ToString();
            }
        }

        public static string ComparisonOperator(FlowComparisonOperator op)
        {
            switch (op)
            {
                case FlowComparisonOperator.Equal: return "等于 ==";
                case FlowComparisonOperator.NotEqual: return "不等于 !=";
                case FlowComparisonOperator.Greater: return "大于 >";
                case FlowComparisonOperator.GreaterOrEqual: return "大于等于 >=";
                case FlowComparisonOperator.Less: return "小于 <";
                case FlowComparisonOperator.LessOrEqual: return "小于等于 <=";
                default: return op.ToString();
            }
        }

        public static string EnumValue(string value)
        {
            switch (value)
            {
                case "Scaled": return "受 Time.timeScale 影响";
                case "Unscaled": return "真实时间（不受 timeScale 影响）";
                case "FlowTime": return "流程时间";
                case "RunLocal": return "当前流程";
                case "Host": return "Host 全局";
                case "CurrentOrFuture": return "当前满足或未来满足";
                case "FutureChange": return "等待未来变化";
                case "FutureMatch": return "等待未来匹配";
                case "Transient": return "临时";
                case "Persistent": return "持久化";
                case "Reconstructable": return "可重建";
                default: return value ?? string.Empty;
            }
        }
    }
}
