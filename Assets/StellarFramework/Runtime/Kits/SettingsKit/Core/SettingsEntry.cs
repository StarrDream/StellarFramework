namespace StellarFramework.Settings
{
    public sealed class SettingEntry
    {
        public SettingDefinition Definition { get; }
        public object SavedValue { get; private set; }
        public object CurrentValue { get; private set; }
        public string LastError { get; private set; }

        public bool IsDirty => !Equals(SavedValue, CurrentValue);

        public SettingEntry(SettingDefinition definition, object initialValue)
        {
            Definition = definition;
            SavedValue = initialValue;
            CurrentValue = initialValue;
        }

        public void SetCurrentValue(object value)
        {
            CurrentValue = value;
        }

        public void MarkSaved()
        {
            SavedValue = CurrentValue;
            LastError = null;
        }

        public void SetSavedValue(object value)
        {
            SavedValue = value;
            CurrentValue = value;
            LastError = null;
        }

        public void SetError(string error)
        {
            LastError = error;
        }

        public T GetValue<T>()
        {
            return (T)CurrentValue;
        }
    }
}
