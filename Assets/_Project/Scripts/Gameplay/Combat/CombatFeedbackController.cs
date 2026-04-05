using MuLike.Performance.Pooling;
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
        [SerializeField] private string _hitPoolKey = "vfx.hit";
        [SerializeField] private string _critPoolKey = "vfx.crit";
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

            ParticleSystem instance = SpawnParticle(prefab, position);
            if (instance == null)
                return;

            instance.Clear(true);
            instance.Play(true);

            PoolAutoRelease autoRelease = instance.GetComponent<PoolAutoRelease>();
            if (autoRelease == null)
                autoRelease = instance.gameObject.AddComponent<PoolAutoRelease>();

            float lifetime = instance.main.duration + instance.main.startLifetime.constantMax + 0.15f;
            autoRelease.Arm(lifetime);
        }

        private void PlayClip(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        private ParticleSystem SpawnParticle(ParticleSystem prefab, Vector3 position)
        {
            MobilePoolManager manager = MobilePoolManager.Instance;
            string key = prefab == _critEffect ? _critPoolKey : _hitPoolKey;

            if (manager != null && !string.IsNullOrWhiteSpace(key)
                && manager.TrySpawn(key, position, Quaternion.identity, out GameObject pooledObject))
            {
                ParticleSystem pooledParticle = pooledObject.GetComponent<ParticleSystem>();
                if (pooledParticle != null)
                    return pooledParticle;

                manager.Release(pooledObject);
            }

            return Instantiate(prefab, position, Quaternion.identity);
        }
    }
}
