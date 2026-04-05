using System;
using System.Collections.Generic;
using MuLike.ContentPipeline.Authoring;
using MuLike.Data.Catalogs;
using MuLike.Shared.Content;

namespace MuLike.ContentPipeline.Runtime
{
    public static class GameContentPipelineBuilder
    {
        public static GameContentBundleDto Build(GameContentPipelineProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var bundle = new GameContentBundleDto
            {
                version = string.IsNullOrWhiteSpace(profile.bundleVersion) ? "1.0.0" : profile.bundleVersion,
                exportedAtUtc = DateTime.UtcNow.ToString("O"),
                items = BuildItems(profile.itemCatalog),
                monsters = BuildMonsters(profile.monsterCatalog),
                skills = BuildSkills(profile.skillCatalog),
                maps = BuildMaps(profile.mapCatalog),
                spawnTables = BuildSpawnTables(profile.spawnTableDatabase),
                dropTables = BuildDropTables(profile.dropTableDatabase),
                balance = BuildBalance(profile.balanceConfig)
            };

            return bundle;
        }

        private static ContentItemDto[] BuildItems(ItemCatalogDatabase source)
        {
            if (source == null)
                return Array.Empty<ContentItemDto>();

            List<ItemDefinition> definitions = source.BuildDefinitions();
            var items = new ContentItemDto[definitions.Count];

            for (int i = 0; i < definitions.Count; i++)
            {
                ItemDefinition item = definitions[i];
                string equipSlot = item.AllowedEquipSlots.Count > 0 ? item.AllowedEquipSlots[0].ToString() : string.Empty;
                string[] equipSlots = new string[item.AllowedEquipSlots.Count];
                for (int j = 0; j < item.AllowedEquipSlots.Count; j++)
                    equipSlots[j] = item.AllowedEquipSlots[j].ToString();

                string[] classRestrictions = new string[item.AllowedClasses.Count];
                for (int j = 0; j < item.AllowedClasses.Count; j++)
                    classRestrictions[j] = item.AllowedClasses[j].ToString();

                items[i] = new ContentItemDto
                {
                    itemId = item.ItemId,
                    name = item.Name,
                    type = item.Type,
                    rarity = (int)item.Rarity + 1,
                    requiredLevel = item.RequiredLevel,
                    isTwoHanded = item.IsTwoHanded,
                    classRestrictions = classRestrictions,
                    isStackable = item.Stackable,
                    maxStack = item.MaxStack,
                    stackRule = (int)item.StackRule,
                    equipSlot = equipSlot,
                    equipSlots = equipSlots,
                    minDamage = item.BasicStats.MinDamage,
                    maxDamage = item.BasicStats.MaxDamage,
                    attackSpeed = item.BasicStats.AttackSpeed,
                    magicPower = item.BasicStats.MagicPower,
                    defense = item.BasicStats.Defense,
                    blockRate = item.BasicStats.BlockRate,
                    moveBonus = item.BasicStats.MoveBonus,
                    bonusAttack = 0,
                    bonusDefense = 0,
                    bonusHp = item.StatBonuses.Hp,
                    bonusAttackRate = item.StatBonuses.AttackRate,
                    bonusMana = item.StatBonuses.Mana,
                    bonusSpellPower = item.StatBonuses.SpellPower,
                    bonusMoveSpeed = item.StatBonuses.MoveSpeed,
                    damageAbsorb = item.StatBonuses.DamageAbsorb,
                    damageBoostPct = item.StatBonuses.DamageBoost,
                    petDamageBonus = item.StatBonuses.PetDamage,
                    petDefenseBonus = item.StatBonuses.PetDefense,
                    autoLoot = item.StatBonuses.AutoLoot,
                    requiredStrength = item.StatRequirements.Strength,
                    requiredAgility = item.StatRequirements.Agility,
                    requiredEnergy = item.StatRequirements.Energy,
                    requiredCommand = item.StatRequirements.Command,
                    allowedExcellentOptions = (int)item.AllowedExcellentOptions,
                    allowSockets = item.AllowSockets,
                    maxSockets = item.MaxSockets,
                    sellValue = item.SellValue
                };
            }

            return items;
        }

        private static ContentMonsterDto[] BuildMonsters(MonsterCatalogDatabase source)
        {
            if (source == null)
                return Array.Empty<ContentMonsterDto>();

            List<MonsterDefinitionRecord> definitions = source.BuildDefinitions();
            var monsters = new ContentMonsterDto[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                MonsterDefinitionRecord monster = definitions[i];
                monsters[i] = new ContentMonsterDto
                {
                    monsterId = monster.monsterId,
                    name = monster.name,
                    level = monster.level,
                    hpMax = monster.hpMax,
                    attack = monster.attack,
                    defense = monster.defense,
                    aggroRadius = monster.aggroRadius,
                    chaseRadius = monster.chaseRadius,
                    leashRadius = monster.leashRadius,
                    attackRange = monster.attackRange,
                    moveSpeed = monster.moveSpeed,
                    respawnSeconds = monster.respawnSeconds,
                    expReward = monster.expReward,
                    dropTableId = monster.dropTableId
                };
            }

            return monsters;
        }

