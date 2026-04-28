using System.Threading;
using Cysharp.Threading.Tasks;
using StellarFramework.Res;
using UnityEngine;

namespace StellarFramework.UI
{
    /// <summary>
    /// UI 加载策略接口
    /// 我只定义 UIKit 真正关心的加载能力，不让 UIKit 知道底层到底使用 Resources、AB、AA 还是业务自定义加载器
    /// </summary>
    public interface IUILoadStrategy
    {
        bool SupportSyncLoad { get; }

        GameObject LoadUIRoot();
        UniTask<GameObject> LoadUIRootAsync(CancellationToken cancellationToken = default);

        GameObject LoadPanelPrefab(string panelName);
        UniTask<GameObject> LoadPanelPrefabAsync(string panelName, CancellationToken cancellationToken = default);

        void UnloadPanelPrefab(string panelName);
        void ReleaseAll();
    }
}
