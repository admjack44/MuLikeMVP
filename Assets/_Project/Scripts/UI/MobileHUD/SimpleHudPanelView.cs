using UnityEngine;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Minimal panel toggle helper for HUD shortcuts (character/map/etc).
    /// </summary>
    public sealed class SimpleHudPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private bool _startVisible;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            SetVisible(_startVisible);
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;

            if (_panelRoot != null)
                _panelRoot.SetActive(visible);
        }

        public void ToggleVisible()
        {
            SetVisible(!IsVisible);
        }
    }
}
