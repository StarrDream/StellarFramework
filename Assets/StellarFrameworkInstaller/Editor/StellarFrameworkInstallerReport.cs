using System.Collections.Generic;
using System.Linq;

namespace StellarFrameworkInstaller
{
    internal sealed class StellarFrameworkInstallerReport
    {
        private readonly List<string> _messages = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _errors = new List<string>();

        public IReadOnlyList<string> Messages => _messages;
        public IReadOnlyList<string> Warnings => _warnings;
        public IReadOnlyList<string> Errors => _errors;
        public bool IsValid => _errors.Count == 0;

        public string Summary
        {
            get
            {
                if (_errors.Count > 0)
                {
                    return _errors.Last();
                }

                if (_warnings.Count > 0)
                {
                    return _warnings.Last();
                }

                return _messages.Count > 0 ? _messages.Last() : "Ready";
            }
        }

        public void AddMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _messages.Add(message.Trim());
            }
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _warnings.Add(message.Trim());
            }
        }

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _errors.Add(message.Trim());
            }
        }

        public void Clear()
        {
            _messages.Clear();
            _warnings.Clear();
            _errors.Clear();
        }
    }
}
