using System;
using MuLike.VFX;
using UnityEngine;

namespace MuLike.Skills
{
    [Flags]
    public enum SkillTag
    {
        None = 0,
        Melee = 1 << 0,
        Ranged = 1 << 1,
        Magic = 1 << 2,
        Support = 1 << 3,
        Summon = 1 << 4,
        ComboStarter = 1 << 5,
        ComboLinker = 1 << 6,
        Ultimate = 1 << 7
    }

    public enum SkillTargetType
    {
        SingleTarget,
        AreaTarget,
        SelfCast,
        DirectionCast,
        ConeCast
    }

    [Serializable]
    public struct SkillResourceCost
    {
        public int mana;
        public int stamina;
        public int energy;
        public int command;
        public int special;
    }

    /// <summary>
    /// Data-only definition for skills. No runtime state here.
    /// </summary>
    [CreateAssetMenu(menuName = "MuLike/Skills/Skill Definition", fileName = "Skill_")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Header("Identity")]
        public int skillId;
        public string displayName;
        [TextArea] public string description;
        public SkillTag tags;
        public SkillTargetType targetType;

        [Header("Behavior")]
        [Min(0f)] public float baseRange = 3f;
        [Min(0f)] public float baseCooldown = 1f;
        [Min(0f)] public float baseCastTime = 0f;
        public bool lockMovementDuringCast = false;
        [Min(0f)] public float telegraphSeconds = 0.15f;

        [Header("Costs")]
        public SkillResourceCost cost;

        [Header("Runtime Grouping")]
        public SkillCooldownGroup sharedCooldownGroup;

        [Header("Animation")]
        public string animatorState = "Cast";
        public int upperBodyLayer = 1;
        [Min(0f)] public float hitFrameTime = 0.1f;

        [Header("VFX")]
        public SkillVfxProfile vfxProfile;

        [Header("Upgrades")]
        public SkillUpgradeNode[] upgrades = Array.Empty<SkillUpgradeNode>();

        public bool IsMelee => (tags & SkillTag.Melee) != 0;
        public bool IsUltimate => (tags & SkillTag.Ultimate) != 0;
    }
}
