using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.ContentPipeline.Authoring
{
    public enum SkillCastTypeAuthoring
    {
        SingleTarget = 0,
        Self = 1,
        Area = 2
    }

    [Serializable]
    public struct SkillDefinitionRecord
    {
        public int skillId;
        public string name;
        public string description;
        public int manaCost;
        public float cooldownSeconds;
        public float castRange;
        public float areaRadius;
        public SkillCastTypeAuthoring castType;
        public int minLevel;
        public int baseDamage;
        public float attackScale;
        public float defenseScale;
    }

    [CreateAssetMenu(fileName = "SkillDefinition", menuName = "MuLike/Content Pipeline/Skill Definition")]
    public sealed class SkillDefinitionAsset : ScriptableObject
    {
        [SerializeField] private SkillDefinitionRecord definition;

        public SkillDefinitionRecord Definition => definition;
    }

    [CreateAssetMenu(fileName = "SkillCatalogDatabase", menuName = "MuLike/Content Pipeline/Skill Catalog Database")]
    public sealed class SkillCatalogDatabase : ScriptableObject
    {
        public List<SkillDefinitionAsset> skillAssets = new();
        public List<SkillDefinitionRecord> inlineSkills = new();

        public List<SkillDefinitionRecord> BuildDefinitions()
        {
            var skills = new List<SkillDefinitionRecord>();

            for (int i = 0; i < skillAssets.Count; i++)
            {
                if (skillAssets[i] == null)
                    continue;

                skills.Add(skillAssets[i].Definition);
            }

            skills.AddRange(inlineSkills);
            return skills;
        }
    }
}
