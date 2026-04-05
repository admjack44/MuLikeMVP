using System;
using System.Collections.Generic;
using MuLike.Skills;
using UnityEngine;

namespace MuLike.Classes
{
    [Serializable]
    public struct MuClassBaseStats
    {
        public int hp;
        public int mana;
        public int stamina;
        public int command;
        public int energy;
        public int damageMin;
        public int damageMax;
        public int defense;
        public float moveSpeed;
    }

    /// <summary>
    /// Data definition for a playable class.
    /// Class identity/restrictions live here; progression tiers are included as data only.
    /// Runtime execution remains outside this file.
    /// </summary>
    [CreateAssetMenu(menuName = "MuLike/Classes/Class Definition", fileName = "ClassDefinition_")]
    public sealed class MuClassDefinition : ScriptableObject
    {
        [Header("Identity")]
        public MuClassId classId;
        public string displayName;
        public MuClassArchetype archetype;
        [TextArea] public string description;

        [Header("Unlock")]
        public ClassUnlockRequirement unlockRequirement = ClassUnlockRequirement.None();

        [Header("Base")]
        public MuClassBaseStats baseStats;
        public RuntimeAnimatorController defaultAnimatorController;

        [Header("Skills")]
        public SkillDefinition basicAttack;
        public SkillDefinition[] activeSkills = Array.Empty<SkillDefinition>();
        public SkillDefinition ultimateSkill;
        public SkillDefinition[] optionalPassiveSkills = Array.Empty<SkillDefinition>();

        [Header("Evolution Tiers")]
        public List<MuClassEvolutionData> evolutions = new();

        public bool IsUnlockedByLevel(int accountLevel)
        {
            if (unlockRequirement.type != UnlockRestrictionType.RequiredLevel)
                return true;

            return accountLevel >= unlockRequirement.requiredLevel;
        }
    }
}
