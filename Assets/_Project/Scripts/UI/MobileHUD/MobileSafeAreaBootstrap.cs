using UnityEngine;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Ensures HUD canvases have a safe-area fitter attached.
    /// </summary>
    public sealed class MobileSafeAreaBootstrap : MonoBehaviour
    {
        [SerializeField] private bool _applyOnStart = true;
        [SerializeField] private bool _scanAllRootCanvases = true;
        [SerializeField] private Canvas[] _explicitCanvases = new Canvas[0];

        private void Start()
        {
            if (_applyOnStart)
                Apply();
        }

        [ContextMenu("Apply Safe Area Setup")]
        public void Apply()
        {
            if (_scanAllRootCanvases)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                    TryEnsureSafeArea(canvases[i]);
            }

            for (int i = 0; i < _explicitCanvases.Length; i++)
                TryEnsureSafeArea(_explicitCanvases[i]);
        }

        private static void TryEnsureSafeArea(Canvas canvas)
        {
            if (canvas == null || !canvas.isRootCanvas)
                return;

            HudSafeAreaFitter[] existing = canvas.GetComponentsInChildren<HudSafeAreaFitter>(true);
            if (existing != null && existing.Length > 0)
                return;

            RectTransform rt = canvas.GetComponent<RectTransform>();
            if (rt == null)
                return;

            if (canvas.GetComponent<HudSafeAreaFitter>() == null)
                canvas.gameObject.AddComponent<HudSafeAreaFitter>();
        }
    }
}
