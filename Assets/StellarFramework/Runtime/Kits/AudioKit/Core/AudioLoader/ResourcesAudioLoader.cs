using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework.Audio
{
    /// <summary>
    /// AudioKit.Core 的默认加载策略，只依赖 Unity Resources。
    /// 需要其他资源系统时，请传入自定义 IAudioLoader，或安装对应 Adapter。
    /// </summary>
    public sealed class ResourcesAudioLoader : IAudioLoader
    {
        public UniTask<AudioClip> LoadAudioAsync(string path, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromCanceled<AudioClip>(cancellationToken);
            }

            return UniTask.FromResult(Resources.Load<AudioClip>(path));
        }

        public void Release()
        {
        }
    }
}
