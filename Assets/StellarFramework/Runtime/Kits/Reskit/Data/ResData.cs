// ==================================================================================
// ResData - Commercial Convergence V2
// ----------------------------------------------------------------------------------
// 职责：资源缓存数据实体。
// 改造说明：
// 1. 引入 Owners 追踪集合（仅在开发期生效），精确记录当前是哪些 Loader 持有了该资源。
// 2. 配合 ResMgr 实现资源泄漏的精准定位。
// ==================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework.Res
{
    public class ResData
    {
        public string Path;
        public Object Asset;
        public int RefCount;
        public string LoaderName;
        public object Data;
        public Action<ResData> UnloadAction;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 审计追踪：记录所有持有该资源的 LoaderId
        // 仅在开发环境开启，Release 包 0 开销
        private HashSet<string> _owners;

        public HashSet<string> Owners
        {
            get
            {
                if (_owners == null) _owners = new HashSet<string>();
                return _owners;
            }
        }

        public void AddOwner(string ownerId)
        {
            Owners.Add(ownerId);
        }

        public void RemoveOwner(string ownerId)
        {
            Owners.Remove(ownerId);
        }
#endif
    }
}