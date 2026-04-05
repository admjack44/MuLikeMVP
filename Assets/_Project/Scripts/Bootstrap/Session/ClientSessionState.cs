using System;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Shared runtime session model across Boot/Login/CharacterSelect/World.
    /// </summary>
    public interface IClientSessionState
    {
        bool IsAuthenticated { get; }
        int SelectedCharacterId { get; }
        int CurrentMapId { get; }
        string CurrentWorldScene { get; }

        event Action SessionChanged;

        void MarkAuthenticated(bool authenticated);
        void SetWorldEntry(int characterId, int mapId, string worldSceneName);
        void ClearCharacterSelection();
    }

    /// <summary>
    /// Lightweight runtime-only session state for scene flow wiring and system binding.
    /// </summary>
    public sealed class RuntimeClientSessionState : IClientSessionState
    {
        public bool IsAuthenticated { get; private set; }
        public int SelectedCharacterId { get; private set; }
        public int CurrentMapId { get; private set; }
        public string CurrentWorldScene { get; private set; } = string.Empty;

        public event Action SessionChanged;

        public void MarkAuthenticated(bool authenticated)
        {
            if (IsAuthenticated == authenticated)
                return;

            IsAuthenticated = authenticated;
            SessionChanged?.Invoke();
        }

        public void SetWorldEntry(int characterId, int mapId, string worldSceneName)
        {
            bool changed = false;

            if (SelectedCharacterId != characterId)
            {
                SelectedCharacterId = characterId;
                changed = true;
            }

            if (CurrentMapId != mapId)
            {
                CurrentMapId = mapId;
                changed = true;
            }

            string safeScene = worldSceneName ?? string.Empty;
            if (!string.Equals(CurrentWorldScene, safeScene, StringComparison.Ordinal))
            {
                CurrentWorldScene = safeScene;
                changed = true;
            }

            if (changed)
                SessionChanged?.Invoke();
        }

        public void ClearCharacterSelection()
        {
            if (SelectedCharacterId == 0 && CurrentMapId == 0 && string.IsNullOrEmpty(CurrentWorldScene))
                return;

            SelectedCharacterId = 0;
            CurrentMapId = 0;
            CurrentWorldScene = string.Empty;
            SessionChanged?.Invoke();
        }
    }
}