using System.Collections;
using UnityEngine;
using StellarFramework;

namespace StellarFramework.Examples
{
    /// <summary>
    /// ConfigKit 综合使用示例
    /// 职责: 演示配置的异步加载、普通配置的读写存档、网络配置的参数化路由拼接
    /// 使用说明: 
    /// 1. 在 ConfigKit Dashboard 中创建一个 Normal 配置 (如 TestGameConfig.json) 和一个 Net 配置 (如 TestApiConfig.json)。
    /// 2. 将此脚本挂载到场景中的任意 GameObject 上。
    /// 3. 运行游戏并查看 Console 日志，或点击 Game 视图中的 GUI 按钮进行交互测试。
    /// </summary>
    public class Example_ConfigKit : MonoBehaviour
    {
        private NormalConfig _gameConfig;
        private NetConfig _apiConfig;

        private bool _isInitialized = false;

        private void Start()
        {
            // 启动初始化流转，确保配置加载完毕后再执行业务逻辑
            StartCoroutine(InitConfigsRoutine());
        }

        private IEnumerator InitConfigsRoutine()
        {
            Debug.Log("[Example_ConfigKit] 开始加载配置...");

            bool isGameConfigLoaded = false;
            bool isApiConfigLoaded = false;

            // 1. 加载普通配置
            // 修正路径：补全 Configs/ 前缀，与编辑器工具的保存目录对齐
            yield return ConfigKit.LoadNormalConfig("TestGameConfig", "Configs/Normal/TestGameConfig.json", config =>
            {
                if (config != null)
                {
                    _gameConfig = config;
                    isGameConfigLoaded = true;
                    Debug.Log($"[Example_ConfigKit] TestGameConfig 加载成功. 来源: {(config.IsUserSave ? "本地存档(Overlay)" : "包内默认配置")}");
                }
            });

            // 2. 加载网络配置
            // 修正路径：补全 Configs/ 前缀
            yield return ConfigKit.LoadNetConfig("TestApiConfig", "Configs/Net/TestApiConfig.json", config =>
            {
                if (config != null)
                {
                    _apiConfig = config;
                    isApiConfigLoaded = true;
                    Debug.Log("[Example_ConfigKit] TestApiConfig 加载成功.");
                }
            });

            // 前置拦截：核心配置缺失时立即阻断，防止后续业务产生空指针或脏数据
            if (!isGameConfigLoaded || !isApiConfigLoaded)
            {
                Debug.LogError("[Example_ConfigKit] 初始化失败: 部分配置文件未找到。请确保在 Dashboard 中创建了 TestGameConfig.json 和 TestApiConfig.json");
                // 修正逻辑：必须使用 yield break 彻底终止协程，防止继续往下执行
                yield break;
            }

            _isInitialized = true;
            Debug.Log("[Example_ConfigKit] 所有配置加载完毕，开始执行业务演示。");

            // 执行读取演示
            DemoReadNormalConfig();
            DemoReadNetConfig();
        }

        private void DemoReadNormalConfig()
        {
            if (_gameConfig == null) return;

            // 读取基础类型，提供默认值以防 JSON 节点缺失或类型不匹配
            string version = _gameConfig.GetString("Version", "1.0.0");
            int maxPlayers = _gameConfig.GetInt("MaxPlayers", 100);
            float bgmVolume = _gameConfig.GetFloat("BGMVolume", 1.0f);
            bool isDebugOpen = _gameConfig.GetBool("IsDebugOpen", false);

            Debug.Log($"[Example_ConfigKit] 读取普通配置 -> Version: {version}, MaxPlayers: {maxPlayers}, BGMVolume: {bgmVolume}, IsDebugOpen: {isDebugOpen}");
        }

        private void DemoReadNetConfig()
        {
            if (_apiConfig == null) return;

            // 演示 1: 无参数路由拼接
            // 假设 TestApiConfig.json 的 Endpoints 中配置了 "Auth.Login"
            string loginUrl = _apiConfig.GetUrl("Auth.Login");
            Debug.Log($"[Example_ConfigKit] 网络路由 (无参数) -> Auth.Login: {loginUrl}");

            // 演示 2: 带参数路由拼接 (零 GC)
            // 假设 TestApiConfig.json 的 Endpoints 中配置了 "Item.GetDetail" : { "Path": "/api/item/{itemId}" }
            // 使用 ValueTuple 隐式转换为 UrlParam 结构体，底层通过 Span<T> 切片替换，避免 string.Format 的装箱开销
            string itemUrl = _apiConfig.GetUrl("Item.GetDetail", ("itemId", 999));
            Debug.Log($"[Example_ConfigKit] 网络路由 (带参数) -> Item.GetDetail: {itemUrl}");

            // 演示 3: 多参数路由拼接
            // 假设 Path 为 "/api/room/{roomId}/player/{uid}"
            string roomUrl = _apiConfig.GetUrl("Room.Join",
                ("roomId", "R-1024"),
                ("uid", 10086)
            );
            Debug.Log($"[Example_ConfigKit] 网络路由 (多参数) -> Room.Join: {roomUrl}");
        }

        private void OnGUI()
        {
            if (!_isInitialized)
            {
                GUILayout.Label("配置加载中...");
                return;
            }

            GUILayout.BeginArea(new Rect(20, 20, 300, 400));
            GUILayout.Label("ConfigKit 交互测试", GUI.skin.box);

            GUILayout.Space(10);
            GUILayout.Label($"当前音量: {_gameConfig.GetFloat("BGMVolume", 1.0f)}");

            if (GUILayout.Button("将音量设置为 0.5", GUILayout.Height(30)))
            {
                // 修改内存中的值
                _gameConfig.Set("BGMVolume", 0.5f);
                Debug.Log("[Example_ConfigKit] 内存音量已修改为 0.5");
            }

            if (GUILayout.Button("将音量设置为 1.0", GUILayout.Height(30)))
            {
                _gameConfig.Set("BGMVolume", 1.0f);
                Debug.Log("[Example_ConfigKit] 内存音量已修改为 1.0");
            }

            GUILayout.Space(10);

            if (GUILayout.Button("保存配置到本地存档 (Save)", GUILayout.Height(40)))
            {
                // 异步写入 PersistentDataPath，下次启动时将触发 Overlay 机制覆盖包内配置
                _gameConfig.Save();
                Debug.Log("[Example_ConfigKit] 配置已触发异步保存。请重新运行游戏，观察音量是否保持为修改后的值。");
            }

            GUILayout.EndArea();
        }
    }
}
