using System;
using System.Collections.Generic;

namespace MuLike.Systems
{
    /// <summary>
    /// Maintains known skills, skill levels and active skill bar for MMO mobile combat UI.
    /// </summary>
    public sealed class SkillBookClientSystem
    {
        [Serializable]
        public struct SkillEntry
        {
            public int SkillId;
            public int Level;
            public bool IsUnlocked;
        }

        [Serializable]
        public sealed class SkillBookSnapshot
        {
            public List<SkillEntry> Skills = new();
            public List<int> ActiveSkillBar = new();
        }

        [Serializable]
        public struct SkillBookDelta
        {
            public bool HasUpsertSkill;
            public SkillEntry UpsertSkill;

            public bool HasRemoveSkill;
            public int RemoveSkillId;

            public bool HasSetActiveSlot;
            public int ActiveSlotIndex;
            public int ActiveSlotSkillId;
        }

        private readonly Dictionary<int, SkillEntry> _skillsById = new();
        private readonly List<int> _activeSkillBar = new();

        public IReadOnlyDictionary<int, SkillEntry> SkillsById => _skillsById;
        public IReadOnlyList<int> ActiveSkillBar => _activeSkillBar;

        public event Action<SkillBookSnapshot> OnSkillBookSnapshotApplied;
        public event Action<SkillBookDelta> OnSkillBookDeltaApplied;
        public event Action OnSkillBookChanged;
        public event Action<SkillEntry> OnSkillUnlocked;
        public event Action<int, int> OnActiveSlotChanged;

        public void ApplySnapshot(SkillBookSnapshot snapshot)
        {
            _skillsById.Clear();
            _activeSkillBar.Clear();

            if (snapshot != null)
            {
                if (snapshot.Skills != null)
                {
                    for (int i = 0; i < snapshot.Skills.Count; i++)
                    {
                        SkillEntry entry = Normalize(snapshot.Skills[i]);
                        if (entry.SkillId <= 0)
                            continue;

                        _skillsById[entry.SkillId] = entry;
                    }
                }

                if (snapshot.ActiveSkillBar != null)
                {
                    for (int i = 0; i < snapshot.ActiveSkillBar.Count; i++)
                        _activeSkillBar.Add(Math.Max(0, snapshot.ActiveSkillBar[i]));
                }
            }

            OnSkillBookSnapshotApplied?.Invoke(CreateSnapshot());
            OnSkillBookChanged?.Invoke();
        }

        public void ApplyDelta(SkillBookDelta delta)
        {
            if (delta.HasUpsertSkill)
            {
                SkillEntry normalized = Normalize(delta.UpsertSkill);
                if (normalized.SkillId > 0)
                {
                    bool wasUnlocked = _skillsById.TryGetValue(normalized.SkillId, out SkillEntry previous) && previous.IsUnlocked;
                    _skillsById[normalized.SkillId] = normalized;

                    if (normalized.IsUnlocked && !wasUnlocked)
                        OnSkillUnlocked?.Invoke(normalized);
                }
            }

            if (delta.HasRemoveSkill)
                _skillsById.Remove(Math.Max(0, delta.RemoveSkillId));

            if (delta.HasSetActiveSlot)
            {
                int slotIndex = Math.Max(0, delta.ActiveSlotIndex);
                int skillId = Math.Max(0, delta.ActiveSlotSkillId);
                while (_activeSkillBar.Count <= slotIndex)
                    _activeSkillBar.Add(0);

                _activeSkillBar[slotIndex] = skillId;
                OnActiveSlotChanged?.Invoke(slotIndex, skillId);
            }

            OnSkillBookDeltaApplied?.Invoke(delta);
            OnSkillBookChanged?.Invoke();
        }

        public SkillBookSnapshot CreateSnapshot()
        {
            var snapshot = new SkillBookSnapshot();

            foreach (KeyValuePair<int, SkillEntry> pair in _skillsById)
                snapshot.Skills.Add(pair.Value);

            snapshot.Skills.Sort((a, b) => a.SkillId.CompareTo(b.SkillId));
            snapshot.ActiveSkillBar.AddRange(_activeSkillBar);
            return snapshot;
        }

        public bool TryGetSkill(int skillId, out SkillEntry entry)
        {
            return _skillsById.TryGetValue(skillId, out entry);
        }

        public void Clear()
        {
            ApplySnapshot(new SkillBookSnapshot());
        }

        private static SkillEntry Normalize(SkillEntry entry)
        {
            entry.SkillId = Math.Max(0, entry.SkillId);
            entry.Level = Math.Max(0, entry.Level);
            return entry;
        }
    }
}
