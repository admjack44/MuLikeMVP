using System;
using MuLike.Gameplay.Controllers;
using MuLike.UI;
using MuLike.UI.MobileHUD;
using MuLike.World;
using UnityEngine;
using UnityEngine.AI;

namespace MuLike.Navigation
{
    /// <summary>
    /// World navigation service:
    /// - Auto path to world points / quest target / NPC target
    /// - Fast travel with cost + cooldown checks
    /// - Integrates minimap fast travel requests
    /// </summary>
    public sealed class MuNavigator : MonoBehaviour
    {
        [Serializable]
        public struct FastTravelNode
        {
            public string id;
            public string displayName;
            public MapLoader.MapId mapId;
            public Vector3 destination;
            public int zenCost;
        }

        [Header("Dependencies")]
        [SerializeField] private CharacterMotor _motor;
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private MapLoader _mapLoader;
        [SerializeField] private MinimapSystem _minimap;
        [SerializeField] private MonoBehaviour _questServiceProvider;

        [Header("Fast Travel")]
        [SerializeField] private FastTravelNode[] _travelNodes = Array.Empty<FastTravelNode>();
        [SerializeField, Min(1f)] private float _fastTravelCooldown = 20f;
        [SerializeField] private int _zenBalance = 50000;

        [Header("Quest")]
        [SerializeField] private bool _autoTrackFirstPathableQuest = true;

        private IQuestTrackerService _questService;
        private float _nextFastTravelAt;

        public int ZenBalance => _zenBalance;

        public event Action<string> OnNavigateLog;
        public event Action<string, bool, string> OnFastTravelResult;

        private void Awake()
        {
            if (_motor == null)
                _motor = FindAnyObjectByType<CharacterMotor>();
            if (_mapLoader == null)
                _mapLoader = FindAnyObjectByType<MapLoader>();
            if (_minimap == null)
                _minimap = FindAnyObjectByType<MinimapSystem>();
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

            if (_minimap != null)
                _minimap.OnFastTravelRequested += HandleFastTravelRequested;
        }

        private void OnDestroy()
        {
            if (_minimap != null)
                _minimap.OnFastTravelRequested -= HandleFastTravelRequested;
        }

        private void Update()
        {
            if (_autoTrackFirstPathableQuest)
                TryAutoPathFirstQuest();
        }

        public bool NavigateTo(Vector3 worldPoint)
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(worldPoint);
                Emit($"NavMesh path -> {worldPoint}");
                return true;
            }

            if (_motor != null)
            {
                _motor.MoveToPoint(worldPoint);
                Emit($"Motor path -> {worldPoint}");
                return true;
            }

            Emit("Navigation failed: missing agent/motor.");
            return false;
        }

        public bool TryFastTravelToNode(string nodeId)
        {
            for (int i = 0; i < _travelNodes.Length; i++)
            {
                FastTravelNode node = _travelNodes[i];
                if (!string.Equals(node.id, nodeId, StringComparison.OrdinalIgnoreCase))
                    continue;

                return TryFastTravel(node);
            }

            OnFastTravelResult?.Invoke(nodeId, false, "Node not found.");
            return false;
        }

        private bool TryFastTravel(FastTravelNode node)
        {
            if (Time.unscaledTime < _nextFastTravelAt)
            {
                OnFastTravelResult?.Invoke(node.id, false, "Fast travel cooldown active.");
                return false;
            }

            if (_zenBalance < node.zenCost)
            {
                OnFastTravelResult?.Invoke(node.id, false, "Not enough Zen.");
                return false;
            }

            _zenBalance -= Mathf.Max(0, node.zenCost);
            _nextFastTravelAt = Time.unscaledTime + _fastTravelCooldown;

            if (_mapLoader != null && _mapLoader.ActiveMapId != node.mapId)
                _mapLoader.TransitionToMap(node.mapId);

            if (_motor != null)
            {
                Transform actor = _motor.transform;
                actor.position = node.destination;
                _motor.Stop();
            }

            OnFastTravelResult?.Invoke(node.id, true, "Fast travel completed.");
            return true;
        }

        private void HandleFastTravelRequested(Vector3 destination)
        {
            // Treat minimap request as local path when destination is within current map.
            NavigateTo(destination);
        }

        private void TryAutoPathFirstQuest()
        {
            if (_questService == null)
                return;

            var quests = _questService.GetActiveQuests();
            if (quests == null)
                return;

            for (int i = 0; i < quests.Count; i++)
            {
                QuestTrackerEntry q = quests[i];
                if (q == null || !q.AutoPathAvailable || q.State != QuestObjectiveState.InProgress)
                    continue;

                NavigateTo(q.WorldTargetPosition);
                if (_minimap != null)
                    _minimap.SetQuestTarget(null, q.WorldTargetPosition, q.Title);
                break;
            }
        }

        private void Emit(string msg)
        {
            OnNavigateLog?.Invoke(msg);
        }
    }
}
