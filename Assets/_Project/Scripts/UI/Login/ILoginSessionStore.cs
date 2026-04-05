namespace MuLike.UI.Login
{
    public interface ILoginSessionStore
    {
        bool TryLoad(out LoginSessionData session);
        void Save(LoginSessionData session);
        void Clear();
    }
}
