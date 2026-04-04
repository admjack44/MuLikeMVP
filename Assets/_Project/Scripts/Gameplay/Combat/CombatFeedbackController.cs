using UnityEngine;

namespace MuLike.Gameplay.Combat
{
    /// <summary>
    /// Plays visual and audio feedback for combat events: hit flashes, death effects, sound cues.
    /// </summary>
    public class CombatFeedbackController : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _hitEffect;
        [SerializeField] private ParticleSystem _critEffect;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private AudioClip _critClip;

        public void PlayHit(Vector3 worldPosition)
        {
            SpawnEffect(_hitEffect, worldPosition);
            PlayClip(_hitClip);
        }

        public void PlayCrit(Vector3 worldPosition)
        {
            SpawnEffect(_critEffect, worldPosition);
            PlayClip(_critClip);
        }

        private void SpawnEffect(ParticleSystem prefab, Vector3 position)
        {
            if (prefab == null) return;
            ParticleSystem instance = Instantiate(prefab, position, Quaternion.identity);
            Destroy(instance.gameObject, instance.main.duration + 0.5f);
        }

        private void PlayClip(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
