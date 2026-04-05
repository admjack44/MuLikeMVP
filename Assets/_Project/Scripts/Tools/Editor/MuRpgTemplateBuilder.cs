using System;
using System.Collections.Generic;
using MuLike.Classes;
using MuLike.Combat;
using MuLike.Skills;
using MuLike.VFX;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Builds data-driven templates for MU-style mobile classes/skills/evolutions/combos.
    ///
    /// Menu:
    /// MuLike/Build/Generate MU RPG Class Templates
    ///
    /// Output:
    /// - Assets/_Project/ScriptableObjects/Classes/*.asset
    /// - Assets/_Project/ScriptableObjects/Skills/*.asset
    /// - Assets/_Project/ScriptableObjects/Combat/*.asset
    /// - Assets/_Project/ScriptableObjects/VFX/*.asset
    /// - Assets/_Project/Animations/Controllers/*.controller
    /// </summary>
    public static class MuRpgTemplateBuilder
    {
        private const string ClassesDir = "Assets/_Project/ScriptableObjects/Classes";
        private const string SkillsDir = "Assets/_Project/ScriptableObjects/Skills";
        private const string CombatDir = "Assets/_Project/ScriptableObjects/Combat";
        private const string VfxDir = "Assets/_Project/ScriptableObjects/VFX";
        private const string ControllersDir = "Assets/_Project/Animations/Controllers";

        [MenuItem("MuLike/Build/Generate MU RPG Class Templates")]
        public static void Generate()
        {
            EnsureDir("Assets/_Project/ScriptableObjects");
            EnsureDir(ClassesDir);
            EnsureDir(SkillsDir);
            EnsureDir(CombatDir);
            EnsureDir(VfxDir);
            EnsureDir("Assets/_Project/Animations");
            EnsureDir(ControllersDir);

            CombatTuningProfile tuning = CreateOrLoad<CombatTuningProfile>($"{CombatDir}/CombatTuning_Mobile.asset");
            tuning.mobileCastTimeMultiplier = 0.7f;
            tuning.mobileMeleeRangeMultiplier = 1.15f;
            tuning.comboWindowMultiplier = 1.2f;
            tuning.shortTelegraphSeconds = 0.12f;
            tuning.allowHeavyParticlesOnMidRange = false;
            tuning.enableFxPooling = true;
            EditorUtility.SetDirty(tuning);

            VfxLodProfile lod = CreateOrLoad<VfxLodProfile>($"{VfxDir}/VfxLodProfile_Mobile.asset");
            lod.highDistance = 8f;
            lod.midDistance = 18f;
            lod.disableHeavyParticlesOnMidRange = true;
            lod.warmupPerPrefab = 2;
            lod.maxPoolPerPrefab = 24;
            EditorUtility.SetDirty(lod);

            SkillCooldownGroup groupMelee = CreateCooldownGroup("CDG_Melee", "melee", 0.2f);
            SkillCooldownGroup groupMagic = CreateCooldownGroup("CDG_Magic", "magic", 0.25f);
            SkillCooldownGroup groupSupport = CreateCooldownGroup("CDG_Support", "support", 0.6f);
            SkillCooldownGroup groupUltimate = CreateCooldownGroup("CDG_Ultimate", "ultimate", 1.5f);

            Dictionary<string, SkillDefinition> skills = new();

            // Dark Wizard
            BuildClassSkills(skills, "DW", groupMagic, groupUltimate,
                ("Basic Arcane Bolt", SkillTag.Ranged | SkillTag.Magic, 5f, 0.5f, 0f),
                ("Fireball", SkillTag.Ranged | SkillTag.Magic | SkillTag.ComboStarter, 7f, 2f, 8f),
                ("Lightning", SkillTag.Ranged | SkillTag.Magic | SkillTag.ComboLinker, 7.5f, 2.4f, 10f),
                ("Ice", SkillTag.Ranged | SkillTag.Magic | SkillTag.ComboLinker, 7f, 2.8f, 11f),
                ("Meteor Storm", SkillTag.Magic | SkillTag.Ultimate | SkillTag.AreaTarget(), 9f, 14f, 28f));

            // Dark Knight
            BuildClassSkills(skills, "DK", groupMelee, groupUltimate,
                ("Basic Slash", SkillTag.Melee, 2.8f, 0.55f, 0f),
                ("Slash", SkillTag.Melee | SkillTag.ComboStarter, 3f, 1.8f, 5f),
                ("Charge Strike", SkillTag.Melee | SkillTag.ComboLinker, 3.2f, 2.6f, 8f),
                ("Twisting Cut", SkillTag.Melee | SkillTag.ComboLinker, 3.3f, 3.2f, 10f),
                ("Earth Breaker", SkillTag.Melee | SkillTag.Ultimate, 4f, 15f, 20f));

            // Elf
            BuildClassSkills(skills, "ELF", groupSupport, groupUltimate,
                ("Basic Arrow", SkillTag.Ranged, 6f, 0.6f, 0f),
                ("Triple Shot", SkillTag.Ranged | SkillTag.ComboStarter, 7f, 2.2f, 7f),
                ("Heal", SkillTag.Support, 6f, 4f, 12f),
                ("Blessing Aura", SkillTag.Support, 0f, 7f, 16f),
                ("Spirit Rain", SkillTag.Ranged | SkillTag.Ultimate | SkillTag.Magic, 8f, 15f, 24f));

            // Magic Gladiator
            BuildClassSkills(skills, "MG", groupMelee, groupUltimate,
                ("Basic Hybrid Slash", SkillTag.Melee, 3f, 0.55f, 0f),
                ("Flame Slash", SkillTag.Melee | SkillTag.Magic | SkillTag.ComboStarter, 3.6f, 2.1f, 8f),
                ("Power Wave", SkillTag.Ranged | SkillTag.Magic | SkillTag.ComboLinker, 6f, 2.9f, 10f),
                ("Cyclone Thrust", SkillTag.Melee | SkillTag.ComboLinker, 3.5f, 3.1f, 11f),
                ("Nova Crash", SkillTag.Magic | SkillTag.Ultimate, 7f, 15f, 24f));

            // Dark Lord
            BuildClassSkills(skills, "DL", groupSupport, groupUltimate,
                ("Basic Scepter Strike", SkillTag.Melee, 2.8f, 0.55f, 0f),
                ("Dark Raven Command", SkillTag.Summon | SkillTag.Support, 8f, 4f, 12f),
                ("Fire Burst", SkillTag.Magic | SkillTag.Ranged, 7f, 2.8f, 10f),
                ("Leadership Aura", SkillTag.Support, 0f, 8f, 18f),
                ("Chaos Dominion", SkillTag.Ultimate | SkillTag.Magic | SkillTag.Support, 9f, 16f, 28f));

            // Slayer
            BuildClassSkills(skills, "SLAYER", groupMelee, groupUltimate,
                ("Basic Fang", SkillTag.Melee, 2.9f, 0.52f, 0f),
                ("Blood Slice", SkillTag.Melee | SkillTag.ComboStarter, 3.2f, 1.9f, 6f),
                ("Shadow Rush", SkillTag.Melee | SkillTag.Ranged | SkillTag.ComboLinker, 4.8f, 2.3f, 8f),
                ("Bleeding Fang", SkillTag.Melee | SkillTag.ComboLinker, 3.2f, 3.0f, 9f),
                ("Crimson Requiem", SkillTag.Ultimate | SkillTag.Melee, 4f, 14f, 23f));

            // Rage Fighter
            BuildClassSkills(skills, "RF", groupMelee, groupUltimate,
                ("Basic Jab", SkillTag.Melee, 2.7f, 0.48f, 0f),
                ("Chain Punch", SkillTag.Melee | SkillTag.ComboStarter, 2.9f, 1.8f, 5f),
                ("Dragon Roar", SkillTag.Melee | SkillTag.ComboLinker, 3.4f, 2.5f, 7f),
                ("Phoenix Uppercut", SkillTag.Melee | SkillTag.ComboLinker, 3.2f, 2.9f, 8f),
                ("Rage Tempest", SkillTag.Melee | SkillTag.Ultimate, 4f, 13f, 20f));

            // Illusion Knight
            BuildClassSkills(skills, "IK", groupMagic, groupUltimate,
                ("Basic Spectral Cut", SkillTag.Melee | SkillTag.Magic, 3f, 0.52f, 0f),
                ("Spectral Slash", SkillTag.Melee | SkillTag.Magic | SkillTag.ComboStarter, 3.4f, 2f, 7f),
                ("Mirage Orb", SkillTag.Ranged | SkillTag.Magic | SkillTag.ComboLinker, 6.5f, 2.4f, 9f),
                ("Phantom Bind", SkillTag.Magic | SkillTag.ComboLinker | SkillTag.Support, 6f, 3.2f, 10f),
                ("Illusion Eclipse", SkillTag.Magic | SkillTag.Ultimate, 7f, 15f, 25f));

            // Controllers
            RuntimeAnimatorController dw = CreateController("DW");
            RuntimeAnimatorController dk = CreateController("DK");
            RuntimeAnimatorController elf = CreateController("ELF");
            RuntimeAnimatorController mg = CreateController("MG");
            RuntimeAnimatorController dl = CreateController("DL");
            RuntimeAnimatorController slayer = CreateController("SLAYER");
            RuntimeAnimatorController rf = CreateController("RF");
            RuntimeAnimatorController ik = CreateController("IK");

            // Classes + evolutions
            CreateClassDefinition(MuClassId.DarkWizard, "Dark Wizard", MuClassArchetype.Caster, ClassUnlockRequirement.None(), dw,
                skills["DW_BASIC"], new[] { skills["DW_FIREBALL"], skills["DW_LIGHTNING"], skills["DW_ICE"] }, skills["DW_METEOR"]);

            CreateClassDefinition(MuClassId.DarkKnight, "Dark Knight", MuClassArchetype.Frontliner, ClassUnlockRequirement.None(), dk,
                skills["DK_BASIC"], new[] { skills["DK_SLASH"], skills["DK_CHARGE"], skills["DK_TWIST"] }, skills["DK_EARTH"]);

            CreateClassDefinition(MuClassId.Elf, "Elf", MuClassArchetype.Support, ClassUnlockRequirement.None(), elf,
                skills["ELF_BASIC"], new[] { skills["ELF_TRIPLE"], skills["ELF_HEAL"], skills["ELF_BLESS"] }, skills["ELF_SPIRIT"]);

            CreateClassDefinition(MuClassId.MagicGladiator, "Magic Gladiator", MuClassArchetype.Hybrid, ClassUnlockRequirement.None(), mg,
                skills["MG_BASIC"], new[] { skills["MG_FLAME"], skills["MG_WAVE"], skills["MG_CYCLONE"] }, skills["MG_NOVA"]);

            CreateClassDefinition(MuClassId.DarkLord, "Dark Lord", MuClassArchetype.Hybrid, ClassUnlockRequirement.None(), dl,
                skills["DL_BASIC"], new[] { skills["DL_RAVEN"], skills["DL_FIRE"], skills["DL_AURA"] }, skills["DL_CHAOS"]);

            CreateClassDefinition(MuClassId.Slayer, "Slayer", MuClassArchetype.Assassin, ClassUnlockRequirement.None(), slayer,
                skills["SLAYER_BASIC"], new[] { skills["SLAYER_BLOOD"], skills["SLAYER_SHADOW"], skills["SLAYER_BLEED"] }, skills["SLAYER_CRIMSON"]);

            CreateClassDefinition(MuClassId.RageFighter, "Rage Fighter", MuClassArchetype.Brawler, ClassUnlockRequirement.None(), rf,
                skills["RF_BASIC"], new[] { skills["RF_CHAIN"], skills["RF_ROAR"], skills["RF_UPPERCUT"] }, skills["RF_TEMPEST"]);

            CreateClassDefinition(MuClassId.IllusionKnight, "Illusion Knight", MuClassArchetype.SpectralHybrid, ClassUnlockRequirement.None(), ik,
                skills["IK_BASIC"], new[] { skills["IK_SPECTRAL"], skills["IK_MIRAGE"], skills["IK_BIND"] }, skills["IK_ECLIPSE"]);

            // Combos
            CreateCombo("Combo_DK", MuClassId.DarkKnight, new[] { skills["DK_SLASH"], skills["DK_TWIST"], skills["DK_CHARGE"] }, 1.35f);
            CreateCombo("Combo_DW", MuClassId.DarkWizard, new[] { skills["DW_FIREBALL"], skills["DW_LIGHTNING"], skills["DW_ICE"] }, 1.30f);
            CreateCombo("Combo_SLAYER", MuClassId.Slayer, new[] { skills["SLAYER_BLOOD"], skills["SLAYER_BLEED"], skills["SLAYER_SHADOW"] }, 1.33f);
            CreateCombo("Combo_RF", MuClassId.RageFighter, new[] { skills["RF_CHAIN"], skills["RF_ROAR"], skills["RF_UPPERCUT"] }, 1.35f);
            CreateCombo("Combo_IK", MuClassId.IllusionKnight, new[] { skills["IK_SPECTRAL"], skills["IK_MIRAGE"], skills["IK_BIND"] }, 1.32f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MuRpgTemplateBuilder] Templates generated successfully.");
        }

        [MenuItem("MuLike/Build/Unlock All Classes From Base")]
        public static void UnlockAllClassesFromBase()
        {
            EnsureDir(ClassesDir);
            string[] guids = AssetDatabase.FindAssets("t:MuClassDefinition", new[] { ClassesDir });
            int changed = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MuClassDefinition def = AssetDatabase.LoadAssetAtPath<MuClassDefinition>(path);
                if (def == null)
                    continue;

                def.unlockRequirement = ClassUnlockRequirement.None();
                EditorUtility.SetDirty(def);
                changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MuRpgTemplateBuilder] Unlock-all applied to {changed} class assets.");
        }

        private static void BuildClassSkills(
            Dictionary<string, SkillDefinition> map,
            string prefix,
            SkillCooldownGroup coreGroup,
            SkillCooldownGroup ultimateGroup,
            (string n, SkillTag t, float r, float cd, float mana) basic,
            (string n, SkillTag t, float r, float cd, float mana) a1,
            (string n, SkillTag t, float r, float cd, float mana) a2,
            (string n, SkillTag t, float r, float cd, float mana) a3,
            (string n, SkillTag t, float r, float cd, float mana) ult)
        {
            map[$"{prefix}_BASIC"] = CreateSkill($"{prefix}_Basic", basic.n, basic.t, SkillTargetType.SingleTarget, basic.r, basic.cd, basic.mana, coreGroup, false);
            map[$"{prefix}_{Token(a1.n)}"] = CreateSkill($"{prefix}_{Token(a1.n)}", a1.n, a1.t, SkillTargetType.SingleTarget, a1.r, a1.cd, a1.mana, coreGroup, false);
            map[$"{prefix}_{Token(a2.n)}"] = CreateSkill($"{prefix}_{Token(a2.n)}", a2.n, a2.t, SkillTargetType.SingleTarget, a2.r, a2.cd, a2.mana, coreGroup, false);
            map[$"{prefix}_{Token(a3.n)}"] = CreateSkill($"{prefix}_{Token(a3.n)}", a3.n, a3.t, SkillTargetType.SingleTarget, a3.r, a3.cd, a3.mana, coreGroup, false);
            map[$"{prefix}_{Token(ult.n)}"] = CreateSkill($"{prefix}_{Token(ult.n)}", ult.n, ult.t | SkillTag.Ultimate, SkillTargetType.AreaTarget, ult.r, ult.cd, ult.mana, ultimateGroup, true);

            // Normalized aliases for required examples
            Alias(map, prefix, a1.n);
            Alias(map, prefix, a2.n);
            Alias(map, prefix, a3.n);
            Alias(map, prefix, ult.n);
        }

        private static void Alias(Dictionary<string, SkillDefinition> map, string prefix, string skillName)
        {
            string key = $"{prefix}_{Token(skillName)}";
            if (!map.TryGetValue(key, out SkillDefinition value))
                return;

            string alias = prefix switch
            {
                "DW" when skillName == "Fireball" => "DW_FIREBALL",
                "DW" when skillName == "Lightning" => "DW_LIGHTNING",
                "DW" when skillName == "Ice" => "DW_ICE",
                "DW" when skillName == "Meteor Storm" => "DW_METEOR",

                "DK" when skillName == "Slash" => "DK_SLASH",
                "DK" when skillName == "Charge Strike" => "DK_CHARGE",
                "DK" when skillName == "Twisting Cut" => "DK_TWIST",
                "DK" when skillName == "Earth Breaker" => "DK_EARTH",

                "ELF" when skillName == "Triple Shot" => "ELF_TRIPLE",
                "ELF" when skillName == "Heal" => "ELF_HEAL",
                "ELF" when skillName == "Blessing Aura" => "ELF_BLESS",
                "ELF" when skillName == "Spirit Rain" => "ELF_SPIRIT",

                "MG" when skillName == "Flame Slash" => "MG_FLAME",
                "MG" when skillName == "Power Wave" => "MG_WAVE",
                "MG" when skillName == "Cyclone Thrust" => "MG_CYCLONE",
                "MG" when skillName == "Nova Crash" => "MG_NOVA",

                "DL" when skillName == "Dark Raven Command" => "DL_RAVEN",
                "DL" when skillName == "Fire Burst" => "DL_FIRE",
                "DL" when skillName == "Leadership Aura" => "DL_AURA",
                "DL" when skillName == "Chaos Dominion" => "DL_CHAOS",

                "SLAYER" when skillName == "Blood Slice" => "SLAYER_BLOOD",
                "SLAYER" when skillName == "Shadow Rush" => "SLAYER_SHADOW",
                "SLAYER" when skillName == "Bleeding Fang" => "SLAYER_BLEED",
                "SLAYER" when skillName == "Crimson Requiem" => "SLAYER_CRIMSON",

                "RF" when skillName == "Chain Punch" => "RF_CHAIN",
                "RF" when skillName == "Dragon Roar" => "RF_ROAR",
                "RF" when skillName == "Phoenix Uppercut" => "RF_UPPERCUT",
                "RF" when skillName == "Rage Tempest" => "RF_TEMPEST",

                "IK" when skillName == "Spectral Slash" => "IK_SPECTRAL",
                "IK" when skillName == "Mirage Orb" => "IK_MIRAGE",
                "IK" when skillName == "Phantom Bind" => "IK_BIND",
                "IK" when skillName == "Illusion Eclipse" => "IK_ECLIPSE",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(alias))
                map[alias] = value;
        }

        private static SkillDefinition CreateSkill(string assetName, string displayName, SkillTag tags, SkillTargetType targetType, float range, float cooldown, float mana, SkillCooldownGroup group, bool ultimate)
        {
            string path = $"{SkillsDir}/{assetName}.asset";
            SkillDefinition s = CreateOrLoad<SkillDefinition>(path);
            s.skillId = StableId(assetName);
            s.displayName = displayName;
            s.description = displayName;
            s.tags = tags;
            s.targetType = targetType;
            s.baseRange = range;
            s.baseCooldown = cooldown;
            s.baseCastTime = ultimate ? 0.35f : 0.08f;
            s.lockMovementDuringCast = ultimate;
            s.telegraphSeconds = ultimate ? 0.16f : 0.1f;
            s.cost = new SkillResourceCost { mana = Mathf.RoundToInt(mana), stamina = ultimate ? 8 : 0 };
            s.sharedCooldownGroup = group;
            s.animatorState = ultimate ? "Ultimate" : "Cast";
            s.upperBodyLayer = 1;
            s.hitFrameTime = ultimate ? 0.2f : 0.1f;
            EditorUtility.SetDirty(s);
            return s;
        }

        private static SkillCooldownGroup CreateCooldownGroup(string assetName, string id, float sec)
        {
            SkillCooldownGroup g = CreateOrLoad<SkillCooldownGroup>($"{SkillsDir}/{assetName}.asset");
            g.groupId = id;
            g.sharedCooldownSeconds = sec;
            EditorUtility.SetDirty(g);
            return g;
        }

        private static MuClassDefinition CreateClassDefinition(MuClassId id, string name, MuClassArchetype archetype, ClassUnlockRequirement unlock, RuntimeAnimatorController controller, SkillDefinition basic, SkillDefinition[] actives, SkillDefinition ultimate)
        {
            MuClassDefinition c = CreateOrLoad<MuClassDefinition>($"{ClassesDir}/Class_{id}.asset");
            c.classId = id;
            c.displayName = name;
            c.archetype = archetype;
            c.description = name;
            c.unlockRequirement = unlock;
            c.defaultAnimatorController = controller;
            c.basicAttack = basic;
            c.activeSkills = actives;
            c.ultimateSkill = ultimate;
            c.optionalPassiveSkills = Array.Empty<SkillDefinition>();

            c.baseStats = new MuClassBaseStats
            {
                hp = archetype == MuClassArchetype.Frontliner ? 170 : 120,
                mana = archetype == MuClassArchetype.Caster ? 180 : 110,
                stamina = 100,
                command = id == MuClassId.DarkLord ? 80 : 20,
                energy = archetype == MuClassArchetype.Caster ? 120 : 60,
                damageMin = 12,
                damageMax = 20,
                defense = archetype == MuClassArchetype.Frontliner ? 18 : 9,
                moveSpeed = 5f
            };

            c.evolutions = BuildDefaultEvolutionTrack(id, actives, ultimate);
            EditorUtility.SetDirty(c);
            return c;
        }

        private static List<MuClassEvolutionData> BuildDefaultEvolutionTrack(MuClassId id, SkillDefinition[] actives, SkillDefinition ultimate)
        {
            List<int> allActiveIds = new();
            for (int i = 0; i < actives.Length; i++)
            {
                if (actives[i] != null)
                    allActiveIds.Add(actives[i].skillId);
            }
            if (ultimate != null)
                allActiveIds.Add(ultimate.skillId);

            List<MuClassEvolutionData> list = new(6);
            for (int tier = 0; tier <= 5; tier++)
            {
                list.Add(new MuClassEvolutionData
                {
                    id = $"{id}_T{tier}",
                    displayName = tier switch
                    {
                        0 => "Base",
                        1 => "First Evolution",
                        2 => "Second Evolution",
                        3 => "Third Evolution",
                        4 => "Awakening",
                        _ => "Ascension"
                    },
                    tier = (MuEvolutionTier)tier,
                    requiredLevel = tier switch
                    {
                        0 => 1,
                        1 => 80,
                        2 => 150,
                        3 => 220,
                        4 => 320,
                        _ => 420
                    },
                    requiredQuestId = tier >= 2 ? 1000 + tier : 0,
                    statMultipliers = new EvolutionStatMultipliers
                    {
                        hp = 1f + tier * 0.08f,
                        mana = 1f + tier * 0.07f,
                        stamina = 1f + tier * 0.05f,
                        damage = 1f + tier * 0.09f,
                        defense = 1f + tier * 0.08f,
                        attackSpeed = 1f + tier * 0.03f
                    },
                    unlockedSkillIds = new List<int>(allActiveIds),
                    passiveBonuses = new List<EvolutionPassiveBonus>
                    {
                        new EvolutionPassiveBonus
                        {
                            id = $"{id}_passive_{tier}",
                            displayName = "Mastery Bonus",
                            value = 2f + tier,
                            description = "Generic mastery scaling"
                        }
                    },
                    vfxTier = tier,
                    animatorOverride = null,
                    uiAccent = tier switch
                    {
                        0 => new Color(0.65f, 0.65f, 0.65f),
                        1 => new Color(0.45f, 0.75f, 1f),
                        2 => new Color(0.35f, 0.95f, 0.65f),
                        3 => new Color(1f, 0.8f, 0.35f),
                        4 => new Color(1f, 0.45f, 0.35f),
                        _ => new Color(0.95f, 0.55f, 1f)
                    },
                    unlockDescription = $"Unlock {nameof(MuEvolutionTier)} {(MuEvolutionTier)tier}"
                });
            }

            return list;
        }

        private static void CreateCombo(string assetName, MuClassId classId, SkillDefinition[] sequence, float finalMultiplier)
        {
            ComboDefinition combo = CreateOrLoad<ComboDefinition>($"{CombatDir}/{assetName}.asset");
            combo.classId = classId;
            combo.comboId = assetName;
            combo.resetTimeout = 1.7f;
            combo.finalStepBonusMultiplier = finalMultiplier;
            combo.showComboIndicator = true;

            combo.steps = new ComboStep[3];
            for (int i = 0; i < 3; i++)
            {
                combo.steps[i] = new ComboStep
                {
                    order = i,
                    requiredSkillId = sequence[i] != null ? sequence[i].skillId : 0,
                    minInputWindow = 0.05f,
                    maxInputWindow = 0.65f,
                    effectMultiplier = i == 2 ? finalMultiplier : 1f,
                    requiresHitFrame = true,
                    animationState = "Attack"
                };
            }

            EditorUtility.SetDirty(combo);
        }

        private static RuntimeAnimatorController CreateController(string shortName)
        {
            string path = $"{ControllersDir}/{shortName}.controller";
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

            EnsureState(ctrl, "Idle", true);
            EnsureState(ctrl, "Run");
            EnsureState(ctrl, "Cast");
            EnsureState(ctrl, "Attack");
            EnsureState(ctrl, "Hit");
            EnsureState(ctrl, "Death");
            EnsureState(ctrl, "Ultimate");

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        private static void EnsureState(AnimatorController controller, string stateName, bool setDefault = false)
        {
            if (controller == null)
                return;

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            for (int i = 0; i < sm.states.Length; i++)
            {
                if (sm.states[i].state != null && sm.states[i].state.name == stateName)
                {
                    if (setDefault)
                        sm.defaultState = sm.states[i].state;
                    return;
                }
            }

            AnimatorState state = sm.AddState(stateName);
            if (setDefault)
                sm.defaultState = state;
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureDir(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string Token(string text)
        {
            return (text ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .ToUpperInvariant();
        }

        private static int StableId(string text)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < text.Length; i++)
                    hash = hash * 31 + text[i];
                return Math.Abs(hash % 2_000_000_000);
            }
        }

        private static SkillTag AreaTarget(this SkillTag tag)
        {
            return tag;
        }
    }
}
