using System;
using System.Threading.Tasks;
using MuLike.ContentPipeline.Runtime;
using MuLike.Core;
using MuLike.Data.DTO;
using MuLike.Gameplay.Controllers;
using MuLike.Networking;
using MuLike.Systems;
using MuLike.UI.CharacterSelect;
using MuLike.UI.HUD;
using MuLike.UI.MobileHUD;
using MuLike.UI.Login;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MuLike.Bootstrap
{
    public interface ISceneFlowCoordinator
    {
        event Action OnLoginReady;
        event Action OnWorldReady;

        void ConfigureForScene(Scene scene);
        void GoToLogin();
        void GoToCharacterSelect();
        void GoToWorld(string preferredScene);
    }

    /// <summary>
    /// Coordinates scene transitions and scene-level presenter wiring.
    /// </summary>
    public sealed class SceneFlowCoordinator : ISceneFlowCoordinator
    {
        private readonly IClientServiceRegistry _registry;
        private readonly ClientBootstrap.SceneFlowSettings _settings;
        private readonly IContentAddressablesService _contentService;

        private LoginPresenter _loginPresenter;
        private CharacterSelectPresenter _characterSelectPresenter;
        private HUDPresenter _hudPresenter;

        public event Action OnLoginReady;
        public event Action OnWorldReady;

        public SceneFlowCoordinator(IClientServiceRegistry registry, ClientBootstrap.SceneFlowSettings settings)
        {
            _registry = registry;
            _settings = settings;
            _registry.TryResolve(out _contentService);
        }

        public void ConfigureForScene(Scene scene)
        {
            TearDownScenePresenters();

            string sceneName = scene.name;
            if (string.Equals(sceneName, _settings.LoginSceneName, StringComparison.Ordinal))
            {
                ConfigureLoginScene();
                return;
            }

            if (string.Equals(sceneName, _settings.CharacterSelectSceneName, StringComparison.Ordinal))
            {
                ConfigureCharacterSelectScene();
                return;
            }

            if (IsWorldScene(sceneName))
                ConfigureWorldScene();
        }

        public void GoToLogin()
        {
            if (string.IsNullOrWhiteSpace(_settings.LoginSceneName))
            {
                Debug.LogError("[SceneFlowCoordinator] Login scene name is empty.");
                return;
            }

            LoadScene(_settings.LoginSceneName);
        }

        public void GoToCharacterSelect()
        {
            if (string.IsNullOrWhiteSpace(_settings.CharacterSelectSceneName))
            {
                Debug.LogError("[SceneFlowCoordinator] CharacterSelect scene name is empty.");
                return;
            }

            LoadScene(_settings.CharacterSelectSceneName);
        }

        public void GoToWorld(string preferredScene)
        {
            string target = string.IsNullOrWhiteSpace(preferredScene)
                ? _settings.DefaultWorldSceneName
                : preferredScene;

            if (string.IsNullOrWhiteSpace(target))
            {
                Debug.LogError("[SceneFlowCoordinator] World scene name is empty.");
                return;
            }

            LoadScene(target);
        }

        private void ConfigureLoginScene()
        {
            _ = PreloadLoginDependenciesAsync();

            LoginView view = UnityEngine.Object.FindObjectOfType<LoginView>();
            if (view == null)
            {
                Debug.LogWarning("[SceneFlowCoordinator] LoginView not found. Login scene presenters were not wired.");
                return;
            }

            if (!_registry.TryResolve(out NetworkGameClient networkClient) || networkClient == null)
            {
                Debug.LogError("[SceneFlowCoordinator] NetworkGameClient is missing. Login scene cannot be wired.");
                return;
            }

            LoginFlowController legacy = UnityEngine.Object.FindObjectOfType<LoginFlowController>();
            if (legacy != null)
                legacy.enabled = false;

            if (_registry.TryResolve(out IClientSessionState sessionState))
            {
                // Login scene is authoritative for auth lifecycle.
                sessionState.MarkAuthenticated(false);
                sessionState.ClearCharacterSelection();
            }

            _loginPresenter = new LoginPresenter(view, networkClient, GoToCharacterSelect);
            _loginPresenter.Bind();
            OnLoginReady?.Invoke();

            Debug.Log("[SceneFlowCoordinator] Login scene configured.");
        }

        private void ConfigureCharacterSelectScene()
        {
            CharacterSelectView view = UnityEngine.Object.FindObjectOfType<CharacterSelectView>();
            if (view == null)
            {
                Debug.LogWarning("[SceneFlowCoordinator] CharacterSelectView not found. CharacterSelect presenters were not wired.");
                return;
            }

            CharacterSelectFlowController legacy = UnityEngine.Object.FindObjectOfType<CharacterSelectFlowController>();
            if (legacy != null)
                legacy.enabled = false;

            var mockService = new MockCharacterSelectService();
            ICharacterSelectService service = mockService;

            if (_registry.TryResolve(out NetworkGameClient networkClient) && networkClient != null)
                service = new NetworkCharacterSelectService(networkClient, mockService);
            else
                Debug.LogWarning("[SceneFlowCoordinator] NetworkGameClient missing. CharacterSelect will run using mock service only.");

            _characterSelectPresenter = new CharacterSelectPresenter(
                view,
                service,
                (result, selectedCharacter) =>
                {
                    if (_registry.TryResolve(out IClientSessionState sessionState))
                    {
                        sessionState.MarkAuthenticated(true);
                        sessionState.SetWorldEntry(
                            result != null ? result.characterId : 0,
                            result != null ? result.mapId : 0,
                            result != null ? result.sceneName : string.Empty);
                    }

                    if (_registry.TryResolve(out NetworkGameClient client) && result != null && result.characterId > 0)
                        client.SetLocalPlayerEntityId(result.characterId);

                    FrontendFlowDirector flow = FrontendFlowDirector.Instance;
                    if (flow != null && result != null)
                        flow.SetSelectedCharacter(result.characterId, selectedCharacter != null ? selectedCharacter.name : string.Empty);

                    string sceneName = result != null ? result.sceneName : string.Empty;
                    GoToWorld(sceneName);
                });

            _characterSelectPresenter.Bind();
            Debug.Log("[SceneFlowCoordinator] CharacterSelect scene configured.");
        }

        private void ConfigureWorldScene()
        {
            _ = PreloadWorldHudDependenciesAsync();

            EnsureWorldVerticalSliceInstaller();

            HUDView hudView = UnityEngine.Object.FindObjectOfType<HUDView>();
            MobileHudController mobileHud = UnityEngine.Object.FindObjectOfType<MobileHudController>();
            if (hudView == null)
            {
                if (mobileHud != null)
                {
                    Debug.Log("[SceneFlowCoordinator] Mobile HUD detected. Legacy HUD presenter wiring skipped.");
                }
                else
                {
                    Debug.Log("[SceneFlowCoordinator] No HUDView found in world scene. HUD presenter wiring skipped.");
                }

                OnWorldReady?.Invoke();
                return;
            }

            HUDFlowController legacy = UnityEngine.Object.FindObjectOfType<HUDFlowController>();
            if (legacy != null)
                legacy.enabled = false;

            if (!_registry.TryResolve(out StatsClientSystem stats)
                || !_registry.TryResolve(out InventoryClientSystem inventory)
                || !_registry.TryResolve(out EquipmentClientSystem equipment)
                || !_registry.TryResolve(out ConsumableClientSystem consumable))
            {
                Debug.LogError("[SceneFlowCoordinator] One or more gameplay systems are missing. HUD presenter cannot be created.");
                return;
            }

            _registry.TryResolve(out NetworkGameClient networkClient);
            TargetingController targeting = UnityEngine.Object.FindObjectOfType<TargetingController>();

            var dependencies = new HUDPresenter.Dependencies
            {
                View = hudView,
                StatsSystem = stats,
                InventorySystem = inventory,
                EquipmentSystem = equipment,
                ConsumableSystem = consumable,
                TargetingController = targeting,
                NetworkClient = networkClient
            };

            _hudPresenter = new HUDPresenter(dependencies, _settings.DefaultCharacterName, _settings.EnableHudDebugConsole);
            _hudPresenter.Bind();
            OnWorldReady?.Invoke();

            Debug.Log("[SceneFlowCoordinator] World scene configured.");
        }

        private static void EnsureWorldVerticalSliceInstaller()
        {
            WorldVerticalSliceInstaller installer = UnityEngine.Object.FindObjectOfType<WorldVerticalSliceInstaller>();
            if (installer != null)
            {
                installer.Install();
                return;
            }

            var go = new GameObject("WorldVerticalSliceInstaller");
            installer = go.AddComponent<WorldVerticalSliceInstaller>();
            installer.Install();
        }

        private void TearDownScenePresenters()
        {
            _loginPresenter?.Unbind();
            _loginPresenter = null;

            _characterSelectPresenter?.Unbind();
            _characterSelectPresenter = null;

            _hudPresenter?.Unbind();
            _hudPresenter = null;
        }

        private static void LoadScene(string sceneName)
        {
            if (SceneController.Instance != null)
            {
                SceneController.Instance.LoadScene(sceneName);
                return;
            }

            SceneController.EnsureInstance().LoadScene(sceneName, "Login");
        }

        private bool IsWorldScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            if (string.Equals(sceneName, _settings.DefaultWorldSceneName, StringComparison.Ordinal))
                return true;

            if (string.Equals(sceneName, _settings.TownSceneName, StringComparison.Ordinal))
                return true;

            if (string.Equals(sceneName, _settings.FieldSceneName, StringComparison.Ordinal))
                return true;

            return false;
        }

        private async Task PreloadLoginDependenciesAsync()
        {
            if (_contentService == null)
            {
                Debug.LogWarning("[SceneFlowCoordinator] ContentAddressablesService unavailable. Login preload skipped.");
                return;
            }

            try
            {
                await _contentService.PreloadGroupAsync(
                    groupName: "login.dependencies",
                    labels: ContentAddressablesLabels.LoginPreload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneFlowCoordinator] Login dependency preload failed: {ex.Message}");
            }
        }

        private async Task PreloadWorldHudDependenciesAsync()
        {
            if (_contentService == null)
            {
                Debug.LogWarning("[SceneFlowCoordinator] ContentAddressablesService unavailable. World HUD preload skipped.");
                return;
            }

            try
            {
                await _contentService.PreloadGroupAsync(
                    groupName: "world.hud.dependencies",
                    labels: ContentAddressablesLabels.WorldHudPreload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneFlowCoordinator] World HUD dependency preload failed: {ex.Message}");
            }
        }
    }
}
