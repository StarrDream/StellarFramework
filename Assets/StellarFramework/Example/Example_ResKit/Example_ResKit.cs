using UnityEngine;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using StellarFramework.Res.AB;

namespace StellarFramework.Examples
{
    /// <summary>
    /// ResKit 真实物理构建环境测试用例 (GUI 交互版)
    /// 职责：严格验证从物理文件 (StreamingAssets / 本地 Bundle) 加载资源的完整闭环。
    /// </summary>
    public class Example_ResKit : MonoBehaviour
    {
        [Header("测试路径配置 (请确保与打包时的路径完全一致)")] [Tooltip("Resources 相对路径")]
        public string resourcesPath = "ResKitTest/TestCube_Res";

        [Tooltip("AB 原始工程路径 (AssetMap.cs 中的 Key)")]
        public string assetBundlePath = "Assets/Art/ResKitTest/TestCapsule_AB.prefab";

        [Tooltip("Addressables 的 Address Name")]
        public string addressablePath = "TestSphere_AA";

        [Tooltip("StreamingAssets 下的相对路径")] public string customRawFilePath = "TestText.txt";

        // 加载器引用
        private IResLoader _resLoader;
        private IResLoader _abLoader;
        private IResLoader _aaLoader;
        private IResLoader _customLoader;

        // 实例引用
        private GameObject _resInstance;
        private GameObject _abInstance;
        private GameObject _aaInstance;

        // UI 状态文本
        private string _statusText = "等待测试...\n请确保您已经通过 Tools Hub 完成了 AB 打包！";
        private bool _isAbManagerInited = false;

        private void Start()
        {
            // 预先分配加载器 (0GC)
            _resLoader = ResKit.Allocate<ResourceLoader>();
            _abLoader = ResKit.Allocate<AssetBundleLoader>();
            _customLoader = ResKit.Allocate<RawTextLoader>();

#if UNITY_ADDRESSABLES
            _aaLoader = ResKit.Allocate<AddressableLoader>();
#endif
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 400, 600));
            GUILayout.BeginVertical("box");

            GUILayout.Label("ResKit 真实物理加载测试",
                new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold });
            GUILayout.Space(10);

            GUI.backgroundColor = _isAbManagerInited ? Color.green : new Color(1f, 0.6f, 0.2f);
            if (GUILayout.Button("1. 初始化 AB 管理器 (必点)", GUILayout.Height(40)))
            {
                InitABManagerAsync().Forget();
            }

            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // --- AB 测试 ---
            if (GUILayout.Button("加载 AssetBundle 资源\n(从 StreamingAssets 物理路径读取)", GUILayout.Height(45)))
            {
                if (!_isAbManagerInited)
                {
                    _statusText = "请先点击上方按钮初始化 AB Manager！";
                    return;
                }

                TestAssetBundleAsync().Forget();
            }

            // --- AA 测试 ---
#if UNITY_ADDRESSABLES
            if (GUILayout.Button("加载 Addressables 资源\n(请确保 Play Mode 设为 Use Existing Build)", GUILayout.Height(45)))
            {
                TestAddressablesAsync().Forget();
            }
#else
            GUILayout.Label("AA 测试不可用 (未定义 UNITY_ADDRESSABLES 宏)");
