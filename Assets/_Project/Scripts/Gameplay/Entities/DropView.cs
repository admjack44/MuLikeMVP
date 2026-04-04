using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// View for item drops on the ground. Shows item name label and glow effect.
    /// </summary>
    public class DropView : EntityView
    {
        [SerializeField] private ParticleSystem _glowEffect;
        [SerializeField] private TMPro.TextMeshPro _nameLabel;

        public int ItemId { get; private set; }

        public void Setup(int itemId, string itemName)
        {
            ItemId = itemId;

            if (_nameLabel != null)
                _nameLabel.text = itemName;

            _glowEffect?.Play();
        }

        public void Pickup()
        {
            _glowEffect?.Stop();
            Destroy(gameObject, 0.3f);
        }
    }
}
