using System;
using System.Collections.Generic;
using MuLike.Server.Game.Definitions;
using MuLike.Server.Game.Skills;
using MuLike.Server.Game.World;
using MuLike.Shared.Content;

namespace MuLike.Server.Infrastructure.ContentPipeline
{
    public static class ServerContentPipelineImporter
    {
        private const string DefaultResourcePath = "Data/Content/server_content_bundle";

        public static bool TryApplyFromResources(
            ItemDatabase itemDatabase,
            SkillDatabase skillDatabase,
            WorldManager worldManager,
            SpawnManager spawnManager,
            string resourcePath = DefaultResourcePath)
        {
            if (!GameContentBundleLoader.TryLoadFromResources(resourcePath, out GameContentBundleDto bundle) || bundle == null)
                return false;

            Apply(itemDatabase, skillDatabase, worldManager, spawnManager, bundle);
            return true;
        }

        public static void Apply(
            ItemDatabase itemDatabase,
            SkillDatabase skillDatabase,
            WorldManager worldManager,
            SpawnManager spawnManager,
            GameContentBundleDto bundle)
        {
            if (itemDatabase == null) throw new ArgumentNullException(nameof(itemDatabase));
            if (skillDatabase == null) throw new ArgumentNullException(nameof(skillDatabase));
            if (worldManager == null) throw new ArgumentNullException(nameof(worldManager));
            if (spawnManager == null) throw new ArgumentNullException(nameof(spawnManager));
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            ApplyItems(itemDatabase, bundle.items);
            ApplySkills(skillDatabase, bundle.skills);
            ApplyMaps(worldManager, bundle.maps);
            ApplyBalance(bundle.balance);
            ApplySpawns(worldManager, spawnManager, bundle.monsters, bundle.dropTables, bundle.spawnTables);

            UnityEngine.Debug.Log($"[ServerContentPipelineImporter] Bundle applied. version={bundle.version}");
        }

        private static void ApplyItems(ItemDatabase itemDatabase, ContentItemDto[] items)
        {
            var list = new List<ItemDefinition>();
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    ContentItemDto source = items[i];
                    if (source == null)
                        continue;

                    list.Add(new ItemDefinition
                    {
                        ItemId = source.itemId,
                        Name = source.name,
                        Type = source.type,
                        Rarity = source.rarity,
                        RequiredLevel = source.requiredLevel,
                        IsTwoHanded = source.isTwoHanded,
                        ClassRestrictions = source.classRestrictions ?? Array.Empty<string>(),
                        IsStackable = source.isStackable,
                        MaxStack = source.maxStack,
                        StackRule = (ItemStackRule)source.stackRule,
                        EquipSlot = source.equipSlot,
                        EquipSlots = source.equipSlots ?? Array.Empty<string>(),
                        MinDamage = source.minDamage,
                        MaxDamage = source.maxDamage,
                        AttackSpeed = source.attackSpeed,
                        MagicPower = source.magicPower,
                        Defense = source.defense,
                        BlockRate = source.blockRate,
                        MoveBonus = source.moveBonus,
                        BonusAttack = source.bonusAttack,
                        BonusDefense = source.bonusDefense,
                        BonusHp = source.bonusHp,
                        BonusAttackRate = source.bonusAttackRate,
                        BonusMana = source.bonusMana,
                        BonusSpellPower = source.bonusSpellPower,
                        BonusMoveSpeed = source.bonusMoveSpeed,
                        DamageAbsorb = source.damageAbsorb,
                        DamageBoostPct = source.damageBoostPct,
                        PetDamageBonus = source.petDamageBonus,
                        PetDefenseBonus = source.petDefenseBonus,
                        AutoLoot = source.autoLoot,
                        RequiredStrength = source.requiredStrength,
                        RequiredAgility = source.requiredAgility,
                        RequiredEnergy = source.requiredEnergy,
                        RequiredCommand = source.requiredCommand,
                        AllowedExcellentOptions = (ExcellentOptionFlags)source.allowedExcellentOptions,
                        AllowSockets = source.allowSockets,
                        MaxSockets = source.maxSockets,
                        SellValue = source.sellValue
                    });
                }
            }

