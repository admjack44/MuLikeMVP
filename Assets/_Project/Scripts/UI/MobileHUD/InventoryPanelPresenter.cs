using MuLike.UI.Inventory;

namespace MuLike.UI.MobileHUD
{
    public sealed class InventoryPanelPresenter
    {
        private readonly InventoryView _view;

        public InventoryPanelPresenter(InventoryView view)
        {
            _view = view;
        }

        public bool IsVisible => _view != null && _view.IsVisible;

        public void SetVisible(bool visible)
        {
            _view?.SetVisible(visible);
        }
    }
}
