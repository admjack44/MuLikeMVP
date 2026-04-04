using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// View for the local or remote player entity. Extends EntityView with player-specific visuals.
    /// </summary>
    public class PlayerView : EntityView
    {
        [SerializeField] private GameObject _selectedIndicator;
        [SerializeField] private SkinnedMeshRenderer _characterRenderer;

        public bool IsLocalPlayer { get; private set; }

        public void InitializeAsLocal()
        {
            IsLocalPlayer = true;
        }

        public void SetSelectedIndicator(bool visible)
        {
            if (_selectedIndicator != null)
                _selectedIndicator.SetActive(visible);
        }

        public override void OnDeath()
        {
            base.OnDeath();
            SetSelectedIndicator(false);
        }
    }
}
