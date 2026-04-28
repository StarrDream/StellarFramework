using UnityEngine;
using UnityEngine.UI;
using StellarFramework;

namespace StellarFramework.Examples
{
    /// <summary>
    /// ActionKit 综合使用示例
    /// 演示链式动画、并行任务、生命周期绑定与手动取消
    /// </summary>
    public class Example_ActionKit : MonoBehaviour
    {
        [Header("测试引用")] public Transform cubeTransform;
        public CanvasGroup uiGroup;

        // 保存当前动画链的引用，用于手动打断
        private UniActionChain _currentChain;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                PlaySequence();
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                // 防御性编程：判空后取消，防止空引用
                _currentChain?.Cancel();
                LogKit.Log("[Example_ActionKit] 已手动取消当前动画");
            }
        }

        private void PlaySequence()
        {
            if (cubeTransform == null || uiGroup == null)
            {
                LogKit.LogError(
                    $"[Example_ActionKit] 播放失败: 缺失关键引用, Cube={cubeTransform}, UIGroup={uiGroup}");
                return;
            }

            // 规范：每次播放新动画前，先打断旧动画，防止逻辑重叠与表现异常
            _currentChain?.Cancel();

            LogKit.Log("[Example_ActionKit] 开始播放序列动画...");

            // 规范：传入 gameObject 绑定生命周期，物体销毁时自动取消 0GC
            _currentChain = ActionKit.Sequence(gameObject)
                // 1. 移动
                .LocalMoveTo(cubeTransform, new Vector3(5, 0, 0), 1f, Ease.OutQuad)
                // 2. 延时
                .Delay(0.5f)
                // 3. 并行执行多个动画
                .Parallel(
                    t => ActionKit.Sequence(gameObject).ScaleTo(cubeTransform, Vector3.one * 2, 1f, Ease.OutBack).Await(),
                    t => ActionKit.Sequence(gameObject).FadeTo(uiGroup, 0.5f, 1f).Await()
                )
                // 4. 回调
                .Callback(() => LogKit.Log("[Example_ActionKit] 动画序列执行完毕！"))
                .OnComplete(() => _currentChain = null)
                .OnCancel(() => _currentChain = null)
                .OnError(ex =>
                {
                    _currentChain = null;
                    LogKit.LogError($"[Example_ActionKit] 动画执行异常: {ex.Message}");
                })
                // 5. 必须调用 Start 触发
                .Start();
        }
    }
}
