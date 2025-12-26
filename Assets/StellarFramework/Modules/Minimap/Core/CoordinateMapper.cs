using UnityEngine;

namespace Minimap.Core
{
    public static class CoordinateMapper
    {
        /// <summary>
        /// 世界坐标 -> UI 局部坐标
        /// </summary>
        public static Vector2 WorldToMapPosition(Vector3 worldPos, MapConfig config)
        {
            // 1. 归一化 (0~1)
            float normX = (worldPos.x - config.minX) / config.RangeX;
            float normZ = (worldPos.z - config.minZ) / config.RangeZ;

            // 2. 翻转修正
            if (config.flipHorizontal) normX = 1f - normX;
            if (config.flipVertical) normZ = 1f - normZ;

            // 3. 映射到 UI 尺寸 (中心点为 0,0)
            float mapX = (normX - 0.5f) * config.mapUIWidth;
            float mapY = (normZ - 0.5f) * config.mapUIHeight;

            return new Vector2(mapX, mapY);
        }

        /// <summary>
        /// 检查是否在地图范围内
        /// </summary>
        public static bool IsInBounds(Vector3 worldPos, MapConfig config)
        {
            return worldPos.x >= config.minX && worldPos.x <= config.maxX &&
                   worldPos.z >= config.minZ && worldPos.z <= config.maxZ;
        }

        /// <summary>
        /// 限制坐标在边界内 (支持圆形和矩形)
        /// </summary>
        public static Vector2 ClampToBorder(Vector2 mapPos, MapConfig config, float padding = 15f)
        {
            if (config.shape == MapConfig.MapShape.Circle)
            {
                // 圆形吸附逻辑：限制向量长度
                float maxRadius = (config.mapUIWidth * 0.5f) - padding;
                return Vector2.ClampMagnitude(mapPos, maxRadius);
            }
            else
            {
                // 矩形吸附逻辑：限制XY轴
                float halfW = (config.mapUIWidth * 0.5f) - padding;
                float halfH = (config.mapUIHeight * 0.5f) - padding;
                float x = Mathf.Clamp(mapPos.x, -halfW, halfW);
                float y = Mathf.Clamp(mapPos.y, -halfH, halfH);
                return new Vector2(x, y);
            }
        }
    }
}