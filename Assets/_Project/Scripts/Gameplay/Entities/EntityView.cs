using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// Base visual representation of any networked entity in the game world.
    /// </summary>
    public class EntityView : MonoBehaviour
    {
        public int EntityId { get; private set; }

        [SerializeField] private Animator _animator;

        public void Initialize(int entityId)
        {
            EntityId = entityId;
        }

        public virtual void SetPosition(Vector3 position)
        {
            transform.position = position;
        }

        public virtual void SetRotation(float rotationY)
        {
            transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }

        public virtual void PlayAnimation(string stateName)
        {
            _animator?.Play(stateName);
        }

        public virtual void OnDeath()
        {
            PlayAnimation("Death");
        }
    }
}
