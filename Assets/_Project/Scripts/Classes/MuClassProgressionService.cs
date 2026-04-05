using System;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Classes
{
    /// <summary>
    /// Resolves current class evolution based on level + quest progression.
    /// Progression concerns stay separated from skill runtime by design.
    /// </summary>
    public sealed class MuClassProgressionService : MonoBehaviour
    {
        [Serializable]
        public struct ProgressionContext
        {
            public MuClassId classId;
            public int level;
            public int[] completedQuestIds;
            public bool hasAccountUnlockFlag;
        }

        [SerializeField] private MuClassRegistry _registry;
        [SerializeField] private StatsClientSystem _stats;

        public event Action<MuClassId, MuEvolutionTier> OnEvolutionChanged;

        private MuClassId _trackedClass = MuClassId.Unknown;
        private MuEvolutionTier _currentTier = MuEvolutionTier.Tier0Base;

        private void Awake()
        {
            if (_registry == null)
                _registry = FindAnyObjectByType<MuClassRegistry>();
            if (_stats == null)
                _stats = Core.GameContext.StatsClientSystem;
        }

        public bool CanCreateClass(MuClassId classId, int accountLevel, bool hasQuest, bool hasAccountFlag)
        {
            if (_registry == null || !_registry.TryGetClass(classId, out MuClassDefinition def))
                return false;

            switch (def.unlockRequirement.type)
            {
                case UnlockRestrictionType.RequiredLevel:
                    return accountLevel >= def.unlockRequirement.requiredLevel;
                case UnlockRestrictionType.RequiredQuest:
                    return hasQuest;
                case UnlockRestrictionType.AccountProgression:
                    return hasAccountFlag;
                default:
                    return true;
            }
        }

        public MuEvolutionTier ResolveTier(ProgressionContext context)
        {
            if (_registry == null || !_registry.TryGetClass(context.classId, out MuClassDefinition def) || def.evolutions == null)
                return MuEvolutionTier.Tier0Base;

            MuEvolutionTier best = MuEvolutionTier.Tier0Base;
            for (int i = 0; i < def.evolutions.Count; i++)
            {
                MuClassEvolutionData evo = def.evolutions[i];
                if (!evo.IsValid)
                    continue;

                if (context.level < evo.requiredLevel)
                    continue;

                if (evo.requiredQuestId > 0 && !Contains(context.completedQuestIds, evo.requiredQuestId))
                    continue;

                if (evo.tier > best)
                    best = evo.tier;
            }

            return best;
        }

        public bool TryGetCurrentEvolution(ProgressionContext context, out MuClassEvolutionData evolution)
        {
            evolution = default;
            MuEvolutionTier tier = ResolveTier(context);
            return _registry != null && _registry.TryGetEvolution(context.classId, tier, out evolution);
        }

        public void TrackClass(MuClassId classId)
        {
            _trackedClass = classId;
            _currentTier = MuEvolutionTier.Tier0Base;
        }

        public void TickProgression(int[] completedQuestIds)
        {
            if (_trackedClass == MuClassId.Unknown || _stats == null)
                return;

            ProgressionContext ctx = new ProgressionContext
            {
                classId = _trackedClass,
                level = _stats.Snapshot.Primary.Level,
                completedQuestIds = completedQuestIds,
                hasAccountUnlockFlag = false
            };

            MuEvolutionTier next = ResolveTier(ctx);
            if (next == _currentTier)
                return;

            _currentTier = next;
            OnEvolutionChanged?.Invoke(_trackedClass, _currentTier);
        }

        private static bool Contains(int[] values, int target)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == target)
                    return true;
            }

            return false;
        }
    }
}
