using System;
using UnityEngine;

namespace MuLike.Gameplay.Combat
{
    [Serializable]
    public struct CombatSkillModel
    {
        public int SkillId;
        public string DisplayName;
        public float Range;
        public float Cooldown;
        public int ManaCost;
        public int StaminaCost;
        public int AutoPriority;
        public bool AllowAutoCast;
        public bool LocksMovement;
        public KeyCode Hotkey;

        public bool IsValid => SkillId >= 0 && Cooldown >= 0f && Range >= 0f && ManaCost >= 0 && StaminaCost >= 0;
    }

    public enum CombatFeedbackType
    {
        BasicAttackStarted,
        SkillCastStarted,
        SkillCastRejected,
        SkillCastConfirmed
    }

    public struct CombatFeedbackEvent
    {
        public CombatFeedbackType Type;
        public int SkillId;
        public int TargetEntityId;
        public int Damage;
        public string Message;
        public Vector3 WorldPosition;
    }
}
