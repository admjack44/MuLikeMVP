using MuLike.Gameplay.Combat;
using MuLike.Gameplay.Controllers;
using MuLike.Networking;
using MuLike.Performance.Rendering;
using MuLike.UI.Inventory;
using MuLike.UI.MobileHUD;
using UnityEngine;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Minimal world-scene installer for the mobile MMORPG vertical slice.
    /// Ensures essential runtime links exist without forcing scene rewrites.
    /// </summary>
    public sealed class WorldVerticalSliceInstaller : MonoBehaviour
    {
        [SerializeField] private bool _runOnStart = true;
        [SerializeField] private bool _logSummary = true;
        [SerializeField] private bool _ensureMobileCameraCulling = true;
        [SerializeField] private bool _ensureMobileUrpQuality = true;

        private void Start()
        {
            if (!_runOnStart)
                return;

            Install();
        }

        [ContextMenu("Install Vertical Slice Wiring")]
        public void Install()
        {
            NetworkGameClient network = FindAnyObjectByType<NetworkGameClient>();
            CharacterMotor motor = FindAnyObjectByType<CharacterMotor>();
            SnapshotSyncDriver snapshot = FindAnyObjectByType<SnapshotSyncDriver>();
            TargetingController targeting = FindAnyObjectByType<TargetingController>();
            CombatController combat = FindAnyObjectByType<CombatController>();
            MobileHudController hud = FindAnyObjectByType<MobileHudController>();
            InventoryFlowController inventory = FindAnyObjectByType<InventoryFlowController>();
            MobileHudView mobileHudView = FindAnyObjectByType<MobileHudView>();
            InventoryView inventoryView = FindAnyObjectByType<InventoryView>();
            Camera mainCamera = Camera.main;
            MobileCameraCullingConfigurator cullingConfigurator = mainCamera != null ? mainCamera.GetComponent<MobileCameraCullingConfigurator>() : null;
            MobileUrpQualityApplier qualityApplier = FindAnyObjectByType<MobileUrpQualityApplier>();
            IClientSessionState sessionState = ClientBootstrap.Instance != null
                ? ClientBootstrap.Instance.Services.ResolveOrNull<IClientSessionState>()
                : null;

            if (sessionState != null && network != null && sessionState.SelectedCharacterId > 0)
                network.SetLocalPlayerEntityId(sessionState.SelectedCharacterId);

            if (motor != null && targeting == null)
                targeting = motor.gameObject.AddComponent<TargetingController>();

            if (motor != null && combat == null)
                combat = motor.gameObject.AddComponent<CombatController>();

            if (snapshot == null)
            {
                var snapshotGo = new GameObject("SnapshotSyncDriver");
                snapshot = snapshotGo.AddComponent<SnapshotSyncDriver>();
            }

            if (mobileHudView != null && hud == null)
            {
                var hudGo = new GameObject("MobileHudController");
                hud = hudGo.AddComponent<MobileHudController>();
            }

            if (inventoryView != null && inventory == null)
            {
                var invGo = new GameObject("InventoryFlowController");
                inventory = invGo.AddComponent<InventoryFlowController>();
            }

            if (inventory != null && sessionState != null && sessionState.SelectedCharacterId > 0)
                inventory.SetCharacterIdRuntime(sessionState.SelectedCharacterId.ToString());

            if (snapshot != null && motor != null)
                snapshot.SetLocalPlayerTransform(motor.transform, motor);

            if (_ensureMobileCameraCulling && mainCamera != null && cullingConfigurator == null)
                cullingConfigurator = mainCamera.gameObject.AddComponent<MobileCameraCullingConfigurator>();

            if (_ensureMobileUrpQuality && qualityApplier == null)
                qualityApplier = gameObject.AddComponent<MobileUrpQualityApplier>();

            if (_logSummary)
            {
                Debug.Log(
                    "[WorldVerticalSliceInstaller] Wiring complete."
                    + $" Network={(network != null)}"
                    + $" Motor={(motor != null)}"
                    + $" Snapshot={(snapshot != null)}"
                    + $" Targeting={(targeting != null)}"
                    + $" Combat={(combat != null)}"
                    + $" HUD={(hud != null)}"
                    + $" Inventory={(inventory != null)}"
                    + $" CameraCulling={(cullingConfigurator != null)}"
                    + $" UrpQuality={(qualityApplier != null)}");
            }
        }
    }
}
