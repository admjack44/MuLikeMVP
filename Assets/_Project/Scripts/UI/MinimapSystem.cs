using System;
using System.Collections.Generic;
using MuLike.UI.MobileHUD;
using MuLike.World;
using UnityEngine;

namespace MuLike.UI
{
    /// <summary>
    /// Circular minimap + world map runtime.
    ///
    /// Minimap:
    /// - Top-right circular presentation (drives existing MinimapView markers).
    /// - Party players, important NPCs, elite monsters.
    /// - Quest target direction indicator.
    /// - 3 zoom levels.
    ///
    /// World map:
    /// - Full map panel toggle.
    /// - Real-time event markers with type filters.
    /// - Fast-travel request relay.
    /// </summary>
    public sealed class MinimapSystem : MonoBehaviour
    {
        public enum MarkerType
        {
            Party,
            ImportantNpc,
            EliteMonster,
            Quest,
            Event,
            Portal
        }

        public enum MinimapZoomLevel
        {
            Near = 0,
            Medium = 1,
            Far = 2
        }

        [Serializable]
        public struct MinimapMarker
        {
            public string id;
            public MarkerType type;
            public Transform target;
            public Vector3 worldPositionFallback;
            public string label;
            public bool visible;
            public bool useCustomColor;
            public Color markerColor;
        }

        [Serializable]
        public struct WorldEventMarker
        {
            public string eventId;
            public string label;
            public MarkerType type;
            public Vector3 worldPosition;
            public float expiresAt;
        }

        [Header("Dependencies")]
        [SerializeField] private MinimapView _minimapView;
        [SerializeField] private MapLoader _mapLoader;
        [SerializeField] private Transform _player;

        [Header("World map panel")]
        [SerializeField] private CanvasGroup _worldMapCanvas;

        [Header("Zoom")]
        [SerializeField] private float _nearRangeMeters = 28f;
        [SerializeField] private float _mediumRangeMeters = 48f;
        [SerializeField] private float _farRangeMeters = 72f;

        private readonly Dictionary<string, MinimapMarker> _markers = new();
        private readonly Dictionary<string, WorldEventMarker> _events = new();
        private readonly HashSet<MarkerType> _worldMapFilters = new();

        private MinimapZoomLevel _zoom = MinimapZoomLevel.Medium;
        private bool _worldMapVisible;

        public MinimapZoomLevel ZoomLevel => _zoom;
        public bool WorldMapVisible => _worldMapVisible;

        public event Action<Vector3> OnFastTravelRequested;
        public event Action<bool> OnWorldMapVisibilityChanged;

        private void Awake()
        {
            if (_minimapView == null)
                _minimapView = FindAnyObjectByType<MinimapView>();
            if (_mapLoader == null)
                _mapLoader = FindAnyObjectByType<MapLoader>();
            if (_player == null)
                _player = FindAnyObjectByType<MuLike.Gameplay.Controllers.CharacterMotor>()?.transform;

            if (_worldMapCanvas != null)
            {
                _worldMapCanvas.alpha = 0f;
                _worldMapCanvas.blocksRaycasts = false;
            }

            if (_mapLoader != null)
                _mapLoader.OnMapLoaded += HandleMapLoaded;

            // By default show all marker types in world map.
            foreach (MarkerType type in Enum.GetValues(typeof(MarkerType)))
                _worldMapFilters.Add(type);
        }

        private void OnDestroy()
        {
            if (_mapLoader != null)
                _mapLoader.OnMapLoaded -= HandleMapLoaded;
        }

        private void Update()
        {
            if (_player == null || _minimapView == null)
                return;

            UpdateMinimapMarkers();
            CleanupExpiredEvents();
        }

        public void SetZoom(MinimapZoomLevel zoom)
        {
            _zoom = zoom;
        }

        public void CycleZoom()
        {
            int next = ((int)_zoom + 1) % 3;
            _zoom = (MinimapZoomLevel)next;
        }

