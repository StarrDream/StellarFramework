using UnityEngine;

/// <summary>
/// 你的游戏入口
/// 我负责拉起全局业务架构，不在这里承载业务逻辑。
/// </summary>
public class GameEntry : MonoBehaviour
{
    private bool _isStarted;

    private void Start()
    {
        if (_isStarted)
        {
            Debug.LogWarning($"[GameEntry] 重复启动已忽略, TriggerObject={gameObject.name}");
            return;
        }

        _isStarted = true;

        if (GameApp.Interface == null)
        {
            Debug.LogError($"[GameEntry] 启动失败: GameApp.Interface 为空, TriggerObject={gameObject.name}");
            return;
        }

        GameApp.Interface.Init();
        Debug.Log($"[GameEntry] 业务架构初始化完成, TriggerObject={gameObject.name}");
    }

    private void OnDestroy()
    {
        if (!_isStarted)
        {
            return;
        }

        if (GameApp.Interface != null && GameApp.Interface.State == StellarFramework.ArchitectureState.Initialized)
        {
            GameApp.Interface.Dispose();
        }

        _isStarted = false;
    }
}