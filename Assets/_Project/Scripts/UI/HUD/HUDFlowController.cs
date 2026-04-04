using MuLike.Gameplay.Controllers;
using MuLike.Networking;
using UnityEngine;
using MuLike.Systems;

namespace MuLike.UI.HUD
{
    /// <summary>
    /// Scene composition root for HUD MVP wiring.
    /// </summary>
    public class HUDFlowController : MonoBehaviour
    {
        [SerializeField] private HUDView _view;
        [SerializeField] private string _characterName = "Player";
        [SerializeField] private bool _enableDebugConsole = true;

        [Header("Optional Explicit Dependencies")]
        [SerializeField] private TargetingController _targetingController;
        [SerializeField] private NetworkGameClient _networkClient;

        private HUDPresenter _presenter;

        private void Awake()
        {
            if (_view == null)
                _view = FindObjectOfType<HUDView>();

            if (_targetingController == null)
                _targetingController = FindObjectOfType<TargetingController>();

            if (_networkClient == null)
                _networkClient = FindObjectOfType<NetworkGameClient>();

            if (_view == null)
            {
                Debug.LogError("[HUDFlowController] HUDView missing.");
                enabled = false;
                return;
            }

            StatsClientSystem stats = HUDPresenter.ResolveOrCreateStatsSystem();
            InventoryClientSystem inventory = HUDPresenter.ResolveOrCreateInventorySystem();
            EquipmentClientSystem equipment = HUDPresenter.ResolveOrCreateEquipmentSystem();
            ConsumableClientSystem consumable = HUDPresenter.ResolveOrCreateConsumableSystem(inventory, stats);

            var deps = new HUDPresenter.Dependencies
            {
                View = _view,
                StatsSystem = stats,
                InventorySystem = inventory,
                EquipmentSystem = equipment,
                ConsumableSystem = consumable,
                TargetingController = _targetingController,
                NetworkClient = _networkClient
            };

            _presenter = new HUDPresenter(deps, _characterName, _enableDebugConsole);
        }

        private void OnEnable()
        {
            _presenter?.Bind();
        }

        private void OnDisable()
        {
            _presenter?.Unbind();
        }
    }
}
