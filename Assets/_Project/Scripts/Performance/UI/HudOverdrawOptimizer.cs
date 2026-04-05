using UnityEngine;
using UnityEngine.UI;

namespace MuLike.Performance.UI
{
    /// <summary>
    /// Reduces HUD overdraw by disabling unnecessary raycasts and flattening hidden layers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudOverdrawOptimizer : MonoBehaviour
    {
        [SerializeField] private Canvas _rootCanvas;
        [SerializeField] private bool _disableRaycastOnDecorativeGraphics = true;
        [SerializeField] private Graphic[] _raycastWhitelist;
        [SerializeField] private CanvasGroup[] _nonCriticalGroups;

        public void ApplyOptimization()
        {
            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>();

            if (_rootCanvas != null)
                _rootCanvas.pixelPerfect = false;

            if (_disableRaycastOnDecorativeGraphics)
                DisableDecorativeRaycasts();
        }

        public void SetNonCriticalVisible(bool visible)
        {
            if (_nonCriticalGroups == null)
                return;

            for (int i = 0; i < _nonCriticalGroups.Length; i++)
            {
                CanvasGroup group = _nonCriticalGroups[i];
                if (group == null)
                    continue;

                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }
        }

        private void Awake()
        {
            ApplyOptimization();
        }

        private void DisableDecorativeRaycasts()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(includeInactive: true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic g = graphics[i];
                if (g == null)
                    continue;

                if (IsWhitelisted(g))
                    continue;

                if (g.GetComponent<Button>() != null || g.GetComponent<Toggle>() != null || g.GetComponent<Slider>() != null)
                    continue;

                g.raycastTarget = false;
            }
        }

        private bool IsWhitelisted(Graphic graphic)
        {
            if (_raycastWhitelist == null)
                return false;

            for (int i = 0; i < _raycastWhitelist.Length; i++)
            {
                if (_raycastWhitelist[i] == graphic)
                    return true;
            }

            return false;
        }
    }
}
