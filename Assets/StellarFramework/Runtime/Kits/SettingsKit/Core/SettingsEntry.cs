namespace StellarFramework.Settings
{
    /// <summary>
    /// 一个 SettingDefinition 的运行时状态。
    /// </summary>
    /// <remarks>
    /// SavedValue 表示最后一次成功保存/加载的值；CurrentValue 表示当前 UI/运行时编辑值。
    /// 两者不相等即为 Dirty。ApplyPending 只应用 CurrentValue，不会把它标记为 Saved；
    /// 只有 Save 或加载/回退流程会更新 SavedValue。
    /// </remarks>
    public sealed class SettingEntry
    {
        /// <summary>静态定义与类型规则。</summary>
        public SettingDefinition Definition { get; }
        /// <summary>最后一次持久化基线值。</summary>
        public object SavedValue { get; private set; }
        /// <summary>当前编辑值。</summary>
        public object CurrentValue { get; private set; }
        /// <summary>最近一次 Normalize/Apply 失败信息。</summary>
        public string LastError { get; private set; }

        /// <summary>当前值是否与持久化基线不同。</summary>
        public bool IsDirty => !Equals(SavedValue, CurrentValue);

        public SettingEntry(SettingDefinition definition, object initialValue)
        {
            Definition = definition;
            SavedValue = initialValue;
            CurrentValue = initialValue;
        }

        /// <summary>更新当前值，不改变 SavedValue。</summary>
        public void SetCurrentValue(object value)
        {
            CurrentValue = value;
        }

        /// <summary>将当前值标记为已保存并清除错误。</summary>
        public void MarkSaved()
        {
            SavedValue = CurrentValue;
            LastError = null;
        }

        /// <summary>同时替换 SavedValue/CurrentValue，用于加载或确定的回退值。</summary>
        public void SetSavedValue(object value)
        {
            SavedValue = value;
            CurrentValue = value;
            LastError = null;
        }

        /// <summary>记录当前 Entry 的错误信息。</summary>
        public void SetError(string error)
        {
            LastError = error;
        }

        /// <summary>
        /// 以调用方确认的类型取得 CurrentValue。
        /// 类型不匹配会按普通强制转换语义抛异常。
        /// </summary>
        public T GetValue<T>()
        {
            return (T)CurrentValue;
        }
    }
}