            if (list.Count > 0)
                itemDatabase.Populate(list);
        }

        private static void ApplySkills(SkillDatabase skillDatabase, ContentSkillDto[] skills)
        {
            var list = new List<SkillDefinition>();
            if (skills != null)
            {
                for (int i = 0; i < skills.Length; i++)
                {
                    ContentSkillDto source = skills[i];
                    if (source == null)
                        continue;

                    var def = new SkillDefinition(
                        source.skillId,
                        source.name,
                        source.description,
                        source.manaCost,
                        source.cooldownSeconds,
                        source.castRange,
                        (SkillCastType)source.castType,
                        source.minLevel,
                        source.areaRadius);

                    int baseDamage = source.baseDamage;
                    float attackScale = source.attackScale;
                    float defenseScale = source.defenseScale;
                    def.SetDamageFormula((attack, defense) =>
                    {
                        int raw = baseDamage + (int)(attack * attackScale) - (int)(defense * defenseScale);
                        return Math.Max(1, raw);
                    });

                    list.Add(def);
                }
            }

            if (list.Count > 0)
                skillDatabase.Populate(list);
        }

        private static void ApplyMaps(WorldManager worldManager, ContentMapDto[] maps)
        {
            if (maps == null)
                return;

            for (int i = 0; i < maps.Length; i++)
            {
                ContentMapDto map = maps[i];
                if (map == null)
                    continue;

                worldManager.RegisterMap(new MapInstance(map.mapId, map.mapName));
            }
        }

        private static void ApplyBalance(ContentBalanceDto balance)
        {
            ServerBalanceRuntimeConfig.Apply(balance);
        }

        private static void ApplySpawns(
            WorldManager worldManager,
            SpawnManager spawnManager,
            ContentMonsterDto[] monsters,
            ContentDropTableDto[] dropTables,
            ContentSpawnTableDto[] spawnTables)
        {
            if (spawnTables == null || monsters == null)
                return;

            Dictionary<string, ContentDropTableDto> dropsById = BuildDropTableIndex(dropTables);
            Dictionary<int, ContentMonsterDto> monstersById = BuildMonsterIndex(monsters);

            for (int i = 0; i < spawnTables.Length; i++)
            {
                ContentSpawnTableDto table = spawnTables[i];
                if (table == null || table.entries == null)
                    continue;

                if (!worldManager.TryGetMap(table.mapId, out _))
                    continue;

                for (int j = 0; j < table.entries.Length; j++)
                {
                    ContentSpawnEntryDto spawn = table.entries[j];
                    if (spawn == null)
                        continue;

                    if (!monstersById.TryGetValue(spawn.monsterId, out ContentMonsterDto monsterDto))
                        continue;

                    MonsterDropDefinition[] drops = ResolveDrops(monsterDto.dropTableId, dropsById);
                    var monsterDef = new MonsterDefinition
                    {
                        MonsterId = monsterDto.monsterId,
                        Name = monsterDto.name,
                        Level = monsterDto.level,
                        HpMax = monsterDto.hpMax,
                        Attack = monsterDto.attack,
                        Defense = monsterDto.defense,
                        AggroRadius = monsterDto.aggroRadius,
                        ChaseRadius = monsterDto.chaseRadius,
                        LeashRadius = monsterDto.leashRadius,
                        AttackRange = monsterDto.attackRange,
                        MoveSpeed = monsterDto.moveSpeed,
                        RespawnSeconds = monsterDto.respawnSeconds,
                        ExpReward = monsterDto.expReward,
                        Drops = drops
                    };

                    int count = spawn.count <= 0 ? 1 : spawn.count;
                    for (int k = 0; k < count; k++)
                        spawnManager.RegisterMonsterSpawnPoint(table.mapId, monsterDef, spawn.x, spawn.y, spawn.z);
                }
            }

            spawnManager.SpawnInitialMonsters(worldManager);
        }

        private static Dictionary<string, ContentDropTableDto> BuildDropTableIndex(ContentDropTableDto[] dropTables)
        {
            var index = new Dictionary<string, ContentDropTableDto>(StringComparer.OrdinalIgnoreCase);
            if (dropTables == null)
                return index;

            for (int i = 0; i < dropTables.Length; i++)
            {
                ContentDropTableDto table = dropTables[i];
                if (table == null || string.IsNullOrWhiteSpace(table.tableId))
                    continue;

                index[table.tableId] = table;
            }

            return index;
        }

        private static Dictionary<int, ContentMonsterDto> BuildMonsterIndex(ContentMonsterDto[] monsters)
        {
            var index = new Dictionary<int, ContentMonsterDto>();
            for (int i = 0; i < monsters.Length; i++)
            {
                ContentMonsterDto monster = monsters[i];
                if (monster == null)
                    continue;

                index[monster.monsterId] = monster;
            }

            return index;
        }

        private static MonsterDropDefinition[] ResolveDrops(string dropTableId, Dictionary<string, ContentDropTableDto> dropsById)
        {
            if (string.IsNullOrWhiteSpace(dropTableId) || !dropsById.TryGetValue(dropTableId, out ContentDropTableDto table) || table.entries == null)
                return Array.Empty<MonsterDropDefinition>();

            var list = new List<MonsterDropDefinition>(table.entries.Length);
            for (int i = 0; i < table.entries.Length; i++)
            {
                ContentDropEntryDto entry = table.entries[i];
                if (entry == null)
                    continue;

                list.Add(new MonsterDropDefinition
                {
                    ItemId = entry.itemId,
                    ChancePercent = entry.chancePercent,
                    MinQuantity = entry.minQuantity,
                    MaxQuantity = entry.maxQuantity
                });
            }

            return list.ToArray();
        }
    }
}
