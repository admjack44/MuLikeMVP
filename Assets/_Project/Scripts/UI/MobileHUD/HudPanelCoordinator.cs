using System;
using System.Collections.Generic;

namespace MuLike.UI.MobileHUD
{
    public sealed class HudPanelCoordinator
    {
        private readonly Dictionary<string, PanelState> _panels = new(StringComparer.Ordinal);

        public string ActivePanelId { get; private set; } = string.Empty;

        public void Register(string panelId, Func<bool> isVisible, Action<bool> setVisible)
        {
            if (string.IsNullOrWhiteSpace(panelId) || isVisible == null || setVisible == null)
                return;

            _panels[panelId] = new PanelState(isVisible, setVisible);
        }

        public void ToggleExclusive(string panelId)
        {
            if (!_panels.TryGetValue(panelId, out PanelState panel))
                return;

            if (panel.IsVisible())
            {
                panel.SetVisible(false);
                if (string.Equals(ActivePanelId, panelId, StringComparison.Ordinal))
                    ActivePanelId = string.Empty;
                return;
            }

            CloseAll();
            panel.SetVisible(true);
            ActivePanelId = panelId;
        }

        public void CloseAll()
        {
            foreach (KeyValuePair<string, PanelState> pair in _panels)
                pair.Value.SetVisible(false);

            ActivePanelId = string.Empty;
        }

        private readonly struct PanelState
        {
            public readonly Func<bool> IsVisible;
            public readonly Action<bool> SetVisible;

            public PanelState(Func<bool> isVisible, Action<bool> setVisible)
            {
                IsVisible = isVisible;
                SetVisible = setVisible;
            }
        }
    }
}
