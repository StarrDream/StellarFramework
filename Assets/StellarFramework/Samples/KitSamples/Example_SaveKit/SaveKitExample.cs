using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.Samples.SaveKit
{
    using CoreSaveKit = global::StellarFramework.SaveKit;

    [Serializable]
    public sealed class SaveKitExampleData
    {
        public int Level;
        public long WorldTick;
    }

    public sealed class SaveKitExampleSection : SaveSection<SaveKitExampleData>
    {
        private readonly SaveKitExample _owner;

        public SaveKitExampleSection(SaveKitExample owner)
        {
            _owner = owner;
        }

        public override SaveSectionId Id => SaveSectionId.From("sample.player");
        public override int SchemaVersion => 1;

        public override SaveKitExampleData Capture(SaveCaptureContext context)
        {
            return new SaveKitExampleData
            {
                Level = _owner.Level,
                WorldTick = _owner.WorldTick
            };
        }

        public override void Restore(SaveKitExampleData data, SaveRestoreContext context)
        {
            _owner.Level = data == null ? 1 : data.Level;
            _owner.WorldTick = data == null ? 0 : data.WorldTick;
        }
    }

    public sealed class SaveKitExample : MonoBehaviour
    {
        [SerializeField] private int _level = 1;
        [SerializeField] private long _worldTick;
        private SaveKitExampleSection _section;

        public int Level { get => _level; set => _level = value; }
        public long WorldTick { get => _worldTick; set => _worldTick = value; }

        private void Awake()
        {
            if (!CoreSaveKit.IsInitialized) CoreSaveKit.Initialize();
            _section = new SaveKitExampleSection(this);
            CoreSaveKit.Register(_section);
        }

        public async UniTask<SaveResult> SaveSampleAsync()
        {
            return await CoreSaveKit.SaveAsync("sample-slot");
        }

        public async UniTask<SaveResult> LoadSampleAsync()
        {
            return await CoreSaveKit.LoadAsync("sample-slot");
        }
    }
}
