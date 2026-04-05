#if UNITY_EDITOR
using System;
using System.IO;
using MuLike.ContentPipeline.Authoring;
using MuLike.ContentPipeline.Runtime;
using MuLike.Data.Catalogs;
using MuLike.Shared.Content;
using UnityEditor;
using UnityEngine;

namespace MuLike.Tools.Editor.ContentPipeline
{
    public static class GameContentPipelineEditorTools
    {
        [MenuItem("MuLike/Content Pipeline/Export Selected Profile")]
        public static void ExportSelectedProfile()
        {
            if (Selection.activeObject is not GameContentPipelineProfile profile)
            {
                EditorUtility.DisplayDialog("Content Pipeline", "Select a GameContentPipelineProfile asset first.", "OK");
                return;
            }

            ExportProfile(profile);
        }

        [MenuItem("MuLike/Content Pipeline/Create Sample Profile + Export")]
        public static void CreateSampleProfileAndExport()
        {
            const string root = "Assets/_Project/ContentPipeline/Sample";
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/ContentPipeline");
            EnsureFolder(root);

            var itemCatalog = ScriptableObject.CreateInstance<ItemCatalogDatabase>();
            itemCatalog.inlineItems.Add(new ItemDefinitionRecord
            {
                itemId = 3001,
                name = "Short Sword",
                category = ItemCategory.Weapon,
                type = "weapon",
                subtype = "sword",
                family = "swords",
                rarity = ItemRarity.Uncommon,
                level = 1,
                requiredLevel = 1,
                stackable = false,
                maxStack = 1,
                stackRule = ItemStackRule.None,
                twoHanded = false,
                basicStats = new ItemBasicStats { MinDamage = 4, MaxDamage = 8, AttackSpeed = 10 },
                allowedEquipSlots = new[] { ItemEquipSlot.WeaponMain },
                allowedClasses = new[] { CharacterClassRestriction.Warrior, CharacterClassRestriction.DarkLord },
                allowSockets = true,
                maxSockets = 2,
                allowedExcellentOptions = ExcellentOptionFlags.BonusDamage,
                sellValue = 300
            });
            AssetDatabase.CreateAsset(itemCatalog, root + "/Sample_ItemCatalog.asset");

            var monsterCatalog = ScriptableObject.CreateInstance<MonsterCatalogDatabase>();
            monsterCatalog.inlineMonsters.Add(new MonsterDefinitionRecord
            {
                monsterId = 101,
                name = "Spider",
                level = 1,
                hpMax = 65,
                attack = 11,
                defense = 4,
                aggroRadius = 9f,
                chaseRadius = 18f,
                leashRadius = 22f,
                attackRange = 2f,
                moveSpeed = 2.8f,
                respawnSeconds = 8f,
                expReward = 35,
                dropTableId = "starter_spider"
            });
            AssetDatabase.CreateAsset(monsterCatalog, root + "/Sample_MonsterCatalog.asset");

            var skillCatalog = ScriptableObject.CreateInstance<SkillCatalogDatabase>();
            skillCatalog.inlineSkills.Add(new SkillDefinitionRecord
            {
                skillId = 1,
                name = "Slash",
                description = "Basic melee attack",
                manaCost = 10,
                cooldownSeconds = 1.5f,
                castRange = 2f,
                areaRadius = 0f,
                castType = SkillCastTypeAuthoring.SingleTarget,
                minLevel = 1,
                baseDamage = 2,
                attackScale = 1f,
                defenseScale = 0.5f
            });
            AssetDatabase.CreateAsset(skillCatalog, root + "/Sample_SkillCatalog.asset");

            var mapCatalog = ScriptableObject.CreateInstance<MapCatalogDatabase>();
            mapCatalog.inlineMaps.Add(new MapDefinitionRecord
            {
                mapId = 1,
                mapName = "World_Dev",
                sceneName = "World_Dev",
                biome = "Forest",
                recommendedLevel = 1
            });
            AssetDatabase.CreateAsset(mapCatalog, root + "/Sample_MapCatalog.asset");

            var dropTable = ScriptableObject.CreateInstance<DropTableDatabase>();
            dropTable.tables.Add(new DropTableRecord
            {
                tableId = "starter_spider",
                entries = new[]
                {
                    new DropEntryRecord { itemId = 3001, chancePercent = 10, minQuantity = 1, maxQuantity = 1 },
                    new DropEntryRecord { itemId = 1001, chancePercent = 35, minQuantity = 1, maxQuantity = 2 }
                }
            });
            AssetDatabase.CreateAsset(dropTable, root + "/Sample_DropTables.asset");

            var spawnTable = ScriptableObject.CreateInstance<SpawnTableDatabase>();
            spawnTable.tables.Add(new SpawnTableRecord
            {
                tableId = "world_dev_starter",
                mapId = 1,
                entries = new[]
                {
                    new SpawnPointRecord { monsterId = 101, x = -4f, y = 0f, z = 6f, count = 1 },
                    new SpawnPointRecord { monsterId = 101, x = 3f, y = 0f, z = 8f, count = 1 }
                }
            });
            AssetDatabase.CreateAsset(spawnTable, root + "/Sample_SpawnTables.asset");

            var balance = ScriptableObject.CreateInstance<BalanceConfigAsset>();
            balance.config.damageMultiplier = 1f;
            balance.config.defenseMultiplier = 1f;
            balance.config.skillDamageMultiplier = 1f;
            balance.config.expMultiplier = 1f;
            balance.config.dropRateMultiplier = 1f;
            balance.config.zenMultiplier = 1f;
            balance.config.respawnSpeedMultiplier = 1f;
            balance.config.eliteSpawnChance = 0.02f;
            AssetDatabase.CreateAsset(balance, root + "/Sample_Balance.asset");

            var profile = ScriptableObject.CreateInstance<GameContentPipelineProfile>();
            profile.itemCatalog = itemCatalog;
            profile.monsterCatalog = monsterCatalog;
            profile.skillCatalog = skillCatalog;
            profile.mapCatalog = mapCatalog;
            profile.spawnTableDatabase = spawnTable;
            profile.dropTableDatabase = dropTable;
            profile.balanceConfig = balance;
            profile.bundleVersion = "sample-1";
            profile.outputAssetPath = "Assets/Resources/Data/Content/server_content_bundle.json";
            profile.resourcePath = "Data/Content/server_content_bundle";
            AssetDatabase.CreateAsset(profile, root + "/Sample_ContentPipelineProfile.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ExportProfile(profile);
            Selection.activeObject = profile;
        }

        private static void ExportProfile(GameContentPipelineProfile profile)
        {
            GameContentBundleDto bundle = GameContentPipelineBuilder.Build(profile);
            string json = JsonUtility.ToJson(bundle, true);

            string assetPath = string.IsNullOrWhiteSpace(profile.outputAssetPath)
                ? "Assets/Resources/Data/Content/server_content_bundle.json"
                : profile.outputAssetPath;

            string absolutePath = ToAbsolutePath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[ContentPipeline] Exported content bundle: {assetPath}");
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string relative = assetPath.Replace("\\", "/").Trim();
            if (Path.IsPathRooted(relative))
                return relative;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, relative);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
