using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    public sealed class MinimapView : MonoBehaviour
    {
        [Serializable]
        public struct MarkerBinding
        {
            public string markerId;
            public RectTransform markerTransform;
            public Image markerImage;
            public TMP_Text markerLabel;
        }

        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private RectTransform _mapViewport;
        [SerializeField] private TMP_Text _mapNameText;
        [SerializeField] private float _worldRadius = 60f;
        [SerializeField] private MarkerBinding[] _fixedMarkers = Array.Empty<MarkerBinding>();

        private readonly Dictionary<string, MarkerBinding> _markers = new(StringComparer.OrdinalIgnoreCase);

        public event Action ExpandedMapRequested;

        private void Awake()
        {
            _markers.Clear();
            for (int i = 0; i < _fixedMarkers.Length; i++)
            {
                MarkerBinding marker = _fixedMarkers[i];
                if (!string.IsNullOrWhiteSpace(marker.markerId))
                    _markers[marker.markerId] = marker;
            }
        }

        public void SetMapName(string mapName)
        {
            if (_mapNameText != null)
                _mapNameText.text = string.IsNullOrWhiteSpace(mapName) ? "Unknown" : mapName;
        }

        public void SetMarker(string markerId, Vector3 worldOffsetFromPlayer, bool visible, string label = null)
        {
            if (!_markers.TryGetValue(markerId, out MarkerBinding marker) || marker.markerTransform == null || _mapViewport == null)
                return;

            marker.markerTransform.gameObject.SetActive(visible);
            if (!visible)
                return;

            Vector2 anchored = WorldOffsetToMinimap(worldOffsetFromPlayer);
            marker.markerTransform.anchoredPosition = anchored;

            if (marker.markerLabel != null)
                marker.markerLabel.text = label ?? string.Empty;
        }

        public void SetVisible(bool visible)
        {
            if (_panelRoot != null)
                _panelRoot.gameObject.SetActive(visible);
        }

        public void RequestExpandedMap()
        {
            ExpandedMapRequested?.Invoke();
        }

        private Vector2 WorldOffsetToMinimap(Vector3 worldOffset)
        {
            float radius = Mathf.Max(1f, _worldRadius);
            Vector2 normalized = new Vector2(worldOffset.x / radius, worldOffset.z / radius);
            normalized = Vector2.ClampMagnitude(normalized, 1f);

            float w = _mapViewport.rect.width * 0.5f;
            float h = _mapViewport.rect.height * 0.5f;
            return new Vector2(normalized.x * w, normalized.y * h);
        }
    }
}
