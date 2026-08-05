#if UNITY_EDITOR
using StellarFramework.Editor;
using UnityEditor;
using UnityEngine;

namespace StellarFramework.Examples
{
    /// <summary>
    /// 仅用于仓库内样例场景的编辑器构建触发器。
    /// </summary>
    [ExecuteAlways]
    public class ExampleEditorBuildTrigger : MonoBehaviour
    {
        private bool _queued;

        private void OnEnable()
        {
            if (Application.isPlaying || _queued)
            {
                return;
            }

            _queued = true;
            EditorApplication.delayCall += RunBuild;
        }

        private void RunBuild()
        {
            if (this == null)
            {
                return;
            }

            // 仅当样例核心资源（UIRoot）缺失时才触发构建，
            // 避免仓库内样例已生成时，每次加载场景都做全量 AssetDatabase 刷新。
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/StellarFramework/Resources/UIPanel/UIRoot.prefab") == null)
            {
                ExamplePlayableSceneBuilder.BuildPlayableScenes();
            }

            DestroyImmediate(this);
        }
    }
}
#endif
