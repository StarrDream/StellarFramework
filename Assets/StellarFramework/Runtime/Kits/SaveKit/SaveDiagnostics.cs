using System;
using System.Collections.Generic;

namespace StellarFramework
{
    public sealed class SaveOperationDiagnostics
    {
        public string Operation { get; internal set; }
        public string LastOperation => Operation;
        public SaveSlotId SlotId { get; internal set; }
        public long Revision { get; internal set; }
        public SaveOperationStatus Result { get; internal set; }
        public double CaptureDurationMs { get; internal set; }
        public double SerializeDurationMs { get; internal set; }
        public double ChecksumDurationMs { get; internal set; }
        public double IoDurationMs { get; internal set; }
        public double CommitDurationMs { get; internal set; }
        public double TotalDurationMs { get; internal set; }
        public int SectionCount { get; internal set; }
        public long RawBytes { get; internal set; }
        public long FinalBytes { get; internal set; }
        public bool BackupUsed { get; internal set; }
        public int MigrationCount { get; internal set; }
        public SaveErrorCode LastError { get; internal set; }
        public string LastErrorMessage { get; internal set; }
        private List<SaveSectionDiagnostics> _sections = new List<SaveSectionDiagnostics>();
        public IReadOnlyList<SaveSectionDiagnostics> Sections => _sections;

        internal SaveSectionDiagnostics GetOrCreateSection(SaveSectionId id)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                if (_sections[i].SectionId == id) return _sections[i];
            }

            var diagnostics = new SaveSectionDiagnostics { SectionId = id };
            _sections.Add(diagnostics);
            return diagnostics;
        }

        public SaveOperationDiagnostics Clone()
        {
            var clone = (SaveOperationDiagnostics)MemberwiseClone();
            clone._sections = new List<SaveSectionDiagnostics>();
            foreach (SaveSectionDiagnostics section in _sections)
            {
                clone._sections.Add(section.Clone());
            }

            return clone;
        }
    }

    public sealed class SaveSectionDiagnostics
    {
        public SaveSectionId SectionId { get; internal set; }
        public double CaptureDurationMs { get; internal set; }
        public double SerializeDurationMs { get; internal set; }
        public long PayloadBytes { get; internal set; }
        public int MigrationSteps { get; internal set; }
        public double ValidationDurationMs { get; internal set; }

        internal SaveSectionDiagnostics Clone()
        {
            return (SaveSectionDiagnostics)MemberwiseClone();
        }
    }
}
