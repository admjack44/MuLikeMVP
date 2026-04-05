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
        [SerializeField] private RectTransform _runtimeMarkerParent;
        [SerializeField] private Sprite _runtimeMarkerSprite;
        [SerializeField] private TMP_FontAsset _runtimeMarkerFont;

        private readonly Dictionary<string, MarkerBinding> _markers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _runtimeMarkerIds = new(StringComparer.OrdinalIgnoreCase);

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
            if (!_markers.TryGetValue(markerId, out MarkerBinding marker))
            {
                marker = CreateRuntimeMarker(markerId);
                if (marker.markerTransform != null)
                    _markers[markerId] = marker;
            }

            if (marker.markerTransform == null || _mapViewport == null)
                return;

            marker.markerTransform.gameObject.SetActive(visible);
            if (!visible)
                return;

            Vector2 anchored = WorldOffsetToMinimap(worldOffsetFromPlayer);
            marker.markerTransform.anchoredPosition = anchored;

            if (marker.markerLabel != null)
                marker.markerLabel.text = label ?? string.Empty;
        }

        public void SetMarkerStyle(string markerId, Color color)
        {
            if (!_markers.TryGetValue(markerId, out MarkerBinding marker))
            {
                marker = CreateRuntimeMarker(markerId);
                if (marker.markerTransform != null)
                    _markers[markerId] = marker;
            }

            if (marker.markerImage != null)
                marker.markerImage.color = color;

            if (marker.markerLabel != null)
                marker.markerLabel.color = color;
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

        private MarkerBinding CreateRuntimeMarker(string markerId)
        {
            RectTransform parent = _runtimeMarkerParent != null ? _runtimeMarkerParent : _mapViewport;
            if (parent == null || string.IsNullOrWhiteSpace(markerId))
                return default;

            if (_runtimeMarkerIds.Contains(markerId) && _markers.TryGetValue(markerId, out MarkerBinding existing))
                return existing;

            GameObject root = new GameObject($"Marker_{markerId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.sizeDelta = new Vector2(18f, 18f);

            Image image = root.GetComponent<Image>();
            image.sprite = _runtimeMarkerSprite;
            image.color = Color.white;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMP_Text));
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(rootRect, false);
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -8f);
            labelRect.sizeDelta = new Vector2(84f, 18f);

            TMP_Text label = labelGo.GetComponent<TMP_Text>();
            label.fontSize = 14f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = string.Empty;
            if (_runtimeMarkerFont != null)
                label.font = _runtimeMarkerFont;

            root.SetActive(false);
            _runtimeMarkerIds.Add(markerId);
            return new MarkerBinding
            {
                markerId = markerId,
                markerTransform = rootRect,
                markerImage = image,
                markerLabel = label
            };
        }
    }
}
