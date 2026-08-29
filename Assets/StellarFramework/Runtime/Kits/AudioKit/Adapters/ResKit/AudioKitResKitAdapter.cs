using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;
using UnityEngine.Audio;

namespace StellarFramework.Audio
{
    /// <summary>
    /// AudioKit 对 ResKit 的可选适配入口。
    /// 仅在安装 StellarFramework.AudioKit.ResKit 后可用。
    /// </summary>
    public static class AudioKitResKit
    {
        public static void Init<TLoader>(AudioMixer mixer) where TLoader : ResLoader, new()
        {
            AudioKit.Init(mixer, new DefaultResKitAudioLoader<TLoader>());
        }
    }

    /// <summary>
    /// 基于 ResKit 的音频加载策略，可独立作为 IAudioLoader 使用。
    /// </summary>
    public sealed class DefaultResKitAudioLoader<TLoader> : IAudioLoader where TLoader : ResLoader, new()
    {
        private IResLoader _resLoader;

        public DefaultResKitAudioLoader()
        {
            _resLoader = ResKit.Allocate<TLoader>();
        }

        public async UniTask<AudioClip> LoadAudioAsync(string path, CancellationToken cancellationToken)
        {
            if (_resLoader == null)
            {
                return null;
            }

            return await _resLoader.LoadAsync<AudioClip>(path).AttachExternalCancellation(cancellationToken);
        }

        public void Release()
        {
            if (_resLoader == null)
            {
                return;
            }

            ResKit.Recycle(_resLoader);
            _resLoader = null;
        }
    }
}
