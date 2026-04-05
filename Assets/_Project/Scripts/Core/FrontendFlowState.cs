namespace MuLike.Core
{
    /// <summary>
    /// Persistent frontend session/runtime flow state used by FrontendFlowDirector.
    /// </summary>
    public sealed class FrontendFlowState
    {
        public bool IsAuthenticated { get; private set; }
        public int AccountId { get; private set; }
        public string SessionToken { get; private set; } = string.Empty;
        public int SelectedCharacterId { get; private set; }
        public string SelectedCharacterName { get; private set; } = string.Empty;
        public string LastWorldScene { get; private set; } = string.Empty;

        public void SetAuthenticated(bool isAuthenticated, int accountId, string sessionToken)
        {
            IsAuthenticated = isAuthenticated;
            AccountId = accountId;
            SessionToken = sessionToken ?? string.Empty;
        }

        public void SetSelectedCharacter(int characterId, string characterName)
        {
            SelectedCharacterId = characterId;
            SelectedCharacterName = characterName ?? string.Empty;
        }

        public void SetLastWorldScene(string sceneName)
        {
            LastWorldScene = sceneName ?? string.Empty;
        }

        public void ResetForLogout()
        {
            IsAuthenticated = false;
            AccountId = 0;
            SessionToken = string.Empty;
            SelectedCharacterId = 0;
            SelectedCharacterName = string.Empty;
            LastWorldScene = string.Empty;
        }
    }
}