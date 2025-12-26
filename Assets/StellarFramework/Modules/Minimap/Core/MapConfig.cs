using UnityEngine;

namespace Minimap.Core
{
    [CreateAssetMenu(fileName = "MapConfig", menuName = "StellarFramework/Minimap/Map Config")]
    public class MapConfig : ScriptableObject
    {
        public enum MapShape
        {
            Rectangle,
            Circle
        }

        public MapShape shape = MapShape.Rectangle;

        [Header("3D 世界边界")] public float minX = -500f;
        public float maxX = 500f;
        public float minZ = -500f;
        public float maxZ = 500f;

        [Header("2D UI 设置")] public Sprite mapSprite;

        [Tooltip("UI上的地图显示区域大小(像素)，请确保和图片实际分辨率比例一致")]
        public float mapUIWidth = 1024f;

        public float mapUIHeight = 1024f;

        [Header("修正")] public bool flipHorizontal;
        public bool flipVertical;

        // 辅助属性
        public float RangeX => maxX - minX;
        public float RangeZ => maxZ - minZ;
    }
}