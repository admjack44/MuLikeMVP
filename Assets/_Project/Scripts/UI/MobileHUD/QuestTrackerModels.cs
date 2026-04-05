using UnityEngine;

namespace MuLike.UI.MobileHUD
{
    public enum QuestObjectiveState
    {
        InProgress,
        Completed,
        Failed
    }

    public sealed class QuestTrackerEntry
    {
        public int QuestId { get; set; }
        public string Title { get; set; }
        public string ObjectiveText { get; set; }
        public QuestObjectiveState State { get; set; }
        public bool AutoPathAvailable { get; set; }
        public Vector3 WorldTargetPosition { get; set; }
        public int MapId { get; set; }
    }
}
