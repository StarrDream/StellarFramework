using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace StellarFramework.Tests.PlayMode
{
    public sealed class SaveKitPlayModeTests
    {
        [UnityTest]
        public IEnumerator FileSystemStorageRoundTripAndCancellation()
        {
            string slot = "playmode-" + Guid.NewGuid().ToString("N");
            SaveKit.Initialize(builder => builder.SetApplicationVersion("playmode"));
            var section = new PlayModeSection();
            Assert.That(SaveKit.Register(section), Is.True);
            section.Value = 27;

            SaveResult save = null;
            yield return SaveKit.SaveAsync(slot).ToCoroutine(result => save = result);
            Assert.That(save, Is.Not.Null);
            Assert.That(save.IsSuccess, Is.True, save.ErrorMessage);

            section.Value = 0;
            SaveResult load = null;
            yield return SaveKit.LoadAsync(slot).ToCoroutine(result => load = result);
            Assert.That(load, Is.Not.Null);
            Assert.That(load.IsSuccess, Is.True, load.ErrorMessage);
            Assert.That(section.Value, Is.EqualTo(27));

            section.Value = 22;
            SaveResult secondSave = null;
            yield return SaveKit.SaveAsync(slot).ToCoroutine(result => secondSave = result);
            Assert.That(secondSave.IsSuccess, Is.True, secondSave.ErrorMessage);
            var storage = SaveKit.Storage as FileSystemSaveStorage;
            Assert.That(storage, Is.Not.Null);
            string backupPath = storage.GetFilePath(SaveSlotId.From(slot), SaveStorageFileKind.Backup);
            string currentPath = storage.GetFilePath(SaveSlotId.From(slot), SaveStorageFileKind.Current);
            byte[] backupBefore = File.ReadAllBytes(backupPath);
            byte[] corrupt = File.ReadAllBytes(currentPath);
            corrupt[corrupt.Length - 1] ^= 0x5A;
            File.WriteAllBytes(currentPath, corrupt);
            section.Value = 0;
            SaveResult recovered = null;
            yield return SaveKit.LoadAsync(slot).ToCoroutine(result => recovered = result);
            Assert.That(recovered.IsSuccess, Is.True, recovered.ErrorMessage);
            Assert.That(recovered.UsedBackup, Is.True);
            Assert.That(section.Value, Is.EqualTo(27));
            Assert.That(File.ReadAllBytes(backupPath), Is.EqualTo(backupBefore));
            Assert.That(File.Exists(storage.GetFilePath(SaveSlotId.From(slot), SaveStorageFileKind.Temporary)), Is.False);

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                SaveResult cancelled = null;
                yield return SaveKit.SaveAsync(slot, cancellation.Token).ToCoroutine(result => cancelled = result);
                Assert.That(cancelled, Is.Not.Null);
                Assert.That(cancelled.ErrorCode, Is.EqualTo(SaveErrorCode.Cancelled));
            }

            SaveResult deleted = null;
            yield return SaveKit.DeleteAsync(slot).ToCoroutine(result => deleted = result);
            Assert.That(deleted, Is.Not.Null);
            Assert.That(deleted.IsSuccess, Is.True, deleted.ErrorMessage);
        }

        [Serializable]
        private sealed class PlayModeData
        {
            public int Value;
        }

        private sealed class PlayModeSection : SaveSection<PlayModeData>
        {
            public int Value;
            public override SaveSectionId Id => SaveSectionId.From("playmode.savekit");
            public override PlayModeData Capture(SaveCaptureContext context) => new PlayModeData { Value = Value };
            public override void Restore(PlayModeData data, SaveRestoreContext context) => Value = data == null ? 0 : data.Value;
        }
    }
}
