using System;
using System.Collections.Generic;

namespace MuLike.Skills
{
    /// <summary>
    /// Runtime loadout selection for quick skill slots.
    /// </summary>
    [Serializable]
    public sealed class SkillLoadout
    {
        public const int MaxSlots = 6;

        [Serializable]
        public struct Slot
        {
            public int slotIndex;
            public SkillDefinition skill;
            public int upgradeLevel;
        }

        public List<Slot> slots = new(MaxSlots);

        public bool TryGetBySlot(int slotIndex, out Slot slot)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].slotIndex != slotIndex)
                    continue;

                slot = slots[i];
                return true;
            }

            slot = default;
            return false;
        }

        public bool TryGetBySkillId(int skillId, out Slot slot)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                SkillDefinition s = slots[i].skill;
                if (s == null || s.skillId != skillId)
                    continue;

                slot = slots[i];
                return true;
            }

            slot = default;
            return false;
        }
    }
}
