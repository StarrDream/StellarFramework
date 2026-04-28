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

            ExamplePlayableSceneBuilder.BuildPlayableScenes();
            DestroyImmediate(this);
        }
    }
}
#endif
