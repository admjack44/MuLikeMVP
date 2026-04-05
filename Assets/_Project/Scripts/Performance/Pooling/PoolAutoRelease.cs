using UnityEngine;

namespace MuLike.Performance.Pooling
{
    /// <summary>
    /// Returns pooled objects to MobilePoolManager after a configured lifetime.
    /// </summary>
    public sealed class PoolAutoRelease : MonoBehaviour
    {
        [SerializeField] private bool _useUnscaledTime;

        private float _releaseAt;
        private bool _armed;

        public void Arm(float lifetimeSeconds)
        {
            if (lifetimeSeconds <= 0f)
            {
                ReleaseNow();
                return;
            }

            float now = _useUnscaledTime ? Time.unscaledTime : Time.time;
            _releaseAt = now + lifetimeSeconds;
            _armed = true;
            enabled = true;
        }

        public void ReleaseNow()
        {
            _armed = false;
            enabled = false;

            if (MobilePoolManager.Instance != null)
            {
                MobilePoolManager.Instance.Release(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        private void Update()
        {
            if (!_armed)
                return;

            float now = _useUnscaledTime ? Time.unscaledTime : Time.time;
            if (now < _releaseAt)
                return;

            ReleaseNow();
        }
    }
}