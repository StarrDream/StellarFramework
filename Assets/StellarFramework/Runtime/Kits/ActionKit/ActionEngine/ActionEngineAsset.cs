using StellarFramework.ActionEngine;
using UnityEngine;

namespace StellarFramework.ActionEngine
{
    [CreateAssetMenu(fileName = "NewActionAsset", menuName = "Stellar/Action Engine Asset")]
    public class ActionEngineAsset : ScriptableObject
    {
        public GameObject TargetPrefab;

        [SerializeReference] public ActionNodeData RootNode = new ActionNodeData { NodeName = "Root" };
    }
}