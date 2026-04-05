using UnityEngine;

namespace MuLike.UI.MobileHUD
{
    public sealed class HudAutoHidePresenter
    {
        private readonly MobileHudView _view;
        private readonly float _idleSeconds;

        private float _timeSinceInput;
        private bool _forceVisible;

        public HudAutoHidePresenter(MobileHudView view, float idleSeconds)
        {
            _view = view;
            _idleSeconds = Mathf.Max(2f, idleSeconds);
        }

        public void NotifyInputActivity()
        {
            _timeSinceInput = 0f;
            _view.SetNonCriticalVisible(true);
        }

        public void SetForceVisible(bool visible)
        {
            _forceVisible = visible;
            if (visible)
                _view.SetNonCriticalVisible(true);
        }

        public void Tick(float deltaTime)
        {
            if (_forceVisible)
                return;

            _timeSinceInput += Mathf.Max(0f, deltaTime);
            bool show = _timeSinceInput < _idleSeconds;
            _view.SetNonCriticalVisible(show);
        }
    }
}
