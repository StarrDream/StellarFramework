// ==================================================================================
// ResData
// ----------------------------------------------------------------------------------
// ResMgr 使用的共享资源记录。第三方后端句柄通过 Data 保持透明传递。
// ==================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    /// <summary>
    /// ResMgr 缓存的一条共享资源记录。
    /// </summary>
    /// <remarks>
    /// Core 只认识 Unity Object 与引用计数。第三方后端句柄放在 <see cref="Data"/>，
    /// 最终通过 <see cref="UnloadAction"/> 释放，例如 Addressables/YooAsset 的 Handle。
    /// </remarks>
    public class ResData
    {
        /// <summary>稳定资源地址。</summary>
        public string Path;
        /// <summary>实际 Unity 资源对象。</summary>
        public Object Asset;
        /// <summary>当前共享引用数，由 ResMgr 维护。</summary>
        public int RefCount;
        /// <summary>参与共享缓存命名空间的 Loader 身份。</summary>
        public string LoaderName;
        /// <summary>后端私有句柄或附加数据，Core 不解释具体类型。</summary>
        public object Data;
        /// <summary>共享引用归零时执行的后端真实释放动作。</summary>
        public Action<ResData> UnloadAction;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 开发期保留 Owner 集合，用于定位“谁没有释放资源”。
        // Release 不维护该集合，避免给运行时主链路增加常驻审计开销。
        private HashSet<string> _owners;

        /// <summary>当前持有者集合，仅 Editor/Development Build 存在。</summary>
        public HashSet<string> Owners
        {
            get
            {
                if (_owners == null) _owners = new HashSet<string>();
                return _owners;
            }
        }

        /// <summary>记录一个开发期 Owner。</summary>
        public void AddOwner(string ownerId)
        {
            Owners.Add(ownerId);
        }

        /// <summary>移除一个开发期 Owner。</summary>
        public void RemoveOwner(string ownerId)
        {
            Owners.Remove(ownerId);
        }
#endif
    }

    /// <summary>
    /// AssetBundle 真实卸载时是否同时销毁已经从 Bundle 加载出的 Unity 对象。
    /// </summary>
    public enum AssetBundleUnloadMode
    {
        PreserveLoadedAssets = 0,
        DestroyLoadedAssets = 1
    }

}
