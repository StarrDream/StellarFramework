using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework;
using UnityEngine;
using StellarFramework.ActionEngine;

namespace StellarFramework
{
    /// <summary>
    /// 动作引擎播放器组件
    /// 负责在运行时桥接 GameObject 与 ActionEngineAsset，提供极简的播放控制接口。
    /// 遵循组件化设计，业务逻辑只需调用 PlayForward/PlayReverse，无需关心底层的 Token 与 Snapshot 管理。
    /// </summary>
    [DisallowMultipleComponent]
    public class ActionPlayer : MonoBehaviour
    {
        [Header("动作资产")]
        [SerializeField] private ActionEngineAsset _actionAsset;
        
        [Header("播放设置")]
        [SerializeField] private bool _playOnAwake = false;
        [SerializeField] private bool _resetOnDisable = true;

        private CancellationTokenSource _playbackCts;

        private void Awake()
        {
            if (_actionAsset == null) return;
            
            // 运行时初始化快照，固化当前状态为绝对基准
            ActionEngineRunner.InitSnapshot(gameObject, _actionAsset);
        }

        private void OnEnable()
        {
            if (_playOnAwake && _actionAsset != null)
            {
                PlayForward().Forget();
            }
        }

        private void OnDisable()
        {
            Stop();
            
            if (_resetOnDisable && _actionAsset != null)
            {
                ActionEngineRunner.RestoreSnapshot(gameObject);
            }
        }

        private void OnDestroy()
        {
            Stop();
            // 必须清理快照字典，防止 GameObject 销毁后产生内存泄漏
            ActionEngineRunner.ClearSnapshot(gameObject);
        }

        /// <summary>
        /// 正向播放动画
        /// </summary>
        public async UniTask PlayForward()
        {
            if (_actionAsset == null)
            {
                Debug.LogError($"[ActionPlayer] PlayForward 失败: 未绑定动作资产, Target={gameObject.name}, Asset=null");
                return;
            }

            Stop();
            _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            await ActionEngineRunner.Play(gameObject, _actionAsset, false, _playbackCts.Token);
        }

        /// <summary>
        /// 倒向播放动画
        /// </summary>
        public async UniTask PlayReverse()
        {
            if (_actionAsset == null)
            {
                Debug.LogError($"[ActionPlayer] PlayReverse 失败: 未绑定动作资产, Target={gameObject.name}, Asset=null");
                return;
            }

            Stop();
            _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            await ActionEngineRunner.Play(gameObject, _actionAsset, true, _playbackCts.Token);
        }

        /// <summary>
        /// 停止当前播放
        /// </summary>
        public void Stop()
        {
            if (_playbackCts != null)
            {
                _playbackCts.Cancel();
                _playbackCts.Dispose();
                _playbackCts = null;
            }
        }

        /// <summary>
        /// 瞬间重置回初始状态
        /// </summary>
        public void ResetToStart()
        {
            Stop();
            ActionEngineRunner.RestoreSnapshot(gameObject);
        }
        
        /// <summary>
        /// 动态替换资产并刷新基准 (适用于对象池复用场景)
        /// </summary>
        public void SetAssetAndRefresh(ActionEngineAsset newAsset)
        {
            if (newAsset == null)
            {
                Debug.LogError($"[ActionPlayer] SetAssetAndRefresh 失败: 传入资产为空, Target={gameObject.name}");
                return;
            }
            
            Stop();
            _actionAsset = newAsset;
            ActionEngineRunner.InitSnapshot(gameObject, _actionAsset, forceOverwrite: true);
        }
    }
}
/*
public class UIPopupView : MonoBehaviour
{
    [SerializeField] private ActionPlayer _transitionPlayer;
    
    // 业务层调用：打开弹窗
    public async UniTask ShowPopupAsync()
    {
        gameObject.SetActive(true);
        // 等待正向入场动画播完
        await _transitionPlayer.PlayForward(); 
        
        // 动画播完后，可以触发一些业务事件
        Debug.Log("弹窗入场完毕，允许玩家点击");
    }
    
    // 业务层调用：关闭弹窗
    public async UniTask HidePopupAsync()
    {
        // 等待倒向退场动画播完
        await _transitionPlayer.PlayReverse(); 
        
        gameObject.SetActive(false);
    }
}
*/
