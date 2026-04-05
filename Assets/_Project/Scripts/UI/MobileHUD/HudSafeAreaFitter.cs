using UnityEngine;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Fits a RectTransform to the screen safe area (notches / rounded corners).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HudSafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea = Rect.zero;
        private Vector2Int _lastScreenSize = Vector2Int.zero;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            Rect safeArea = Screen.safeArea;
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (safeArea != _lastSafeArea || size != _lastScreenSize)
                ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            Rect safeArea = Screen.safeArea;
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);

            Vector2 minAnchor = safeArea.position;
            Vector2 maxAnchor = safeArea.position + safeArea.size;

            minAnchor.x /= width;
            minAnchor.y /= height;
            maxAnchor.x /= width;
            maxAnchor.y /= height;

            _rectTransform.anchorMin = minAnchor;
            _rectTransform.anchorMax = maxAnchor;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
