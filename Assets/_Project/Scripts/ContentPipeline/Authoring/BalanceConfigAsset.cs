using System;
using UnityEngine;

namespace MuLike.ContentPipeline.Authoring
{
    [Serializable]
    public struct BalanceConfigRecord
    {
        public float damageMultiplier;
        public float defenseMultiplier;
        public float skillDamageMultiplier;
        public float expMultiplier;
        public float dropRateMultiplier;
        public float zenMultiplier;
        public float respawnSpeedMultiplier;
        public float eliteSpawnChance;
    }

    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "MuLike/Content Pipeline/Balance Config")]
    public sealed class BalanceConfigAsset : ScriptableObject
    {
        public BalanceConfigRecord config = new BalanceConfigRecord
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
    }
}
