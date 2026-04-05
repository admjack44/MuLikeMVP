using System;
using System.Collections.Generic;

namespace MuLike.UI.MobileHUD
{
    public interface IQuestTrackerService
    {
        event Action<IReadOnlyList<QuestTrackerEntry>> QuestsUpdated;
        IReadOnlyList<QuestTrackerEntry> GetActiveQuests();
        bool TryGetQuestById(int questId, out QuestTrackerEntry entry);
        void Refresh();
    }
}
