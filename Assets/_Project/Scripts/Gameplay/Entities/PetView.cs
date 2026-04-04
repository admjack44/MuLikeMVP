using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// View for pet entities. Follows the owner and displays pet-specific effects.
    /// </summary>
    public class PetView : EntityView
    {
        [SerializeField] private ParticleSystem _auraEffect;

        public EntityView Owner { get; private set; }

        public void SetOwner(EntityView owner)
        {
            Owner = owner;
        }

        public void SetAura(bool active)
        {
            if (_auraEffect == null) return;

            if (active) _auraEffect.Play();
            else _auraEffect.Stop();
        }
    }
}
