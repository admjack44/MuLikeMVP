using UnityEngine;

namespace MuLike.Performance.UI
{
    /// <summary>
    /// Time gate for reducing UI update frequency.
    /// </summary>
    public sealed class UiUpdateThrottler
    {
        private readonly float _minInterval;
        private float _nextUpdateAt;

        public UiUpdateThrottler(float updatesPerSecond)
        {
            float safeRate = Mathf.Clamp(updatesPerSecond, 1f, 120f);
            _minInterval = 1f / safeRate;
            _nextUpdateAt = 0f;
        }

        public bool ShouldRunNow(float now)
        {
            if (now < _nextUpdateAt)
                return false;

            _nextUpdateAt = now + _minInterval;
            return true;
        }
    }
}
