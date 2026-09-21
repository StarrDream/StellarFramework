using System;
using UnityEngine;

namespace StellarFramework.Localization.UnityUGUI
{
    [Serializable]
    public sealed class LocalizationAuthoringEntry
    {
        [SerializeField] private string _key;
        [SerializeField, TextArea(1, 6)] private string _value;

        public string Key => _key ?? string.Empty;
        public string Value => _value ?? string.Empty;

        public LocalizationAuthoringEntry() { }

        public LocalizationAuthoringEntry(string key, string value)
        {
            _key = key;
            _value = value;
        }
    }
}
