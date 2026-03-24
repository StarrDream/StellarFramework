using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace StellarFramework.Audio
{
    /// <summary>
    /// 音频资源加载策略接口
    /// 允许业务层接入任意资源管理方案 (如 YooAsset, Addressables, ResKit 等)
    /// </summary>
    public interface IAudioLoader
    {
        /// <summary>
        /// 异步加载音频片段
        /// </summary>
        /// <param name="path">资源路径或标识</param>
        /// <param name="cancellationToken">取消令牌，用于在管理器销毁时打断加载</param>
        /// <returns>AudioClip 实例，加载失败返回 null</returns>
        UniTask<AudioClip> LoadAudioAsync(string path, CancellationToken cancellationToken);

        /// <summary>
        /// 释放资源加载器及其持有的缓存
        /// </summary>
        void Release();
    }
}