        private static ContentSkillDto[] BuildSkills(SkillCatalogDatabase source)
        {
            if (source == null)
                return Array.Empty<ContentSkillDto>();

            List<SkillDefinitionRecord> definitions = source.BuildDefinitions();
            var skills = new ContentSkillDto[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                SkillDefinitionRecord skill = definitions[i];
                skills[i] = new ContentSkillDto
                {
                    skillId = skill.skillId,
                    name = skill.name,
                    description = skill.description,
                    manaCost = skill.manaCost,
                    cooldownSeconds = skill.cooldownSeconds,
                    castRange = skill.castRange,
                    areaRadius = skill.areaRadius,
                    castType = (int)skill.castType,
                    minLevel = skill.minLevel,
                    baseDamage = skill.baseDamage,
                    attackScale = skill.attackScale,
                    defenseScale = skill.defenseScale
                };
            }

            return skills;
        }

        private static ContentMapDto[] BuildMaps(MapCatalogDatabase source)
        {
            if (source == null)
                return Array.Empty<ContentMapDto>();

            List<MapDefinitionRecord> definitions = source.BuildDefinitions();
            var maps = new ContentMapDto[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                MapDefinitionRecord map = definitions[i];
                maps[i] = new ContentMapDto
                {
                    mapId = map.mapId,
                    mapName = map.mapName,
                    sceneName = map.sceneName,
                    biome = map.biome,
                    recommendedLevel = map.recommendedLevel
                };
            }

            return maps;
        }

        private static ContentSpawnTableDto[] BuildSpawnTables(SpawnTableDatabase source)
        {
            if (source == null || source.tables == null)
                return Array.Empty<ContentSpawnTableDto>();

            var tables = new ContentSpawnTableDto[source.tables.Count];
            for (int i = 0; i < source.tables.Count; i++)
            {
                SpawnTableRecord table = source.tables[i];
                SpawnPointRecord[] records = table.entries ?? Array.Empty<SpawnPointRecord>();
                var entries = new ContentSpawnEntryDto[records.Length];
                for (int j = 0; j < records.Length; j++)
                {
                    entries[j] = new ContentSpawnEntryDto
                    {
                        monsterId = records[j].monsterId,
                        x = records[j].x,
                        y = records[j].y,
                        z = records[j].z,
                        count = records[j].count
                    };
                }

                tables[i] = new ContentSpawnTableDto
                {
                    tableId = table.tableId,
                    mapId = table.mapId,
                    entries = entries
                };
            }

            return tables;
        }

        private static ContentDropTableDto[] BuildDropTables(DropTableDatabase source)
        {
            if (source == null || source.tables == null)
                return Array.Empty<ContentDropTableDto>();

            var tables = new ContentDropTableDto[source.tables.Count];
            for (int i = 0; i < source.tables.Count; i++)
            {
                DropTableRecord table = source.tables[i];
                DropEntryRecord[] records = table.entries ?? Array.Empty<DropEntryRecord>();
                var entries = new ContentDropEntryDto[records.Length];
                for (int j = 0; j < records.Length; j++)
                {
                    entries[j] = new ContentDropEntryDto
                    {
                        itemId = records[j].itemId,
                        chancePercent = records[j].chancePercent,
                        minQuantity = records[j].minQuantity,
                        maxQuantity = records[j].maxQuantity
                    };
                }

                tables[i] = new ContentDropTableDto
                {
                    tableId = table.tableId,
                    entries = entries
                };
            }

            return tables;
        }

        private static ContentBalanceDto BuildBalance(BalanceConfigAsset source)
        {
            if (source == null)
            {
                return new ContentBalanceDto
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

            return new ContentBalanceDto
            {
                damageMultiplier = source.config.damageMultiplier,
                defenseMultiplier = source.config.defenseMultiplier,
                skillDamageMultiplier = source.config.skillDamageMultiplier,
                expMultiplier = source.config.expMultiplier,
                dropRateMultiplier = source.config.dropRateMultiplier,
                zenMultiplier = source.config.zenMultiplier,
                respawnSpeedMultiplier = source.config.respawnSpeedMultiplier,
                eliteSpawnChance = source.config.eliteSpawnChance
            };
        }
    }
}
