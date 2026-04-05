using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.Systems
{
    public sealed class StatRebuildService
    {
        public (int hpMax, int manaMax, int minDamage, int maxDamage, int defense) Rebuild(
            int level,
            int strength,
            int agility,
            int vitality,
            int energy)
        {
            int hpMax = 100 + (vitality * 3) + level;
            int manaMax = 50 + (energy * 2) + level;
            int minDamage = 5 + (strength / 3);
            int maxDamage = minDamage + (strength / 2);
            int defense = agility / 4;

            return (hpMax, manaMax, minDamage, maxDamage, defense);
        }

        public void RebuildPlayerWithEquipment(PlayerEntity player, int attackBonus, int defenseBonus, int hpBonus)
        {
            if (player == null)
                return;

            // Reset to level base first.
            player.SetLevel(player.Level);
            player.ApplyEquipmentBonuses(attackBonus, defenseBonus, hpBonus);
        }
    }
}
