namespace MuLike.Bootstrap
{
    /// <summary>
    /// Client session state progression from initial connection to in-world gameplay.
    /// Used to gate scene transitions and coordinate UI feedback.
    /// </summary>
    public enum ClientSessionState
    {
        /// <summary>Not connected to any server or session active.</summary>
        Disconnected = 0,

        /// <summary>Attempting TCP connection to game server.</summary>
        Connecting = 1,

        /// <summary>Connected but waiting for auth credentials validation.</summary>
        Authenticating = 2,

        /// <summary>Auth complete; account token valid; ready for character selection.</summary>
        Authenticated = 3,

        /// <summary>Character selected; preparing to load world scene.</summary>
        CharacterSelection = 4,

        /// <summary>World load in progress; entities/assets loading.</summary>
        EnteringWorld = 5,

        /// <summary>In-world gameplay active; HUD visible.</summary>
        InWorld = 6,

        /// <summary>Auth/Network error state. Recovery requires manual retry.</summary>
        Failed = 7,
    }
}
