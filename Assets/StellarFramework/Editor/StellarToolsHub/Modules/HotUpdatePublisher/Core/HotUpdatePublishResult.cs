using System;
using System.Collections.Generic;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>一个 Publisher 阶段的明确成功或失败结果。</summary>
    public sealed class HotUpdatePublishStepResult
    {
        private HotUpdatePublishStepResult(
            bool success,
            HotUpdatePublishErrorCode errorCode,
            string error,
            IReadOnlyList<string> warnings)
        {
            Success = success;
            ErrorCode = errorCode;
            Error = error ?? string.Empty;
            Warnings = warnings ?? Array.Empty<string>();
        }

        /// <summary>阶段是否完成。</summary>
        public bool Success { get; }

        /// <summary>机器可判定的错误代码。</summary>
        public HotUpdatePublishErrorCode ErrorCode { get; }

        /// <summary>错误上下文；仅用于诊断和显示，不用于成功判断。</summary>
        public string Error { get; }

        /// <summary>此阶段产生的警告。</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>构造成功阶段结果。</summary>
        public static HotUpdatePublishStepResult Succeeded(params string[] warnings)
        {
            return new HotUpdatePublishStepResult(
                true,
                HotUpdatePublishErrorCode.None,
                string.Empty,
                CopyWarnings(warnings));
        }

        /// <summary>构造失败阶段结果。</summary>
        public static HotUpdatePublishStepResult Failed(
            HotUpdatePublishErrorCode errorCode,
            string error,
            params string[] warnings)
        {
            if (errorCode == HotUpdatePublishErrorCode.None ||
                errorCode == HotUpdatePublishErrorCode.Cancelled)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(errorCode), errorCode, "A failed step must use a non-cancellation error code.");
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                throw new ArgumentException("A failed step requires diagnostic context.", nameof(error));
            }

            return new HotUpdatePublishStepResult(
                false,
                errorCode,
                error,
                CopyWarnings(warnings));
        }

        private static IReadOnlyList<string> CopyWarnings(string[] warnings)
        {
            if (warnings == null || warnings.Length == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new List<string>(warnings.Length);
            for (int index = 0; index < warnings.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(warnings[index]))
                {
                    copy.Add(warnings[index]);
                }
            }

            return copy.ToArray();
        }
    }

    /// <summary>一次 HotUpdate 发布尝试的机器可判定结果。</summary>
    public sealed class HotUpdatePublishResult
    {
        internal HotUpdatePublishResult(
            bool success,
            HotUpdatePublishStage failedStage,
            HotUpdatePublishErrorCode errorCode,
            string error,
            Exception exception,
            TimeSpan duration,
            IReadOnlyList<string> warnings,
            HotUpdateReleaseRecord releaseRecord)
        {
            Success = success;
            FailedStage = failedStage;
            ErrorCode = errorCode;
            Error = error ?? string.Empty;
            Exception = exception;
            Duration = duration;
            Warnings = warnings ?? Array.Empty<string>();
            ReleaseRecord = releaseRecord;
        }

        /// <summary>发布流水线是否完整完成。</summary>
        public bool Success { get; }

        /// <summary>失败或取消时的实际执行阶段；成功时为 None。</summary>
        public HotUpdatePublishStage FailedStage { get; }

        /// <summary>稳定的机器错误代码。</summary>
        public HotUpdatePublishErrorCode ErrorCode { get; }

        /// <summary>错误诊断文本。</summary>
        public string Error { get; }

        /// <summary>未处理阶段异常的原始对象，保留完整诊断堆栈。</summary>
        public Exception Exception { get; }

        /// <summary>完整流水线用时。</summary>
        public TimeSpan Duration { get; }

        /// <summary>执行期间收集的警告。</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>成功 Finalize 后生成的发布记录。</summary>
        public HotUpdateReleaseRecord ReleaseRecord { get; }
    }
}