#endif

            // --- Resources 测试 ---
            if (GUILayout.Button("加载 Resources 资源", GUILayout.Height(35)))
            {
                TestResourcesAsync().Forget();
            }

            // --- 自定义加载器测试 ---
            if (GUILayout.Button("加载 RawText (自定义加载器读取 StreamingAssets)", GUILayout.Height(45)))
            {
                TestCustomLoaderAsync().Forget();
            }

            GUILayout.Space(20);
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("销毁实例并回收加载器 (触发 Unload)", GUILayout.Height(40)))
            {
                CleanupMemory();
            }

            GUI.backgroundColor = Color.white;

            GUILayout.Space(20);
            GUILayout.Label("当前状态与日志:");
            GUILayout.TextArea(_statusText, GUILayout.Height(150));

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private async UniTaskVoid InitABManagerAsync()
        {
            _statusText = "正在读取 StreamingAssets 下的 Manifest 并预热 Shader...";
            await AssetBundleManager.Instance.InitAsync();
            _isAbManagerInited = true;
            _statusText = "AB 管理器初始化完成！Manifest 与 Shaders 已加载。";
        }

        private async UniTaskVoid TestAssetBundleAsync()
        {
            _statusText = $"正在通过 AB 加载: {assetBundlePath}\n(底层将读取 StreamingAssets 物理文件)";
            var prefab = await _abLoader.LoadAsync<GameObject>(assetBundlePath);
            if (prefab != null)
            {
                if (_abInstance != null) Destroy(_abInstance);
                _abInstance = Instantiate(prefab, new Vector3(0, 0, 0), Quaternion.identity);
                _statusText = "AssetBundle 加载成功并实例化！\n如果你看到了材质，说明 Shader 预热成功。";
            }
            else
            {
                _statusText =
                    "AssetBundle 加载失败！\n请确认：\n1. 是否使用了 Tools Hub 进行了打包？\n2. StreamingAssets/AssetBundles 目录下是否有文件？";
            }
        }

#if UNITY_ADDRESSABLES
        private async UniTaskVoid TestAddressablesAsync()
        {
            _statusText = $"正在通过 AA 加载: {addressablePath}\n(底层将读取 Library/com.unity.addressables 下的物理 Bundle)";
            var prefab = await _aaLoader.LoadAsync<GameObject>(addressablePath);
            if (prefab != null)
            {
                if (_aaInstance != null) Destroy(_aaInstance);
                _aaInstance = Instantiate(prefab, new Vector3(2, 0, 0), Quaternion.identity);
                _statusText = "Addressables 加载成功并实例化！";
            }
            else
            {
                _statusText =
 "Addressables 加载失败！\n请确认：\n1. 是否在 Groups 窗口点击了 Build？\n2. Play Mode Script 是否切换为了 Use Existing Build？";
            }
        }
#endif

        private async UniTaskVoid TestResourcesAsync()
        {
            _statusText = $"正在通过 Resources 加载: {resourcesPath}";
            var prefab = await _resLoader.LoadAsync<GameObject>(resourcesPath);
            if (prefab != null)
            {
                if (_resInstance != null) Destroy(_resInstance);
                _resInstance = Instantiate(prefab, new Vector3(-2, 0, 0), Quaternion.identity);
                _statusText = "Resources 加载成功并实例化！";
            }
            else
            {
                _statusText = "Resources 加载失败，请检查路径。";
            }
        }

        private async UniTaskVoid TestCustomLoaderAsync()
        {
            _statusText = $"正在通过 CustomLoader 读取: {customRawFilePath}";
            var textAsset = await _customLoader.LoadAsync<TextAsset>(customRawFilePath);
            if (textAsset != null)
            {
                _statusText = $"RawText 读取成功！\n内容:\n{textAsset.text}";
            }
            else
            {
                _statusText = "RawText 读取失败，请检查 StreamingAssets 目录下是否存在该文件。";
            }
        }

        private void CleanupMemory()
        {
            _statusText = "正在执行严格内存清理...";

            // 必须先销毁实例，否则 AB 模式下 Unload(true) 会导致材质变粉
            if (_resInstance != null) Destroy(_resInstance);
            if (_abInstance != null) Destroy(_abInstance);
            if (_aaInstance != null) Destroy(_aaInstance);

            // 再回收加载器
            if (_resLoader != null) ResKit.Recycle(_resLoader);
            if (_abLoader != null) ResKit.Recycle(_abLoader);
            if (_customLoader != null) ResKit.Recycle(_customLoader);

#if UNITY_ADDRESSABLES
            if (_aaLoader != null) ResKit.Recycle(_aaLoader);
#endif

            // 重新分配，以便可以再次点击按钮测试
            Start();

            _statusText = "内存清理完毕！\n实例已销毁，加载器已回收，底层 Unload 已触发。";
        }
    }
}