using UnityEngine;

namespace MuLike.Combat
{
    /// <summary>
    /// Global mobile combat tuning profile.
    /// </summary>
    [CreateAssetMenu(menuName = "MuLike/Combat/Tuning Profile", fileName = "CombatTuning_Mobile")]
    public sealed class CombatTuningProfile : ScriptableObject
    {
        [Header("Mobile Global")]
        [Range(0.3f, 1f)] public float mobileCastTimeMultiplier = 0.7f;
        [Range(1f, 2f)] public float mobileMeleeRangeMultiplier = 1.15f;
        [Range(0.5f, 2f)] public float comboWindowMultiplier = 1.2f;
        [Range(0.02f, 0.5f)] public float shortTelegraphSeconds = 0.12f;

        [Header("FX")]
        public bool allowHeavyParticlesOnMidRange = false;
        public bool enableFxPooling = true;
    }
}
