using MuLike.UI.Chat;

namespace MuLike.UI.MobileHUD
{
    public sealed class ChatPanelPresenter
    {
        private readonly ChatView _view;

        public ChatPanelPresenter(ChatView view)
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
