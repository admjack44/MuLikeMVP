using System;

namespace MuLike.Classes
{
    /// <summary>
    /// Playable classes available in MU mobile runtime.
    /// Keep this enum stable because ids may be persisted in save/network payloads.
    /// </summary>
    public enum MuClassId
    {
        Unknown = 0,
        DarkWizard = 1,
        DarkKnight = 2,
        Elf = 3,
        MagicGladiator = 4,
        DarkLord = 5,
        Slayer = 6,
        RageFighter = 7,
        IllusionKnight = 8
    }

    public enum MuClassArchetype
    {
        Caster,
        Frontliner,
        Support,
        Hybrid,
        Assassin,
        Brawler,
        SpectralHybrid
    }

    public enum UnlockRestrictionType
    {
        None,
        RequiredLevel,
        RequiredQuest,
        AccountProgression
    }

    [Serializable]
    public struct ClassUnlockRequirement
    {
        public UnlockRestrictionType type;
        public int requiredLevel;
        public int requiredQuestId;
        public string requiredAccountFlag;

        public static ClassUnlockRequirement None()
        {
            return new ClassUnlockRequirement { type = UnlockRestrictionType.None };
        }

        public static ClassUnlockRequirement Level(int level)
        {
            return new ClassUnlockRequirement
            {
                type = UnlockRestrictionType.RequiredLevel,
                requiredLevel = level
            };
        }
    }
}
