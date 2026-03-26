using UnityEngine;
using System;

namespace StellarFramework.ActionEngine
{
    [CreateAssetMenu(fileName = "NewActionAsset", menuName = "Stellar/Action Engine Asset")]
    public class ActionEngineAsset : ScriptableObject
    {
        [Header("绑定配置")] [Tooltip("该动画资产关联的原始预制体")]
        public GameObject TargetPrefab;

        [Header("逻辑编排")] public ActionGroupData RootGroup = new ActionGroupData { GroupName = "Root" };
    }
}