using MuLike.UI.Equipment;

namespace MuLike.UI.MobileHUD
{
    public sealed class EquipmentPanelPresenter
    {
        private readonly EquipmentPanelView _view;

        public EquipmentPanelPresenter(EquipmentPanelView view)
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
