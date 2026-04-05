using System;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Lightweight runtime session state shared by client systems and flow services.
    /// Tracks authentication, character selection, and in-world state transitions.
    /// </summary>
    public sealed class SessionStateClient
    {
        public ClientSessionState State { get; private set; } = ClientSessionState.Disconnected;
        public bool IsAuthenticated { get; private set; }
        public int AccountId { get; private set; }
        public int CharacterId { get; private set; }
        public string SessionToken { get; private set; } = string.Empty;
        public string CurrentWorldScene { get; private set; } = string.Empty;

        public event Action StateChanged;
        public event Action<ClientSessionState, ClientSessionState> StateTransitioned;

        /// <summary>
        /// Attempt transition to requested state. No-op if already in that state.
        /// Emits StateTransitioned event on successful transition.
        /// </summary>
        public bool TryTransitionTo(ClientSessionState requestedState)
        {
            if (State == requestedState)
                return false;

            ClientSessionState previousState = State;
            State = requestedState;
            StateChanged?.Invoke();
            StateTransitioned?.Invoke(previousState, requestedState);
            return true;
        }

        public void SetAuthenticatedSession(int accountId, string sessionToken)
        {
            bool changed = false;

            if (!IsAuthenticated)
            {
                IsAuthenticated = true;
                changed = true;
            }

            if (AccountId != accountId)
            {
                AccountId = accountId;
                changed = true;
            }

            string safeToken = sessionToken ?? string.Empty;
            if (!string.Equals(SessionToken, safeToken, StringComparison.Ordinal))
            {
                SessionToken = safeToken;
                changed = true;
            }

            if (changed)
                StateChanged?.Invoke();
        }

        public void SetCharacter(int characterId)
        {
            if (CharacterId == characterId)
                return;

            CharacterId = characterId;
            StateChanged?.Invoke();
        }

        public void SetWorldScene(string sceneName)
        {
            string safe = sceneName ?? string.Empty;
            if (string.Equals(CurrentWorldScene, safe, StringComparison.Ordinal))
                return;

            CurrentWorldScene = safe;
            StateChanged?.Invoke();
        }

        public void ClearForLogout()
        {
            bool changed = IsAuthenticated
                || AccountId != 0
                || CharacterId != 0
                || !string.IsNullOrEmpty(SessionToken)
                || !string.IsNullOrEmpty(CurrentWorldScene)
                || State != ClientSessionState.Disconnected;

            IsAuthenticated = false;
            AccountId = 0;
            CharacterId = 0;
            SessionToken = string.Empty;
            CurrentWorldScene = string.Empty;
            ClientSessionState previousState = State;
            State = ClientSessionState.Disconnected;

            if (changed)
            {
                StateChanged?.Invoke();
                if (previousState != ClientSessionState.Disconnected)
                    StateTransitioned?.Invoke(previousState, ClientSessionState.Disconnected);
            }
        }
    }
}