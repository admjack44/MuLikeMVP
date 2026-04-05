using System;
using System.Collections.Generic;
using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Commercial HUD UX layer: minimap, quest tracker, target indicator, combat feedback and panel auto-hide.
    /// </summary>
    public sealed class MobileHudUxPresenter
    {
        private readonly MobileHudView _view;
        private readonly CharacterMotor _motor;
        private readonly TargetingController _targeting;
        private readonly StatsClientSystem _stats;
        private readonly IQuestTrackerService _quests;
        private readonly HudAutoHidePresenter _autoHide;

        private int _lastHp = -1;

        public MobileHudUxPresenter(
            MobileHudView view,
            CharacterMotor motor,
            TargetingController targeting,
            StatsClientSystem stats,
            IQuestTrackerService quests,
            float nonCriticalAutoHideSeconds)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _motor = motor;
            _targeting = targeting;
            _stats = stats;
            _quests = quests;
            _autoHide = new HudAutoHidePresenter(_view, nonCriticalAutoHideSeconds);
        }

        public void Bind()
        {
            _view.AnyHudInput += HandleAnyHudInput;
            _view.QuestTapped += HandleQuestTapped;
            _view.MapPressed += HandleMapPressed;
            _view.SettingsPressed += HandleSettingsPressed;
            _view.MinimapExpandPressed += HandleMapPressed;

            if (_quests != null)
            {
                _quests.QuestsUpdated += HandleQuestsUpdated;
                HandleQuestsUpdated(_quests.GetActiveQuests());
            }

            if (_targeting != null)
                _targeting.OnTargetChanged += HandleTargetChanged;

            if (_stats != null)
            {
                _stats.OnStatsSnapshotApplied += HandleStatsSnapshot;
                _stats.OnStatsDeltaApplied += HandleStatsDelta;
                HandleStatsSnapshot(_stats.Snapshot);
            }
        }

        public void Unbind()
        {
            _view.AnyHudInput -= HandleAnyHudInput;
            _view.QuestTapped -= HandleQuestTapped;
            _view.MapPressed -= HandleMapPressed;
            _view.SettingsPressed -= HandleSettingsPressed;
            _view.MinimapExpandPressed -= HandleMapPressed;

            if (_quests != null)
                _quests.QuestsUpdated -= HandleQuestsUpdated;

            if (_targeting != null)
                _targeting.OnTargetChanged -= HandleTargetChanged;

            if (_stats != null)
            {
                _stats.OnStatsSnapshotApplied -= HandleStatsSnapshot;
                _stats.OnStatsDeltaApplied -= HandleStatsDelta;
            }
        }

        public void Tick(float deltaTime)
        {
            _autoHide.Tick(deltaTime);
            UpdateMinimapMarkers();
        }

        private void HandleAnyHudInput()
        {
            _autoHide.NotifyInputActivity();
        }

        private void HandleQuestTapped(int questId)
        {
            if (_quests == null || _motor == null)
                return;

            if (!_quests.TryGetQuestById(questId, out QuestTrackerEntry quest) || quest == null || !quest.AutoPathAvailable)
                return;

            _motor.MoveToPoint(quest.WorldTargetPosition);
            _view.SetStatusToast($"Auto-path: {quest.Title}");
            _autoHide.NotifyInputActivity();
        }

        private void HandleMapPressed()
        {
            _view.SetStatusToast("Map panel requested.");
            _autoHide.NotifyInputActivity();
        }

        private void HandleSettingsPressed()
        {
            _view.SetStatusToast("Settings panel requested.");
            _autoHide.NotifyInputActivity();
        }

        private void HandleQuestsUpdated(IReadOnlyList<QuestTrackerEntry> quests)
        {
            _view.SetQuestEntries(quests);
            _autoHide.NotifyInputActivity();
        }

        private void HandleTargetChanged(EntityView target)
        {
            _view.SetTargetIndicatorTarget(target);
            _view.SetTarget(target != null ? target.name : "No target");
            _autoHide.NotifyInputActivity();
        }

        private void HandleStatsSnapshot(StatsClientSystem.PlayerStatsSnapshot snapshot)
        {
            int hp = snapshot.Resources.Hp.Current;
            int hpMax = Mathf.Max(1, snapshot.Resources.Hp.Max);

            if (_lastHp >= 0)
            {
                int delta = _lastHp - hp;
                if (delta > 0)
                    _view.ShowDamageTaken(delta);
            }

            _lastHp = hp;
            bool lowHp = hp <= Mathf.CeilToInt(hpMax * 0.3f);
            _view.SetLowHpState(lowHp);
            _autoHide.SetForceVisible(lowHp);
        }

        private void HandleStatsDelta(StatsClientSystem.PlayerStatsDelta _)
        {
            if (_stats != null)
                HandleStatsSnapshot(_stats.Snapshot);
        }

        private void UpdateMinimapMarkers()
        {
            if (_motor == null)
                return;

            _view.SetMinimapMapName("Lorencia");

            if (_targeting != null && _targeting.CurrentTarget != null)
            {
                Vector3 offset = _targeting.CurrentTarget.transform.position - _motor.transform.position;
                _view.SetMinimapMarker("target", offset, true, "Target");
            }
            else
            {
                _view.SetMinimapMarker("target", Vector3.zero, false, string.Empty);
            }

            if (_quests != null)
            {
                IReadOnlyList<QuestTrackerEntry> active = _quests.GetActiveQuests();
                QuestTrackerEntry firstWithPath = null;
                for (int i = 0; i < active.Count; i++)
                {
                    if (!active[i].AutoPathAvailable)
                        continue;

                    firstWithPath = active[i];
                    break;
                }

                if (firstWithPath != null)
                {
                    Vector3 offset = firstWithPath.WorldTargetPosition - _motor.transform.position;
                    _view.SetMinimapMarker("quest", offset, true, "Quest");
                }
                else
                {
                    _view.SetMinimapMarker("quest", Vector3.zero, false, string.Empty);
                }
            }
        }
    }
}
