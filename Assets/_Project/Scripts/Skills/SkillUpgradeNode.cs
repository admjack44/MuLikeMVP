using System;

namespace MuLike.Skills
{
    [Serializable]
    public struct SkillUpgradeNode
    {
        public int level;
        public float damageMultiplier;
        public float cooldownDeltaSeconds;
        public int manaCostDelta;
        public int staminaCostDelta;
        public int energyCostDelta;
        public int commandCostDelta;
        public float castTimeDeltaSeconds;
        public float rangeDelta;
    }
}
