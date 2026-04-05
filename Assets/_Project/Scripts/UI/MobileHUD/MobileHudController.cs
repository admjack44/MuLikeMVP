using MuLike.Core;
using MuLike.Data.Catalogs;
using MuLike.Gameplay.Combat;
using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using MuLike.Networking;
using MuLike.Systems;
using MuLike.UI.Chat;
using MuLike.UI.Equipment;
using MuLike.UI.Inventory;
using UnityEngine;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Bridges MobileHudView with gameplay systems.
    /// </summary>
    public sealed class MobileHudController : MonoBehaviour
    {
        private const int HpPotionQuickSlotId = 1;
        private const int MpPotionQuickSlotId = 2;

        [SerializeField] private MobileHudView _view;
        [SerializeField] private CharacterMotor _characterMotor;
        [SerializeField] private CombatController _combatController;
        [SerializeField] private TargetingController _targetingController;
        [SerializeField] private CameraFollowController _cameraFollow;

        [Header("MMORPG Runtime Dependencies")]
        [SerializeField] private StatsClientSystem _statsSystem;
        [SerializeField] private CharacterSessionSystem _characterSessionSystem;
        [SerializeField] private SkillBookClientSystem _skillBookSystem;
        [SerializeField] private InventoryClientSystem _inventorySystem;
        [SerializeField] private EquipmentClientSystem _equipmentSystem;

        [Header("Panels")]
        [SerializeField] private InventoryView _inventoryView;
        [SerializeField] private EquipmentPanelView _equipmentPanelView;
        [SerializeField] private ChatView _chatView;
        [SerializeField] private SimpleHudPanelView _characterPanel;
        [SerializeField] private SimpleHudPanelView _mapPanel;

        [Header("Movement Feel")]
        [SerializeField, Range(0f, 0.8f)] private float _moveDeadZone = 0.12f;
        [SerializeField] private float _moveSmoothing = 14f;
        [SerializeField] private bool _stopOnRelease = true;

        [Header("Targeting")]
        [SerializeField] private bool _autoTargetOnAttack = true;
        [SerializeField] private bool _switchTargetByRelevance = true;
        [SerializeField] private float _retargetInterval = 0.35f;
        [SerializeField] private float _skillAvailabilityRefreshInterval = 0.12f;

        [Header("Aim Assist")]
        [SerializeField] private bool _enableAimAssist = true;
        [SerializeField, Range(0f, 1f)] private float _aimAssistStrength = 0.32f;
        [SerializeField] private float _aimAssistMaxDistance = 12f;
        [SerializeField, Range(0f, 180f)] private float _aimAssistMaxAngle = 65f;

        [Header("Debug")]
        [SerializeField] private bool _drawDebugGizmos = false;

        [Header("Auto Combat")]
        [SerializeField] private bool _showAutoCombatLogs = true;

        [Header("Quick Slots")]
        [SerializeField, Min(0.25f)] private float _potionCooldownSeconds = 1.2f;
        [SerializeField] private string _defaultHpPotionLabel = "HP";
        [SerializeField] private string _defaultMpPotionLabel = "MP";

        [Header("HUD UX")]
        [SerializeField] private bool _useMockQuestTracker = true;
        [SerializeField, Min(2f)] private float _nonCriticalAutoHideSeconds = 5f;

        private struct PotionQuickSlotState
        {
            public int SlotId;
            public int InventorySlotIndex;
            public int ItemId;
            public int Count;
            public string Label;
            public float CooldownEndTime;
        }

        private float _nextRetargetAt;
        private float _nextSkillAvailabilityAt;
        private Vector2 _smoothedInput;
        private Vector3 _lastResolvedMoveDirection;
        private float _nextTargetHudRefreshAt;
        private bool _autoBattleEnabled;

        private IQuestTrackerService _questTrackerService;
        private MobileHudUxPresenter _uxPresenter;
        private PotionQuickSlotState _hpPotionSlot;
        private PotionQuickSlotState _mpPotionSlot;
        private HudPanelCoordinator _panelCoordinator;
        private InventoryPanelPresenter _inventoryPanelPresenter;
        private EquipmentPanelPresenter _equipmentPanelPresenter;
        private ChatPanelPresenter _chatPanelPresenter;
        private InventoryPresenter _inventoryPresenter;
        private EquipmentPresenter _equipmentPresenter;
        private ChatPresenter _chatPresenter;
        private InventoryEquipmentService _inventoryEquipmentService;
        private IChatTransport _fallbackChatTransport;

        private void Awake()
        {
            if (_view == null)
                _view = FindAnyObjectByType<MobileHudView>();

            if (_characterMotor == null)
                _characterMotor = FindAnyObjectByType<CharacterMotor>();

            if (_combatController == null)
                _combatController = FindAnyObjectByType<CombatController>();

            if (_targetingController == null)
                _targetingController = FindAnyObjectByType<TargetingController>();

            if (_cameraFollow == null)
                _cameraFollow = FindAnyObjectByType<CameraFollowController>();

            if (_inventoryView == null)
                _inventoryView = FindAnyObjectByType<InventoryView>();

            if (_equipmentPanelView == null)
                _equipmentPanelView = FindAnyObjectByType<EquipmentPanelView>();

            if (_chatView == null)
                _chatView = FindAnyObjectByType<ChatView>();

            if (_statsSystem == null && GameContext.TryGetSystem(out StatsClientSystem stats))
                _statsSystem = stats;

            if (_characterSessionSystem == null && GameContext.TryGetSystem(out CharacterSessionSystem session))
                _characterSessionSystem = session;

            if (_skillBookSystem == null && GameContext.TryGetSystem(out SkillBookClientSystem skillBook))
                _skillBookSystem = skillBook;

            if (_inventorySystem == null && GameContext.TryGetSystem(out InventoryClientSystem inventory))
                _inventorySystem = inventory;

            if (_equipmentSystem == null && GameContext.TryGetSystem(out EquipmentClientSystem equipment))
                _equipmentSystem = equipment;

            if (_view == null)
            {
                Debug.LogError("[MobileHudController] MobileHudView missing.");
                enabled = false;
                return;
            }

            if (_useMockQuestTracker)
                _questTrackerService = new MockQuestTrackerService();

            _uxPresenter = new MobileHudUxPresenter(
                _view,
                _characterMotor,
                _targetingController,
                _statsSystem,
                _questTrackerService,
                _nonCriticalAutoHideSeconds);

            InitializePanelPresenters();

            if (_combatController != null)
            {
                for (int i = 0; i < _combatController.SkillCount; i++)
                {
                    if (_combatController.TryGetSkillModel(i, out CombatSkillModel skill))
                        _view.SetSkillName(skill.SkillId, skill.DisplayName);
                }
            }

            _hpPotionSlot = new PotionQuickSlotState
            {
                SlotId = HpPotionQuickSlotId,
                InventorySlotIndex = -1,
                Label = _defaultHpPotionLabel,
                ItemId = 0,
                Count = 0,
                CooldownEndTime = 0f
            };

            _mpPotionSlot = new PotionQuickSlotState
            {
                SlotId = MpPotionQuickSlotId,
                InventorySlotIndex = -1,
                Label = _defaultMpPotionLabel,
                ItemId = 0,
                Count = 0,
                CooldownEndTime = 0f
            };

            RefreshSkillStripFromSkillBook();
            RefreshPotionQuickSlots();
            ApplyPotionQuickSlotView();
        }

        private void OnEnable()
        {
            _view.MoveInputChanged += HandleMoveInput;
            _view.SkillPressed += HandleSkillPressed;
            _view.AutoBattleChanged += HandleAutoAttackChanged;
            _view.PotionQuickSlotPressed += HandlePotionQuickSlotPressed;
            _view.ChatPressed += HandleChatPressed;
            _view.InventoryPressed += HandleInventoryPressed;
            _view.CharacterPressed += HandleCharacterPressed;
            _view.MinimapPressed += HandleMinimapPressed;
            _view.MapPressed += HandleMapPressed;

            if (_statsSystem != null)
            {
                _statsSystem.OnStatsSnapshotApplied += HandleStatsSnapshot;
                _statsSystem.OnStatsDeltaApplied += HandleStatsDelta;
                HandleStatsSnapshot(_statsSystem.Snapshot);
            }

            if (_characterSessionSystem != null)
            {
                _characterSessionSystem.OnSessionSnapshotApplied += HandleSessionSnapshot;
                _characterSessionSystem.OnSessionDeltaApplied += HandleSessionDelta;
                HandleSessionSnapshot(_characterSessionSystem.Snapshot);
            }

            if (_skillBookSystem != null)
            {
                _skillBookSystem.OnSkillBookSnapshotApplied += HandleSkillBookSnapshot;
                _skillBookSystem.OnSkillBookDeltaApplied += HandleSkillBookDelta;
                RefreshSkillStripFromSkillBook();
            }

            if (_inventorySystem != null)
            {
                _inventorySystem.OnInventoryChanged += HandleInventoryChanged;
                RefreshPotionQuickSlots();
                ApplyPotionQuickSlotView();
            }

            if (_equipmentSystem != null)
                _equipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;

            if (_targetingController != null)
            {
                _targetingController.OnTargetChanged += HandleTargetChanged;
                HandleTargetChanged(_targetingController.CurrentTarget);
            }

            if (_combatController != null)
            {
                _combatController.OnSkillCooldownUpdated += HandleSkillCooldownUpdated;
                _combatController.OnSkillCastRejected += HandleSkillCastRejected;
                _combatController.OnAutoCombatStateChanged += HandleAutoCombatStateChanged;
                _combatController.OnAutoCombatEnabledChanged += HandleAutoCombatEnabledChanged;
                _combatController.OnAutoCombatLog += HandleAutoCombatLog;
                HandleAutoCombatEnabledChanged(_combatController.IsAutoCombatEnabled);
            }

            _inventoryPresenter?.Bind();
            _equipmentPresenter?.Bind();
            _chatPresenter?.Bind();

            _uxPresenter?.Bind();
            _questTrackerService?.Refresh();
        }

        private void OnDisable()
        {
            _view.MoveInputChanged -= HandleMoveInput;
            _view.SkillPressed -= HandleSkillPressed;
            _view.AutoBattleChanged -= HandleAutoAttackChanged;
            _view.PotionQuickSlotPressed -= HandlePotionQuickSlotPressed;
            _view.ChatPressed -= HandleChatPressed;
            _view.InventoryPressed -= HandleInventoryPressed;
            _view.CharacterPressed -= HandleCharacterPressed;
            _view.MinimapPressed -= HandleMinimapPressed;
            _view.MapPressed -= HandleMapPressed;

            if (_statsSystem != null)
            {
                _statsSystem.OnStatsSnapshotApplied -= HandleStatsSnapshot;
                _statsSystem.OnStatsDeltaApplied -= HandleStatsDelta;
            }

            if (_characterSessionSystem != null)
            {
                _characterSessionSystem.OnSessionSnapshotApplied -= HandleSessionSnapshot;
                _characterSessionSystem.OnSessionDeltaApplied -= HandleSessionDelta;
            }

            if (_skillBookSystem != null)
            {
                _skillBookSystem.OnSkillBookSnapshotApplied -= HandleSkillBookSnapshot;
                _skillBookSystem.OnSkillBookDeltaApplied -= HandleSkillBookDelta;
            }

            if (_inventorySystem != null)
                _inventorySystem.OnInventoryChanged -= HandleInventoryChanged;

            if (_equipmentSystem != null)
                _equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;

            if (_targetingController != null)
                _targetingController.OnTargetChanged -= HandleTargetChanged;

            if (_combatController != null)
            {
                _combatController.OnSkillCooldownUpdated -= HandleSkillCooldownUpdated;
                _combatController.OnSkillCastRejected -= HandleSkillCastRejected;
                _combatController.OnAutoCombatStateChanged -= HandleAutoCombatStateChanged;
                _combatController.OnAutoCombatEnabledChanged -= HandleAutoCombatEnabledChanged;
                _combatController.OnAutoCombatLog -= HandleAutoCombatLog;
            }

            _inventoryPresenter?.Unbind();
            _equipmentPresenter?.Unbind();
            _chatPresenter?.Unbind();

            _uxPresenter?.Unbind();
        }

        private void Update()
        {
            HandleRetargetTick();
            TickSkillAvailability();
            TickPotionCooldowns();
            TickTargetPortraitRefresh();
            _uxPresenter?.Tick(Time.unscaledDeltaTime);
        }

        private void HandleMoveInput(Vector2 input)
        {
            if (_characterMotor == null)
                return;

            float smoothStep = 1f - Mathf.Exp(-Mathf.Max(1f, _moveSmoothing) * Time.deltaTime);
            _smoothedInput = Vector2.Lerp(_smoothedInput, input, smoothStep);

            if (_smoothedInput.magnitude < _moveDeadZone)
            {
                if (_stopOnRelease)
                    _characterMotor.Stop();

                _lastResolvedMoveDirection = Vector3.zero;
                return;
            }

            Vector3 direction = ResolveMoveDirectionFromCamera(_smoothedInput);
            direction = ApplyAimAssist(direction);

            if (direction.sqrMagnitude <= 0.001f)
            {
                if (_stopOnRelease)
                    _characterMotor.Stop();
                return;
            }

            _lastResolvedMoveDirection = direction;
            _characterMotor.SetMoveDirection(direction);
        }

        private void HandleSkillPressed(int skillId)
        {
            if (_combatController == null)
                return;

            if (_autoTargetOnAttack)
                EnsureAttackTarget();

            if (!_combatController.CanCastSkillById(skillId, out string reason))
            {
                if (!string.IsNullOrWhiteSpace(reason))
                    _view.SetStatusToast(reason);
                return;
            }

            _combatController.TryCastSkillById(skillId);
        }

        private void HandleAutoAttackChanged(bool enabled)
        {
            _combatController?.SetAutoCombatEnabled(enabled);
        }

        public Vector3 ResolveMoveDirectionFromCamera(Vector2 input)
        {
            // Prefer CameraFollowController for accurate camera-relative movement
            if (_cameraFollow != null)
            {
                Vector3 cameraForward = _cameraFollow.GetCameraRelativeForward();
                Vector3 cameraRight = _cameraFollow.GetCameraRelativeRight();
                Vector3 direction = (cameraRight * input.x + cameraForward * input.y);
                return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
            }

            // Fallback: use main camera
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
            {
                Vector3 fallback = new Vector3(input.x, 0f, input.y);
                return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.zero;
            }

            Vector3 forward = cam.forward;
            Vector3 right = cam.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 directionCam = (right * input.x + forward * input.y);
            return directionCam.sqrMagnitude > 0.001f ? directionCam.normalized : Vector3.zero;
        }

        private void HandleStatsSnapshot(StatsClientSystem.PlayerStatsSnapshot snapshot)
        {
            _view.SetHp(snapshot.Resources.Hp.Current, snapshot.Resources.Hp.Max);
            _view.SetMp(snapshot.Resources.Mana.Current, snapshot.Resources.Mana.Max);
            _view.SetSd(snapshot.Resources.Shield.Current, snapshot.Resources.Shield.Max);
            _view.SetStamina(snapshot.Resources.Stamina.Current, snapshot.Resources.Stamina.Max);
        }

        private void HandleStatsDelta(StatsClientSystem.PlayerStatsDelta _)
        {
            if (_statsSystem != null)
                HandleStatsSnapshot(_statsSystem.Snapshot);
        }

        private void HandleTargetChanged(EntityView target)
        {
            ApplyTargetPortrait(target);
        }

        private void HandleSkillCooldownUpdated(int skillId, float remaining, float total)
        {
            _view.SetSkillCooldown(skillId, remaining, total);
        }

        private void HandleSkillCastRejected(int _, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return;

            _view.SetStatusToast(reason);
        }

        private void HandleSessionSnapshot(CharacterSessionSystem.SessionSnapshot snapshot)
        {
            if (!_characterSessionSystem.Snapshot.IsAuthenticated)
            {
                _view.SetStatusToast("Guest mode: no authenticated session.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.SelectedCharacterName))
                _view.SetStatusToast($"Character: {snapshot.SelectedCharacterName}");

            if (_inventoryEquipmentService != null)
            {
                string characterId = snapshot.SelectedCharacterId > 0
                    ? snapshot.SelectedCharacterId.ToString()
                    : "char-local";
                _inventoryEquipmentService.SetCharacterId(characterId);
            }
        }

        private void HandleSessionDelta(CharacterSessionSystem.SessionDelta _)
        {
            if (_characterSessionSystem != null)
                HandleSessionSnapshot(_characterSessionSystem.Snapshot);
        }

        private void HandleSkillBookSnapshot(SkillBookClientSystem.SkillBookSnapshot _)
        {
            RefreshSkillStripFromSkillBook();
        }

        private void HandleSkillBookDelta(SkillBookClientSystem.SkillBookDelta _)
        {
            RefreshSkillStripFromSkillBook();
        }

        private void HandleInventoryChanged()
        {
            RefreshPotionQuickSlots();
            ApplyPotionQuickSlotView();
        }

        private void HandleEquipmentChanged()
        {
            _view.SetStatusToast($"Equipment updated ({_equipmentSystem.Equipped.Count} equipped).");
        }

        private void HandlePotionQuickSlotPressed(int quickSlotId)
        {
            if (quickSlotId == HpPotionQuickSlotId)
            {
                TryUsePotionQuickSlot(ref _hpPotionSlot);
                return;
            }

            if (quickSlotId == MpPotionQuickSlotId)
                TryUsePotionQuickSlot(ref _mpPotionSlot);
        }

        private void HandleAutoCombatStateChanged(AutoCombatState state)
        {
            if (_showAutoCombatLogs)
                Debug.Log($"[MobileHUD] AutoCombatState={state}");

            bool enabled = state != AutoCombatState.Disabled && state != AutoCombatState.Invalid;
            if (_autoBattleEnabled != enabled)
            {
                _autoBattleEnabled = enabled;
                _view.SetAutoBattleState(enabled);
            }
        }

        private void HandleAutoCombatEnabledChanged(bool enabled)
        {
            _autoBattleEnabled = enabled;
            _view.SetAutoBattleState(enabled);
        }

        private void HandleAutoCombatLog(string line)
        {
            if (_showAutoCombatLogs)
                Debug.Log(line);
        }

        private void HandleRetargetTick()
        {
            if (!_switchTargetByRelevance || _targetingController == null)
                return;

            if (Time.time < _nextRetargetAt)
                return;

            _nextRetargetAt = Time.time + Mathf.Max(0.1f, _retargetInterval);

            EntityView best = _targetingController.AcquirePriorityTarget();
            if (best != null && best != _targetingController.CurrentTarget)
                _targetingController.SetLockedTarget(best);
        }

        private void TickSkillAvailability()
        {
            if (_view == null || _combatController == null)
                return;

            if (Time.unscaledTime < _nextSkillAvailabilityAt)
                return;

            _nextSkillAvailabilityAt = Time.unscaledTime + Mathf.Max(0.05f, _skillAvailabilityRefreshInterval);

            for (int i = 0; i < _combatController.SkillCount; i++)
            {
                if (!_combatController.TryGetSkillModel(i, out CombatSkillModel skill))
                    continue;

                bool canCast = _combatController.CanCastSkillById(skill.SkillId, out _);
                _view.SetSkillInteractable(skill.SkillId, canCast);
            }

            bool canBasic = _combatController.CanCastSkillById(_combatController.BasicAttackSkillId, out _);
            _view.SetSkillInteractable(_combatController.BasicAttackSkillId, canBasic);
        }

        private void EnsureAttackTarget()
        {
            if (_targetingController == null)
                return;

            if (_targetingController.CurrentTarget != null)
                return;

            EntityView best = _targetingController.AcquirePriorityTarget();
            if (best != null)
                _targetingController.SetLockedTarget(best);
        }

        private Vector3 ApplyAimAssist(Vector3 direction)
        {
            if (!_enableAimAssist || _targetingController == null || _targetingController.CurrentTarget == null)
                return direction;

            Vector3 from = _characterMotor != null ? _characterMotor.transform.position : transform.position;
            Vector3 toTarget = _targetingController.CurrentTarget.transform.position - from;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance > _aimAssistMaxDistance || distance <= 0.01f)
                return direction;

            Vector3 targetDir = toTarget.normalized;
            float angle = Vector3.Angle(direction, targetDir);
            if (angle > _aimAssistMaxAngle)
                return direction;

            float assistT = Mathf.Clamp01(_aimAssistStrength * (1f - angle / Mathf.Max(1f, _aimAssistMaxAngle)));
            return Vector3.Slerp(direction, targetDir, assistT).normalized;
        }

        private void HandleChatPressed()
        {
            if (_chatPanelPresenter == null)
            {
                _view.SetStatusToast("Chat panel not configured.");
                return;
            }

            _panelCoordinator?.ToggleExclusive("chat");
            _view.SetStatusToast(_chatPanelPresenter.IsVisible ? "Chat opened." : "Chat closed.");
        }

        private void HandleInventoryPressed()
        {
            if (_inventoryPanelPresenter == null)
            {
                Debug.LogWarning("[MobileHUD] Inventory panel not found.");
                return;
            }

            _panelCoordinator?.ToggleExclusive("inventory");
            _view.SetStatusToast(_inventoryPanelPresenter.IsVisible ? "Inventory opened." : "Inventory closed.");
        }

        private void HandleCharacterPressed()
        {
            if (_equipmentPanelPresenter != null)
            {
                _panelCoordinator?.ToggleExclusive("equipment");
                _view.SetStatusToast(_equipmentPanelPresenter.IsVisible
                    ? $"Equipment opened ({_equipmentSystem?.Equipped.Count ?? 0} equipped)."
                    : "Equipment closed.");
                return;
            }

            if (_characterPanel != null)
            {
                _characterPanel.ToggleVisible();
                _view.SetStatusToast($"Character panel {(_characterPanel.IsVisible ? "opened" : "closed")}. Equipped: {_equipmentSystem?.Equipped.Count ?? 0}");
                return;
            }

            _view.SetStatusToast("Equipment panel requested.");
        }

        private void HandleMinimapPressed()
        {
            HandleMapPressed();
        }

        private void HandleMapPressed()
        {
            if (_mapPanel == null)
            {
                _view.SetStatusToast("Map panel requested.");
                return;
            }

            _mapPanel.ToggleVisible();
            _view.SetStatusToast($"Map panel {(_mapPanel.IsVisible ? "opened" : "closed")}." );
        }

        private void InitializePanelPresenters()
        {
            _panelCoordinator = new HudPanelCoordinator();

            if (_inventoryView != null)
            {
                _inventoryPanelPresenter = new InventoryPanelPresenter(_inventoryView);
                _panelCoordinator.Register("inventory", () => _inventoryPanelPresenter.IsVisible, visible => _inventoryPanelPresenter.SetVisible(visible));
            }

            if (_equipmentPanelView != null)
            {
                _equipmentPanelPresenter = new EquipmentPanelPresenter(_equipmentPanelView);
                _panelCoordinator.Register("equipment", () => _equipmentPanelPresenter.IsVisible, visible => _equipmentPanelPresenter.SetVisible(visible));
            }

            if (_chatView != null)
            {
                _chatPanelPresenter = new ChatPanelPresenter(_chatView);
                _panelCoordinator.Register("chat", () => _chatPanelPresenter.IsVisible, visible => _chatPanelPresenter.SetVisible(visible));
            }

            EnsureInventoryEquipmentPresenters();
            EnsureChatPresenter();

            _panelCoordinator.CloseAll();
        }

        private void EnsureInventoryEquipmentPresenters()
        {
            if (_inventoryPresenter != null || _equipmentPresenter != null)
                return;

            if (FindAnyObjectByType<InventoryFlowController>() != null)
                return;

            if (_inventoryView == null && _equipmentPanelView == null)
                return;

            if (_inventorySystem == null)
            {
                _inventorySystem = new InventoryClientSystem(catalogResolver: GameContext.CatalogResolver);
                GameContext.RegisterSystem(_inventorySystem);
            }

            if (_equipmentSystem == null)
            {
                _equipmentSystem = new EquipmentClientSystem(GameContext.CatalogResolver);
                GameContext.RegisterSystem(_equipmentSystem);
            }

            var transport = new MockInventoryEquipmentTransport(_inventorySystem, _equipmentSystem);
            _inventoryEquipmentService = new InventoryEquipmentService(
                _inventorySystem,
                _equipmentSystem,
                _statsSystem,
                GameContext.CatalogResolver,
                transport);

            if (_characterSessionSystem != null)
            {
                string characterId = _characterSessionSystem.Snapshot.SelectedCharacterId > 0
                    ? _characterSessionSystem.Snapshot.SelectedCharacterId.ToString()
                    : "char-local";
                _inventoryEquipmentService.SetCharacterId(characterId);
            }

            if (_inventoryView != null)
            {
                _inventoryPresenter = new InventoryPresenter(
                    _inventoryView,
                    _inventoryEquipmentService,
                    lootPickup: null,
                    _inventorySystem,
                    _equipmentSystem,
                    GameContext.CatalogResolver);
            }

            if (_equipmentPanelView != null)
            {
                _equipmentPresenter = new EquipmentPresenter(
                    _equipmentPanelView,
                    _equipmentSystem,
                    _inventoryEquipmentService,
                    GameContext.CatalogResolver);
            }
        }

        private void EnsureChatPresenter()
        {
            if (_chatPresenter != null || _chatView == null)
                return;

            if (FindAnyObjectByType<ChatFlowController>() != null)
                return;

            if (!GameContext.TryGetSystem(out ChatClientSystem chatSystem) || chatSystem == null)
            {
                chatSystem = new ChatClientSystem();
                GameContext.RegisterSystem(chatSystem);
            }

            if (!chatSystem.HasTransport)
            {
                _fallbackChatTransport = new MockChatTransport();
                chatSystem.AttachTransport(_fallbackChatTransport);
            }

            if (string.IsNullOrWhiteSpace(chatSystem.LocalPlayerName) && _characterSessionSystem != null)
            {
                chatSystem.LocalPlayerName = string.IsNullOrWhiteSpace(_characterSessionSystem.Snapshot.SelectedCharacterName)
                    ? "Player"
                    : _characterSessionSystem.Snapshot.SelectedCharacterName;
            }

            _chatPresenter = new ChatPresenter(_chatView, chatSystem);
        }

        private void RefreshSkillStripFromSkillBook()
        {
            if (_view == null)
                return;

            if (_skillBookSystem == null || _skillBookSystem.ActiveSkillBar == null || _skillBookSystem.ActiveSkillBar.Count == 0)
            {
                if (_combatController == null)
                    return;

                for (int i = 0; i < _combatController.SkillCount; i++)
                {
                    if (_combatController.TryGetSkillModel(i, out CombatSkillModel skill))
                        _view.SetSkillName(skill.SkillId, skill.DisplayName);
                }

                return;
            }

            for (int i = 0; i < _skillBookSystem.ActiveSkillBar.Count; i++)
            {
                int skillId = _skillBookSystem.ActiveSkillBar[i];
                if (skillId <= 0)
                    continue;

                string displayName = ResolveSkillDisplayName(skillId);
                _view.SetSkillName(skillId, displayName);
            }
        }

        private string ResolveSkillDisplayName(int skillId)
        {
            if (_combatController != null)
            {
                for (int i = 0; i < _combatController.SkillCount; i++)
                {
                    if (!_combatController.TryGetSkillModel(i, out CombatSkillModel model))
                        continue;

                    if (model.SkillId == skillId && !string.IsNullOrWhiteSpace(model.DisplayName))
                        return model.DisplayName;
                }
            }

            if (_skillBookSystem != null && _skillBookSystem.TryGetSkill(skillId, out SkillBookClientSystem.SkillEntry skill))
                return $"S{skillId} Lv.{Mathf.Max(1, skill.Level)}";

            return $"S{skillId}";
        }

        private void RefreshPotionQuickSlots()
        {
            _hpPotionSlot = ResolvePotionForQuickSlot(_hpPotionSlot, requireHpRestore: true, requireManaRestore: false, _defaultHpPotionLabel);
            _mpPotionSlot = ResolvePotionForQuickSlot(_mpPotionSlot, requireHpRestore: false, requireManaRestore: true, _defaultMpPotionLabel);
        }

        private PotionQuickSlotState ResolvePotionForQuickSlot(
            PotionQuickSlotState previous,
            bool requireHpRestore,
            bool requireManaRestore,
            string defaultLabel)
        {
            var result = previous;
            result.InventorySlotIndex = -1;
            result.ItemId = 0;
            result.Count = 0;
            result.Label = defaultLabel;

            if (_inventorySystem == null || GameContext.CatalogResolver == null)
                return result;

            InventoryClientSystem.QuickSlotKind quickKind = requireHpRestore
                ? InventoryClientSystem.QuickSlotKind.HpPotion
                : InventoryClientSystem.QuickSlotKind.MpPotion;

            if (_inventorySystem.TryGetQuickSlot(quickKind, out int configuredSlotIndex)
                && _inventorySystem.TryGetSlot(configuredSlotIndex, out InventoryClientSystem.InventorySlot configuredSlot)
                && !configuredSlot.IsEmpty)
            {
                if (GameContext.CatalogResolver.TryGetItemDefinition(configuredSlot.ItemId, out ItemDefinition configuredItem)
                    && configuredItem != null
                    && configuredItem.Category == ItemCategory.Consumable)
                {
                    bool hpOk = !requireHpRestore || configuredItem.Restore.Hp > 0;
                    bool mpOk = !requireManaRestore || configuredItem.Restore.Mana > 0;
                    if (hpOk && mpOk)
                    {
                        result.InventorySlotIndex = configuredSlotIndex;
                        result.ItemId = configuredItem.ItemId;
                        result.Count = Mathf.Max(0, configuredSlot.Quantity);
                        result.Label = string.IsNullOrWhiteSpace(configuredItem.Name) ? defaultLabel : configuredItem.Name;
                        return result;
                    }
                }
            }

            IReadOnlyList<InventoryClientSystem.InventorySlot> slots = _inventorySystem.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventoryClientSystem.InventorySlot slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                if (!GameContext.CatalogResolver.TryGetItemDefinition(slot.ItemId, out ItemDefinition item) || item == null)
                    continue;

                if (item.Category != ItemCategory.Consumable)
                    continue;

                bool hasHp = item.Restore.Hp > 0;
                bool hasMana = item.Restore.Mana > 0;

                if (requireHpRestore && !hasHp)
                    continue;

                if (requireManaRestore && !hasMana)
                    continue;

                result.InventorySlotIndex = slot.SlotIndex;
                result.ItemId = item.ItemId;
                result.Count = Mathf.Max(0, slot.Quantity);
                result.Label = string.IsNullOrWhiteSpace(item.Name) ? defaultLabel : item.Name;
                return result;
            }

            return result;
        }

        private void ApplyPotionQuickSlotView()
        {
            float hpRemaining = Mathf.Max(0f, _hpPotionSlot.CooldownEndTime - Time.unscaledTime);
            float mpRemaining = Mathf.Max(0f, _mpPotionSlot.CooldownEndTime - Time.unscaledTime);

            _view.SetPotionQuickSlot(
                _hpPotionSlot.SlotId,
                _hpPotionSlot.Label,
                _hpPotionSlot.Count,
                hpRemaining,
                _potionCooldownSeconds,
                _hpPotionSlot.InventorySlotIndex >= 0);

            _view.SetPotionQuickSlot(
                _mpPotionSlot.SlotId,
                _mpPotionSlot.Label,
                _mpPotionSlot.Count,
                mpRemaining,
                _potionCooldownSeconds,
                _mpPotionSlot.InventorySlotIndex >= 0);
        }

        private void TryUsePotionQuickSlot(ref PotionQuickSlotState slotState)
        {
            if (_inventorySystem == null || _statsSystem == null || GameContext.CatalogResolver == null)
            {
                _view.SetStatusToast("Potion system unavailable.");
                return;
            }

            float remaining = slotState.CooldownEndTime - Time.unscaledTime;
            if (remaining > 0f)
                return;

            if (slotState.InventorySlotIndex < 0)
            {
                _view.SetStatusToast("No potion assigned to quick slot.");
                return;
            }

            if (!_inventorySystem.TryGetSlot(slotState.InventorySlotIndex, out InventoryClientSystem.InventorySlot inventorySlot) || inventorySlot.IsEmpty)
            {
                RefreshPotionQuickSlots();
                ApplyPotionQuickSlotView();
                _view.SetStatusToast("Potion slot is empty.");
                return;
            }

            if (!GameContext.CatalogResolver.TryGetItemDefinition(inventorySlot.ItemId, out ItemDefinition item) || item == null)
            {
                _view.SetStatusToast("Potion definition missing.");
                return;
            }

            if (!_inventorySystem.TryConsumeFromSlot(inventorySlot.SlotIndex, 1, out _, out string consumeError))
            {
                _view.SetStatusToast(string.IsNullOrWhiteSpace(consumeError) ? "Could not use potion." : consumeError);
                return;
            }

            ApplyRestore(item.Restore);

            slotState.CooldownEndTime = Time.unscaledTime + Mathf.Max(0.25f, _potionCooldownSeconds);
            RefreshPotionQuickSlots();
            ApplyPotionQuickSlotView();
            _view.SetStatusToast($"Used {slotState.Label}.");
        }

        private void ApplyRestore(ItemRestoreEffect restore)
        {
            if (_statsSystem == null)
                return;

            StatsClientSystem.PlayerStatsSnapshot snapshot = _statsSystem.Snapshot;
            int hpTarget = snapshot.Resources.Hp.Current + Mathf.Max(0, restore.Hp);
            int manaTarget = snapshot.Resources.Mana.Current + Mathf.Max(0, restore.Mana);

            _statsSystem.ApplyDelta(new StatsClientSystem.PlayerStatsDelta
            {
                HasHp = true,
                HpCurrent = hpTarget,
                HpMax = snapshot.Resources.Hp.Max,
                HasMana = true,
                ManaCurrent = manaTarget,
                ManaMax = snapshot.Resources.Mana.Max
            });
        }

        private void TickPotionCooldowns()
        {
            bool anyCooldown = _hpPotionSlot.CooldownEndTime > Time.unscaledTime
                || _mpPotionSlot.CooldownEndTime > Time.unscaledTime;

            if (!anyCooldown)
                return;

            ApplyPotionQuickSlotView();
        }

        private void TickTargetPortraitRefresh()
        {
            if (_targetingController == null || _targetingController.CurrentTarget == null)
                return;

            if (Time.unscaledTime < _nextTargetHudRefreshAt)
                return;

            _nextTargetHudRefreshAt = Time.unscaledTime + 0.2f;
            ApplyTargetPortrait(_targetingController.CurrentTarget);
        }

        private void ApplyTargetPortrait(EntityView target)
        {
            if (target == null)
            {
                _view.SetTargetPortrait("No target", 0, 0, 0, false);
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(target.name) ? "Target" : target.name;
            int level = 1;
            int hpCurrent = 100;
            int hpMax = 100;

            TargetHudRuntimeData targetHudData = target.GetComponent<TargetHudRuntimeData>();
            if (targetHudData != null)
            {
                displayName = targetHudData.DisplayName;
                level = targetHudData.Level;
                hpCurrent = targetHudData.HpCurrent;
                hpMax = targetHudData.HpMax;
            }

            _view.SetTargetPortrait(displayName, level, hpCurrent, hpMax, true);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawDebugGizmos)
                return;

            Vector3 origin = _characterMotor != null ? _characterMotor.transform.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + _lastResolvedMoveDirection * 2.2f);

            if (_targetingController == null || _targetingController.CurrentTarget == null)
                return;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, _targetingController.CurrentTarget.transform.position);
        }
    }
}
