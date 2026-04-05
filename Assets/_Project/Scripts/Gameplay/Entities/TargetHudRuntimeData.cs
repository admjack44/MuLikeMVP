using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// Optional metadata component used by mobile HUD target portrait.
    /// </summary>
    public sealed class TargetHudRuntimeData : MonoBehaviour
    {
        [SerializeField] private string _displayName = "Target";
        [SerializeField, Min(1)] private int _level = 1;
        [SerializeField, Min(0)] private int _hpCurrent = 100;
        [SerializeField, Min(0)] private int _hpMax = 100;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? "Target" : _displayName;
        public int Level => Mathf.Max(1, _level);
        public int HpCurrent => Mathf.Clamp(_hpCurrent, 0, Mathf.Max(0, _hpMax));
        public int HpMax => Mathf.Max(0, _hpMax);

        public void ApplyState(string displayName, int level, int hpCurrent, int hpMax)
        {
            _displayName = displayName ?? string.Empty;
            _level = Mathf.Max(1, level);
            _hpMax = Mathf.Max(0, hpMax);
            _hpCurrent = Mathf.Clamp(hpCurrent, 0, _hpMax > 0 ? _hpMax : int.MaxValue);
        }
    }
}
