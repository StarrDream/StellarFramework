using System.Collections.Generic;
using Minimap.Extras;
using Minimap.Interaction;
using Minimap.Markers;
using StellarFramework;
using UnityEngine;
using UnityEngine.UI;

namespace Minimap.Core
{
    public class MinimapManager : MonoBehaviour
    {
        public static MinimapManager Instance { get; private set; }

        [Header("核心配置")] public MapConfig mapConfig;

        [Header("模块引用")] public MinimapInteraction interaction;
        public QuestNavigator questNavigator;
        public Image mapBackgroundImage;
        public RectTransform markersContainer;

        [Header("默认预制体")] [Tooltip("如果注册时没有指定Prefab，就用这个默认的")]
        public MapMarker defaultMarkerPrefab;

        // --- 核心修改：多对象池 ---
        // Key: Prefab引用, Value: 该Prefab对应的实例池
        private Dictionary<MapMarker, Stack<MapMarker>> _pools = new Dictionary<MapMarker, Stack<MapMarker>>();

        // 活跃列表 (用于Update)
        private readonly List<MapMarker> _activeMarkers = new List<MapMarker>(64);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            RefreshMapVisuals();
            if (interaction == null) interaction = GetComponentInChildren<MinimapInteraction>();

            // 预加载默认池 (可选)
            if (defaultMarkerPrefab != null)
            {
                PrewarmPool(defaultMarkerPrefab, 5);
            }
        }

        /// <summary>
        /// 预热对象池
        /// </summary>
        public void PrewarmPool(MapMarker prefab, int count)
        {
            if (prefab == null) return;
            if (!_pools.ContainsKey(prefab))
            {
                _pools[prefab] = new Stack<MapMarker>();
            }

            for (int i = 0; i < count; i++)
            {
                CreateNewMarkerToPool(prefab);
            }
        }

        private void Update()
        {
            if (mapConfig == null) return;

            for (int i = _activeMarkers.Count - 1; i >= 0; i--)
            {
                var marker = _activeMarkers[i];
                if (!marker.IsValid)
                {
                    RemoveMarker(marker);
                    continue;
                }

                marker.OnGameUpdate();
            }
        }

        // --- 对象池逻辑 ---

        private MapMarker CreateNewMarkerToPool(MapMarker prefab)
        {
            var go = Instantiate(prefab.gameObject, markersContainer);
            var comp = go.GetComponent<MapMarker>();
            comp.Initialize();
            comp.originPrefab = prefab; // 关键：记住我是谁生的
            comp.Deactivate();

            // 放入对应的池子
            _pools[prefab].Push(comp);
            return comp;
        }

        private MapMarker Allocate(MapMarker prefab)
        {
            // 1. 确保池子存在
            if (!_pools.ContainsKey(prefab))
            {
                _pools[prefab] = new Stack<MapMarker>();
            }

            // 2. 如果池子空了，创建新的
            if (_pools[prefab].Count == 0)
            {
                return CreateNewMarkerToPool(prefab);
            }

            // 3. 出栈
            return _pools[prefab].Pop();
        }

        // --- 公开 API ---

        /// <summary>
        /// 注册一个小地图图标
        /// </summary>
        /// <param name="prefabOverride">指定使用哪个Prefab。如果为null，使用默认的。</param>
        public MapMarker Register(Transform target, MapMarker prefabOverride = null, Sprite icon = null, Color? color = null,
            bool syncRotation = false, bool clampToBorder = true, bool hideOutOfBounds = false, float rotationOffset = 0f)
        {
            if (target == null) return null;

            // 确定使用哪个 Prefab
            MapMarker prefabToUse = prefabOverride != null ? prefabOverride : defaultMarkerPrefab;

            if (prefabToUse == null)
            {
                LogKit.LogError("[Minimap] 注册失败：没有指定 Prefab 且没有默认 Prefab");
                return null;
            }

            // 从对应的池里取
            var marker = Allocate(prefabToUse);

            // 设置属性
            Color finalColor = color.HasValue ? color.Value : Color.white;
            marker.Setup(target, mapConfig, icon, finalColor, syncRotation, clampToBorder, hideOutOfBounds, rotationOffset);

            _activeMarkers.Add(marker);
            return marker;
        }

        public void RemoveMarker(MapMarker marker)
        {
            if (marker == null) return;

            if (_activeMarkers.Contains(marker))
            {
                _activeMarkers.Remove(marker);
                marker.Deactivate();

                // 关键：回收到它原本的池子里
                if (marker.originPrefab != null && _pools.ContainsKey(marker.originPrefab))
                {
                    _pools[marker.originPrefab].Push(marker);
                }
                else
                {
                    // 理论上不该发生，除非 Prefab 引用丢了，那就直接销毁
                    Destroy(marker.gameObject);
                }
            }
        }

        public void LoadMapConfig(MapConfig newConfig)
        {
            mapConfig = newConfig;
            RefreshMapVisuals();
        }

        public void RefreshMapVisuals()
        {
            if (mapConfig == null) return;
            if (mapBackgroundImage != null)
            {
                if (mapConfig.mapSprite != null) mapBackgroundImage.sprite = mapConfig.mapSprite;
                mapBackgroundImage.rectTransform.sizeDelta = new Vector2(mapConfig.mapUIWidth, mapConfig.mapUIHeight);
            }

            if (markersContainer != null)
            {
                markersContainer.sizeDelta = new Vector2(mapConfig.mapUIWidth, mapConfig.mapUIHeight);
                markersContainer.anchoredPosition = Vector2.zero;
            }
        }
    }
}