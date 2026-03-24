using System.IO;
using System.Text;
using UnityEngine;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;

namespace StellarFramework.Examples
{
    /// <summary>
    /// 自定义扩展加载器示例：直接从 StreamingAssets 目录读取原生文本文件。
    /// 职责：演示如何通过继承 ResLoader 扩展框架，接入框架的引用计数与异步去重管线。
    /// </summary>
    public class RawTextLoader : ResLoader
    {
        // 架构重构：不再需要 Hack 枚举，直接定义专属的命名空间
        public override string LoaderName => "RawText";

        protected override ResData LoadRealSync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogKit.LogError("[RawTextLoader] 同步加载失败: 路径为空");
                return null;
            }

            string fullPath = Path.Combine(Application.streamingAssetsPath, path);
            if (!File.Exists(fullPath))
            {
                LogKit.LogError($"[RawTextLoader] 同步加载失败: 文件不存在，路径={fullPath}");
                return null;
            }

            string content = File.ReadAllText(fullPath, Encoding.UTF8);

            TextAsset textAsset = new TextAsset(content);
            textAsset.name = path;

            return new ResData
            {
                Asset = textAsset
            };
        }

        protected override async UniTask<ResData> LoadRealAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogKit.LogError("[RawTextLoader] 异步加载失败: 路径为空");
                return null;
            }

            string fullPath = Path.Combine(Application.streamingAssetsPath, path);
            if (!File.Exists(fullPath))
            {
                LogKit.LogError($"[RawTextLoader] 异步加载失败: 文件不存在，路径={fullPath}");
                return null;
            }

            string content;
            using (StreamReader reader = new StreamReader(fullPath, Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync();
            }

            TextAsset textAsset = new TextAsset(content);
            textAsset.name = path;

            return new ResData
            {
                Asset = textAsset
            };
        }

        // 架构重构：实现自定义的卸载逻辑，ResMgr 会无脑回调此方法
        protected override void UnloadReal(ResData data)
        {
            if (data.Asset != null)
            {
                LogKit.Log($"[RawTextLoader] 触发真实卸载: {data.Path}");
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(data.Asset);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(data.Asset);
                }
            }
        }
    }
}