using System;
using System.Collections.Generic;
using MuLike.Combat;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Skills
{
    /// <summary>
    /// Runtime executor for skills (cooldowns, local UX validation, and server request dispatch).
    ///
    /// By design this manager does NOT own class/evolution definitions.
    /// It only executes skills present in the active loadout.
    /// </summary>
    public sealed class SkillManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CombatTuningProfile _tuning;
        [SerializeField] private StatsClientSystem _stats;

        [Header("Mobile Rules")]
        [SerializeField] private bool _applyMobileCastTimeReduction = true;
        [SerializeField] private bool _applyMobileMeleeRangeBoost = true;

        [Header("State")]
        [SerializeField] private SkillLoadout _loadout = new();

        private readonly Dictionary<int, float> _cooldownBySkillId = new();
        private readonly Dictionary<string, float> _cooldownByGroup = new();
        private int _nextRequestId = 1;

        public SkillLoadout Loadout => _loadout;

        public event Action<SkillExecutionContext> OnExecutionRequestedToServer;
        public event Action<int, string> OnExecutionRejectedLocally;
        public event Action<int> OnExecutionAcceptedLocally;

        private void Awake()
        {
            if (_stats == null)
                _stats = Core.GameContext.StatsClientSystem;
        }

        public bool TryExecuteSlot(int slotIndex, SkillExecutionContext baseContext)
        {
            if (!_loadout.TryGetBySlot(slotIndex, out SkillLoadout.Slot slot) || slot.skill == null)
            {
                OnExecutionRejectedLocally?.Invoke(0, "Skill slot is empty.");
                return false;
            }

            return TryExecuteSkill(slot.skill, slot.upgradeLevel, baseContext);
        }

        public bool TryExecuteSkill(SkillDefinition skill, int upgradeLevel, SkillExecutionContext baseContext)
        {
            if (skill == null)
            {
                OnExecutionRejectedLocally?.Invoke(0, "Skill definition is null.");
                return false;
            }

            if (!CanExecute(skill, upgradeLevel, out string reason))
            {
                OnExecutionRejectedLocally?.Invoke(skill.skillId, reason);
                return false;
            }

            SkillExecutionContext ctx = baseContext;
            ctx.requestId = _nextRequestId++;
            ctx.skillId = skill.skillId;
            ctx.clientTimestamp = Time.unscaledTime;
            ctx.upgradeLevel = Mathf.Max(0, upgradeLevel);

            ArmCooldowns(skill, upgradeLevel);
            OnExecutionAcceptedLocally?.Invoke(skill.skillId);
            OnExecutionRequestedToServer?.Invoke(ctx);
            return true;
        }

        public bool CanExecute(SkillDefinition skill, int upgradeLevel, out string reason)
        {
            reason = string.Empty;
            if (skill == null)
            {
                reason = "Skill is null.";
                return false;
            }

            if (GetRemainingCooldown(skill.skillId) > 0f)
            {
                reason = "Skill is on cooldown.";
                return false;
            }

            if (skill.sharedCooldownGroup != null && GetRemainingGroupCooldown(skill.sharedCooldownGroup.groupId) > 0f)
            {
                reason = "Shared cooldown active.";
                return false;
            }

            SkillResourceCost cost = ResolveCost(skill, upgradeLevel);
            if (_stats != null)
            {
                StatsClientSystem.ResourceStats r = _stats.Snapshot.Resources;
                if (r.Mana.Current < cost.mana)
                {
                    reason = "Not enough mana.";
                    return false;
                }

                if (r.Stamina.Current < cost.stamina)
                {
                    reason = "Not enough stamina.";
                    return false;
                }
            }

            return true;
        }

        public float GetRemainingCooldown(int skillId)
        {
            if (!_cooldownBySkillId.TryGetValue(skillId, out float endAt))
                return 0f;

            return Mathf.Max(0f, endAt - Time.time);
        }

        public float GetRemainingGroupCooldown(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return 0f;

            if (!_cooldownByGroup.TryGetValue(groupId, out float endAt))
                return 0f;

            return Mathf.Max(0f, endAt - Time.time);
        }

        public float ResolveEffectiveCastTime(SkillDefinition skill, int upgradeLevel)
        {
            float castTime = Mathf.Max(0f, skill.baseCastTime + GetUpgrade(skill, upgradeLevel).castTimeDeltaSeconds);
            if (_applyMobileCastTimeReduction)
            {
                float mobileFactor = _tuning != null ? _tuning.mobileCastTimeMultiplier : 0.7f;
                castTime *= Mathf.Clamp(mobileFactor, 0.1f, 1f);
            }

            return castTime;
        }

        public float ResolveEffectiveRange(SkillDefinition skill, int upgradeLevel)
        {
            float range = Mathf.Max(0.1f, skill.baseRange + GetUpgrade(skill, upgradeLevel).rangeDelta);
            if (_applyMobileMeleeRangeBoost && skill.IsMelee)
            {
                float factor = _tuning != null ? _tuning.mobileMeleeRangeMultiplier : 1.15f;
                range *= Mathf.Max(1f, factor);
            }

            return range;
        }

        private void ArmCooldowns(SkillDefinition skill, int upgradeLevel)
        {
            float ownCd = Mathf.Max(0f, skill.baseCooldown + GetUpgrade(skill, upgradeLevel).cooldownDeltaSeconds);
            _cooldownBySkillId[skill.skillId] = Time.time + ownCd;

            if (skill.sharedCooldownGroup != null && !string.IsNullOrWhiteSpace(skill.sharedCooldownGroup.groupId))
            {
                float shared = Mathf.Max(0f, skill.sharedCooldownGroup.sharedCooldownSeconds);
                _cooldownByGroup[skill.sharedCooldownGroup.groupId] = Time.time + shared;
            }
        }

        private SkillUpgradeNode GetUpgrade(SkillDefinition skill, int level)
        {
            if (skill.upgrades == null || skill.upgrades.Length == 0 || level <= 0)
                return default;

            SkillUpgradeNode best = default;
            bool found = false;
            for (int i = 0; i < skill.upgrades.Length; i++)
            {
                SkillUpgradeNode n = skill.upgrades[i];
                if (n.level > level)
                    continue;

                if (!found || n.level > best.level)
                {
                    best = n;
                    found = true;
                }
            }

            return best;
        }

        private SkillResourceCost ResolveCost(SkillDefinition skill, int level)
        {
            SkillUpgradeNode up = GetUpgrade(skill, level);
            SkillResourceCost baseCost = skill.cost;
            baseCost.mana = Mathf.Max(0, baseCost.mana + up.manaCostDelta);
            baseCost.stamina = Mathf.Max(0, baseCost.stamina + up.staminaCostDelta);
            baseCost.energy = Mathf.Max(0, baseCost.energy + up.energyCostDelta);
            baseCost.command = Mathf.Max(0, baseCost.command + up.commandCostDelta);
            return baseCost;
        }
    }
}