        public void ToggleWorldMap()
        {
            SetWorldMapVisible(!_worldMapVisible);
        }

        public void SetWorldMapVisible(bool visible)
        {
            _worldMapVisible = visible;
            if (_worldMapCanvas != null)
            {
                _worldMapCanvas.alpha = visible ? 1f : 0f;
                _worldMapCanvas.blocksRaycasts = visible;
            }

            OnWorldMapVisibilityChanged?.Invoke(_worldMapVisible);
        }

        public void SetFilter(MarkerType type, bool enabled)
        {
            if (enabled) _worldMapFilters.Add(type);
            else _worldMapFilters.Remove(type);
        }

        public void UpsertMarker(MinimapMarker marker)
        {
            if (string.IsNullOrWhiteSpace(marker.id))
                return;

            _markers[marker.id] = marker;
        }

        public void RemoveMarker(string markerId)
        {
            _markers.Remove(markerId);
            _minimapView?.SetMarker(markerId, Vector3.zero, false, string.Empty);
        }

        public void UpsertWorldEvent(string eventId, string label, MarkerType type, Vector3 worldPos, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return;

            _events[eventId] = new WorldEventMarker
            {
                eventId = eventId,
                label = label,
                type = type,
                worldPosition = worldPos,
                expiresAt = Time.unscaledTime + Mathf.Max(1f, durationSeconds)
            };

            UpsertMarker(new MinimapMarker
            {
                id = $"event:{eventId}",
                type = MarkerType.Event,
                target = null,
                worldPositionFallback = worldPos,
                label = label,
                visible = true
            });
        }

        public void SetQuestTarget(Transform target, Vector3 fallbackPos, string label = "Quest")
        {
            UpsertMarker(new MinimapMarker
            {
                id = "quest",
                type = MarkerType.Quest,
                target = target,
                worldPositionFallback = fallbackPos,
                label = label,
                visible = true
            });
        }

        public void RequestFastTravel(Vector3 destination)
        {
            OnFastTravelRequested?.Invoke(destination);
        }

        private void UpdateMinimapMarkers()
        {
            float range = ResolveZoomRange();
            _minimapView.SetMapName(_mapLoader != null ? _mapLoader.ActiveMapId.ToString() : "Map");

            foreach (var kv in _markers)
            {
                MinimapMarker marker = kv.Value;
                if (!marker.visible)
                {
                    _minimapView.SetMarker(marker.id, Vector3.zero, false, string.Empty);
                    continue;
                }

                Vector3 worldPos = marker.target != null ? marker.target.position : marker.worldPositionFallback;
                Vector3 offset = worldPos - _player.position;
                offset.y = 0f;

                bool inRange = offset.magnitude <= range;
                bool filtered = _worldMapVisible && !_worldMapFilters.Contains(marker.type);
                bool visible = inRange && !filtered;
                if (marker.useCustomColor)
                    _minimapView.SetMarkerStyle(marker.id, marker.markerColor);
                _minimapView.SetMarker(marker.id, offset, visible, marker.label);
            }
        }

        private float ResolveZoomRange()
        {
            return _zoom switch
            {
                MinimapZoomLevel.Near => _nearRangeMeters,
                MinimapZoomLevel.Far => _farRangeMeters,
                _ => _mediumRangeMeters
            };
        }

        private void HandleMapLoaded(MapLoader.MapId mapId)
        {
            _minimapView?.SetMapName(mapId.ToString());
        }

        private void CleanupExpiredEvents()
        {
            if (_events.Count == 0)
                return;

            float now = Time.unscaledTime;
            var expired = new List<string>();
            foreach (var kv in _events)
            {
                if (kv.Value.expiresAt <= now)
                    expired.Add(kv.Key);
            }

            for (int i = 0; i < expired.Count; i++)
            {
                string id = expired[i];
                _events.Remove(id);
                RemoveMarker($"event:{id}");
            }
        }
    }
}
