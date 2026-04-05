using System;
using System.Collections.Generic;
using MuLike.Gameplay.Controllers;
using MuLike.UI.MobileHUD;
using UnityEngine;
using UnityEngine.AI;

namespace MuLike.Quest
{
    /// <summary>
    /// Auto-quest manager for mobile flows.
    ///
    /// Features:
    /// - Tracks active quest objectives from IQuestTrackerService.
    /// - Auto-path to quest objectives / NPCs using CharacterMotor or NavMeshAgent.
    /// - Auto-turn-in completed quests.
    /// - Optional auto-accept for available quests.
    ///
    /// Networking/authority:
    /// - This class emits intent events (`OnQuestTurnInRequested`, `OnQuestAcceptRequested`).
    /// - Server should validate and respond with authoritative quest state updates.
    /// </summary>
    public sealed class AutoQuestManager : MonoBehaviour
    {
        [Serializable]
        public struct NpcWaypoint
        {
            public int npcId;
            public string npcName;
            public Vector3 worldPosition;
        }

        [Header("Dependencies")]
        [SerializeField] private CharacterMotor _motor;
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private MonoBehaviour _questServiceProvider;

        [Header("Automation")]
        [SerializeField] private bool _enabledByDefault = false;
        [SerializeField] private bool _autoTurnInCompleted = true;
        [SerializeField] private bool _autoAcceptNewQuests = false;
        [SerializeField, Min(0.1f)] private float _tickInterval = 0.5f;
        [SerializeField, Min(0.1f)] private float _reachDistance = 1.8f;

        [Header("NPC Pathing")]
        [SerializeField] private NpcWaypoint[] _npcWaypoints = Array.Empty<NpcWaypoint>();

        [Header("Debug")]
        [SerializeField] private bool _verboseLogs = false;

        private IQuestTrackerService _questService;
        private bool _enabled;
        private float _nextTickAt;
        private QuestTrackerEntry _activeQuest;

        public bool IsEnabled => _enabled;

        public event Action<int> OnQuestTurnInRequested;
        public event Action<int> OnQuestAcceptRequested;
        public event Action<int, Vector3> OnAutoPathStarted;
        public event Action<string> OnQuestLog;

        private void Awake()
        {
            if (_motor == null)
                _motor = FindAnyObjectByType<CharacterMotor>();
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();

            _questService = _questServiceProvider as IQuestTrackerService;
            if (_questService == null)
            {
                MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IQuestTrackerService candidate)
                    {
                        _questService = candidate;
                        break;
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (_questService != null)
                _questService.QuestsUpdated += HandleQuestsUpdated;
        }

        private void OnDisable()
        {
            if (_questService != null)
                _questService.QuestsUpdated -= HandleQuestsUpdated;
        }

        private void Start()
        {
            SetEnabled(_enabledByDefault);
            _questService?.Refresh();
        }

        private void Update()
        {
            if (!_enabled || _questService == null)
                return;

            if (Time.unscaledTime < _nextTickAt)
                return;

            _nextTickAt = Time.unscaledTime + Mathf.Max(0.1f, _tickInterval);
            TickQuestFlow();
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            Emit($"Enabled={_enabled}");
        }

        private void TickQuestFlow()
        {
            IReadOnlyList<QuestTrackerEntry> quests = _questService.GetActiveQuests();
            if (quests == null || quests.Count == 0)
                return;

            QuestTrackerEntry completed = null;
            QuestTrackerEntry inProgress = null;

            for (int i = 0; i < quests.Count; i++)
            {
                QuestTrackerEntry q = quests[i];
                if (q == null)
                    continue;

                if (q.State == QuestObjectiveState.Completed)
                {
                    completed = q;
                    break;
                }

                if (q.State == QuestObjectiveState.InProgress && inProgress == null)
                    inProgress = q;
            }

            if (_autoTurnInCompleted && completed != null)
            {
                AutoTurnIn(completed);
                return;
            }

            if (inProgress != null)
            {
                FollowQuestObjective(inProgress);
                return;
            }

            if (_autoAcceptNewQuests)
                AutoAcceptPlaceholder();
        }

        private void FollowQuestObjective(QuestTrackerEntry quest)
        {
            if (quest == null)
                return;

            _activeQuest = quest;
            if (!quest.AutoPathAvailable)
                return;

            Vector3 destination = quest.WorldTargetPosition;
            NavigateTo(destination);
            OnAutoPathStarted?.Invoke(quest.QuestId, destination);
            Emit($"Auto-path quest {quest.QuestId}: {quest.Title}");
        }

        public bool TryNavigateToNpc(int npcId)
        {
            for (int i = 0; i < _npcWaypoints.Length; i++)
            {
                if (_npcWaypoints[i].npcId != npcId)
                    continue;

                NavigateTo(_npcWaypoints[i].worldPosition);
                Emit($"Navigating to NPC {npcId} ({_npcWaypoints[i].npcName}).");
                return true;
            }

            Emit($"NPC waypoint not found for npcId={npcId}");
            return false;
        }

        private void AutoTurnIn(QuestTrackerEntry quest)
        {
            if (quest == null)
                return;

            OnQuestTurnInRequested?.Invoke(quest.QuestId);
            Emit($"Turn-in requested for quest {quest.QuestId}");
        }

        private void AutoAcceptPlaceholder()
        {
            // Placeholder integration point for server-driven available-quest listings.
            // The concrete quest id should come from a backend response, not from static client data.
            const int PlaceholderQuestId = -1;
            OnQuestAcceptRequested?.Invoke(PlaceholderQuestId);
            Emit("Auto-accept requested (placeholder). Integrate with backend available-quest feed.");
        }

        private void NavigateTo(Vector3 worldPoint)
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(worldPoint);
                return;
            }

            _motor?.MoveToPoint(worldPoint);
        }

        private void HandleQuestsUpdated(IReadOnlyList<QuestTrackerEntry> quests)
        {
            if (quests == null)
                return;

            if (_activeQuest == null)
                return;

            for (int i = 0; i < quests.Count; i++)
            {
                QuestTrackerEntry q = quests[i];
                if (q == null || q.QuestId != _activeQuest.QuestId)
                    continue;

                _activeQuest = q;
                break;
            }
        }

        private void Emit(string msg)
        {
            if (!_verboseLogs)
                return;

            string line = $"[AutoQuest] {msg}";
            Debug.Log(line);
            OnQuestLog?.Invoke(line);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _npcWaypoints.Length; i++)
            {
                Vector3 p = _npcWaypoints[i].worldPosition;
                Gizmos.DrawWireSphere(p, 0.4f);
            }
        }
#endif
    }
}
