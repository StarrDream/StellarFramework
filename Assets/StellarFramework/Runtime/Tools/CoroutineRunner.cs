using System.Collections;
using UnityEngine;

namespace StellarFramework
{
    /// <summary>
    /// 为非 MonoBehaviour 代码提供协程宿主的运行时工具。
    /// 不依赖 SingletonKit、LogKit 或任何第三方包，可单独导入使用。
    /// </summary>
    public sealed class CoroutineRunner : MonoBehaviour
    {
        private const string RunnerObjectName = "[StellarFramework.CoroutineRunner]";

        private static CoroutineRunner _instance;

        /// <summary>
        /// 获取全局协程宿主；首次访问时自动创建。
        /// </summary>
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    CreateInstance();
                }

                return _instance;
            }
        }

        /// <summary>
        /// 从非组件代码启动协程。
        /// </summary>
        public static Coroutine Run(IEnumerator routine)
        {
            return routine == null ? null : Instance.StartCoroutine(routine);
        }

        /// <summary>
        /// 停止由 <see cref="Run"/> 返回的协程。
        /// </summary>
        public static void Stop(Coroutine routine)
        {
            if (_instance != null && routine != null)
            {
                _instance.StopCoroutine(routine);
            }
        }

        private static void CreateInstance()
        {
            var runnerObject = new GameObject(RunnerObjectName);
            DontDestroyOnLoad(runnerObject);
            _instance = runnerObject.AddComponent<CoroutineRunner>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
