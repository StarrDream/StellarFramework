using Minimap.Core;
using Minimap.Markers; // 引用 MapMarker
using UnityEngine;

namespace Minimap.Extras
{
    public class MapEntity : MonoBehaviour
    {
        [Header("表现设置")] [Tooltip("指定特定的 Marker Prefab。如果不填，将使用 Manager 里的默认 Prefab")]
        public MapMarker customMarkerPrefab;

        public Sprite icon;
        public Color iconColor = Color.white;

        [Header("旋转设置")] public bool syncRotation = true;
        public float rotationOffset = 0f;

        [Header("主角专用")] public bool isMainPlayer = false;

        private MapMarker _myMarker;

        private void Start()
        {
            if (MinimapManager.Instance != null)
            {
                // 注册时传入 customMarkerPrefab
                _myMarker = MinimapManager.Instance.Register(
                    target: this.transform,
                    prefabOverride: customMarkerPrefab, // 关键改动
                    icon: icon,
                    color: iconColor,
                    syncRotation: syncRotation,
                    rotationOffset: rotationOffset
                );
            }

            if (isMainPlayer && MinimapManager.Instance?.interaction != null)
            {
                MinimapManager.Instance.interaction.SetPlayerTarget(this.transform);
            }
        }

        private void OnDestroy()
        {
            if (_myMarker != null && MinimapManager.Instance != null)
            {
                MinimapManager.Instance.RemoveMarker(_myMarker);
            }
        }
    }
}