using MuLike.Classes;
using UnityEngine;

namespace MuLike.Combat
{
    /// <summary>
    /// Per-class combo configuration.
    /// </summary>
    [CreateAssetMenu(menuName = "MuLike/Combat/Combo Definition", fileName = "Combo_")]
    public sealed class ComboDefinition : ScriptableObject
    {
        public MuClassId classId;
        public string comboId;
        public ComboStep[] steps;
        [Min(0.05f)] public float resetTimeout = 1.8f;
        [Min(1f)] public float finalStepBonusMultiplier = 1.25f;
        public bool showComboIndicator = true;
    }
}
