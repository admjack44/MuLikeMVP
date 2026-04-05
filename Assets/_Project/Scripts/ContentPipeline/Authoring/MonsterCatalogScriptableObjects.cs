using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.ContentPipeline.Authoring
{
    [Serializable]
    public struct MonsterDefinitionRecord
    {
        public int monsterId;
        public string name;
        public int level;
        public int hpMax;
        public int attack;
        public int defense;
        public float aggroRadius;
        public float chaseRadius;
        public float leashRadius;
        public float attackRange;
        public float moveSpeed;
        public float respawnSeconds;
        public int expReward;
        public string dropTableId;
    }

    [CreateAssetMenu(fileName = "MonsterDefinition", menuName = "MuLike/Content Pipeline/Monster Definition")]
    public sealed class MonsterDefinitionAsset : ScriptableObject
    {
        [SerializeField] private MonsterDefinitionRecord definition;

        public MonsterDefinitionRecord Definition => definition;
    }

    [CreateAssetMenu(fileName = "MonsterCatalogDatabase", menuName = "MuLike/Content Pipeline/Monster Catalog Database")]
    public sealed class MonsterCatalogDatabase : ScriptableObject
    {
        public List<MonsterDefinitionAsset> monsterAssets = new();
        public List<MonsterDefinitionRecord> inlineMonsters = new();

        public List<MonsterDefinitionRecord> BuildDefinitions()
        {
            var monsters = new List<MonsterDefinitionRecord>();

            for (int i = 0; i < monsterAssets.Count; i++)
            {
                if (monsterAssets[i] == null)
                    continue;

                monsters.Add(monsterAssets[i].Definition);
            }

            monsters.AddRange(inlineMonsters);
            return monsters;
        }
    }
}
