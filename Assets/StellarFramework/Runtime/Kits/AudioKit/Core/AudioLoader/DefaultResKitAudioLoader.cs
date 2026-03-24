using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using StellarFramework.Res;

namespace StellarFramework.Audio
{
    /// <summary>
    /// 默认的音频加载策略 (泛型重构版)
    /// 基于框架内置的 ResKit 实现，通过泛型 TLoader 决定底层加载方式
    /// </summary>
    public class DefaultResKitAudioLoader<TLoader> : IAudioLoader where TLoader : ResLoader, new()
    {
        private IResLoader _resLoader;

        public DefaultResKitAudioLoader()
        {
            // 优雅的泛型分配，彻底告别 switch-case 和枚举
            _resLoader = ResKit.Allocate<TLoader>();
        }

        public async UniTask<AudioClip> LoadAudioAsync(string path, CancellationToken cancellationToken)
        {
            if (_resLoader == null) return null;
            return await _resLoader.LoadAsync<AudioClip>(path).AttachExternalCancellation(cancellationToken);
        }

        public void Release()
        {
            if (_resLoader != null)
            {
                ResKit.Recycle(_resLoader);
                _resLoader = null;
            }
        }
    }
}