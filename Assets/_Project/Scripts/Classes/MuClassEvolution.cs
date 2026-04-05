using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Classes
{
    /// <summary>
    /// Generic evolution tier used by every class (0..5).
    /// This is intentionally class-agnostic and data-driven.
    /// </summary>
    public enum MuEvolutionTier
    {
        Tier0Base = 0,
        Tier1FirstEvolution = 1,
        Tier2SecondEvolution = 2,
        Tier3ThirdEvolution = 3,
        Tier4Awakening = 4,
        Tier5Ascension = 5
    }

    [Serializable]
    public struct EvolutionStatMultipliers
    {
        public float hp;
        public float mana;
        public float stamina;
        public float damage;
        public float defense;
        public float attackSpeed;

        public static EvolutionStatMultipliers Identity()
        {
            return new EvolutionStatMultipliers
            {
                hp = 1f,
                mana = 1f,
                stamina = 1f,
                damage = 1f,
                defense = 1f,
                attackSpeed = 1f
            };
        }
    }

    [Serializable]
    public struct EvolutionPassiveBonus
    {
        public string id;
        public string displayName;
        public float value;
        public string description;
    }

    [Serializable]
    public struct MuClassEvolutionData
    {
        public string id;
        public string displayName;
        public MuEvolutionTier tier;
        public int requiredLevel;
        public int requiredQuestId;
        public EvolutionStatMultipliers statMultipliers;
        public List<int> unlockedSkillIds;
        public List<EvolutionPassiveBonus> passiveBonuses;
        public int vfxTier;
        public RuntimeAnimatorController animatorOverride;
        public Color uiAccent;
        public string unlockDescription;

        public bool IsValid => !string.IsNullOrWhiteSpace(id) && requiredLevel >= 0;
    }
}
