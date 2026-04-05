using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Mock quest source used until authoritative quest data is available.
    /// </summary>
    public sealed class MockQuestTrackerService : IQuestTrackerService
    {
        private readonly List<QuestTrackerEntry> _active = new();

        public event Action<IReadOnlyList<QuestTrackerEntry>> QuestsUpdated;

        public MockQuestTrackerService()
        {
            Seed();
        }

        public IReadOnlyList<QuestTrackerEntry> GetActiveQuests()
        {
            return _active;
        }

        public bool TryGetQuestById(int questId, out QuestTrackerEntry entry)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].QuestId != questId)
                    continue;

                entry = _active[i];
                return true;
            }

            entry = null;
            return false;
        }

        public void Refresh()
        {
            QuestsUpdated?.Invoke(_active);
        }

        private void Seed()
        {
            _active.Clear();
            _active.Add(new QuestTrackerEntry
            {
                QuestId = 101,
                Title = "Goblin Hunt",
                ObjectiveText = "Defeat 12 Goblins (4/12)",
                State = QuestObjectiveState.InProgress,
                AutoPathAvailable = true,
                WorldTargetPosition = new Vector3(16f, 0f, -8f),
                MapId = 1
            });

            _active.Add(new QuestTrackerEntry
            {
                QuestId = 102,
                Title = "Scout the East Gate",
                ObjectiveText = "Reach East Gate Watchtower",
                State = QuestObjectiveState.InProgress,
                AutoPathAvailable = true,
                WorldTargetPosition = new Vector3(42f, 0f, 24f),
                MapId = 1
            });

            _active.Add(new QuestTrackerEntry
            {
                QuestId = 103,
                Title = "Supply Delivery",
                ObjectiveText = "Return to Blacksmith",
                State = QuestObjectiveState.Completed,
                AutoPathAvailable = true,
                WorldTargetPosition = new Vector3(-10f, 0f, 5f),
                MapId = 1
            });
        }
    }
}
