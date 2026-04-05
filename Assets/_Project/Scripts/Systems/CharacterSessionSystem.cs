using System;

namespace MuLike.Systems
{
    /// <summary>
    /// Holds authenticated account/session/character selection state for client runtime flows.
    /// </summary>
    public sealed class CharacterSessionSystem
    {
        [Serializable]
        public struct SessionSnapshot
        {
            public bool IsAuthenticated;
            public int AccountId;
            public string SessionToken;
            public int SelectedCharacterId;
            public string SelectedCharacterName;
            public string LastWorldScene;
        }

        [Serializable]
        public struct SessionDelta
        {
            public bool HasAuthentication;
            public bool IsAuthenticated;
            public int AccountId;
            public string SessionToken;

            public bool HasCharacterSelection;
            public int SelectedCharacterId;
            public string SelectedCharacterName;

            public bool HasLastWorldScene;
            public string LastWorldScene;
        }

        public SessionSnapshot Snapshot { get; private set; }

        public event Action<SessionSnapshot> OnSessionSnapshotApplied;
        public event Action<SessionDelta> OnSessionDeltaApplied;
        public event Action<SessionSnapshot> OnSessionChanged;
        public event Action<bool, int> OnAuthenticationChanged;
        public event Action<int, string> OnCharacterSelectionChanged;

        public void ApplySnapshot(SessionSnapshot snapshot)
        {
            SessionSnapshot previous = Snapshot;
            Snapshot = Normalize(snapshot);

            EmitChanges(previous, Snapshot);
            OnSessionSnapshotApplied?.Invoke(Snapshot);
            OnSessionChanged?.Invoke(Snapshot);
        }

        public void ApplyDelta(SessionDelta delta)
        {
            SessionSnapshot previous = Snapshot;
            SessionSnapshot next = Snapshot;

            if (delta.HasAuthentication)
            {
                next.IsAuthenticated = delta.IsAuthenticated;
                next.AccountId = Math.Max(0, delta.AccountId);
                next.SessionToken = delta.SessionToken ?? string.Empty;
            }

            if (delta.HasCharacterSelection)
            {
                next.SelectedCharacterId = Math.Max(0, delta.SelectedCharacterId);
                next.SelectedCharacterName = delta.SelectedCharacterName ?? string.Empty;
            }

            if (delta.HasLastWorldScene)
                next.LastWorldScene = delta.LastWorldScene ?? string.Empty;

            Snapshot = Normalize(next);

            EmitChanges(previous, Snapshot);
            OnSessionDeltaApplied?.Invoke(delta);
            OnSessionChanged?.Invoke(Snapshot);
        }

        public void Clear()
        {
            ApplySnapshot(default);
        }

        private static SessionSnapshot Normalize(SessionSnapshot snapshot)
        {
            snapshot.AccountId = Math.Max(0, snapshot.AccountId);
            snapshot.SessionToken = snapshot.SessionToken ?? string.Empty;
            snapshot.SelectedCharacterId = Math.Max(0, snapshot.SelectedCharacterId);
            snapshot.SelectedCharacterName = snapshot.SelectedCharacterName ?? string.Empty;
            snapshot.LastWorldScene = snapshot.LastWorldScene ?? string.Empty;

            if (!snapshot.IsAuthenticated)
            {
                snapshot.AccountId = 0;
                snapshot.SessionToken = string.Empty;
            }

            return snapshot;
        }

        private void EmitChanges(SessionSnapshot previous, SessionSnapshot current)
        {
            if (previous.IsAuthenticated != current.IsAuthenticated || previous.AccountId != current.AccountId)
                OnAuthenticationChanged?.Invoke(current.IsAuthenticated, current.AccountId);

            if (previous.SelectedCharacterId != current.SelectedCharacterId
                || !string.Equals(previous.SelectedCharacterName, current.SelectedCharacterName, StringComparison.Ordinal))
            {
                OnCharacterSelectionChanged?.Invoke(current.SelectedCharacterId, current.SelectedCharacterName);
            }
        }
    }
}
