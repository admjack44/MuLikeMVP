using MuLike.Shared.Content;

namespace MuLike.Server.Infrastructure.ContentPipeline
{
    public static class ServerBalanceRuntimeConfig
    {
        public static ContentBalanceDto Current { get; private set; } = new ContentBalanceDto
        {
            damageMultiplier = 1f,
            defenseMultiplier = 1f,
            skillDamageMultiplier = 1f,
            expMultiplier = 1f,
            dropRateMultiplier = 1f,
            zenMultiplier = 1f,
            respawnSpeedMultiplier = 1f,
            eliteSpawnChance = 0.02f
        };

        public static void Apply(ContentBalanceDto dto)
        {
            if (dto == null)
                return;

            Current = dto;
        }
    }
}
