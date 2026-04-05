using MuLike.Bootstrap;
using MuLike.Core;
using MuLike.Data.DTO;
using MuLike.Networking;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Composition root for character select scene.
    /// Accepts injected SessionStateClient and ClientFlowFeedbackService from LoginFlowController.
    /// </summary>
    public class CharacterSelectFlowController : MonoBehaviour
    {
        [SerializeField] private CharacterSelectView _view;
        [SerializeField] private NetworkGameClient _networkClient;

        [Header("Runtime Service")]
        [SerializeField] private bool _useMockService = true;

        [Header("Navigation")]
        [SerializeField] private bool _loadSceneOnEnterWorld = true;
        [SerializeField] private string _fallbackWorldSceneName = "World_Dev";
        [SerializeField] private string _townSceneName = "Town_01";

        [Header("Session Integration")]
        [SerializeField] private FrontendFlowDirector _frontendFlowDirector;

        private ICharacterSelectService _service;
        private CharacterSelectPresenter _presenter;
        private SessionStateClient _sessionStateClient;
        private ClientFlowFeedbackService _feedbackService;

        /// <summary>
        /// Inject session state and feedback services from LoginFlowController.
        /// </summary>
        public void InjectSessionServices(SessionStateClient sessionStateClient, ClientFlowFeedbackService feedbackService)
        {
            _sessionStateClient = sessionStateClient;
            _feedbackService = feedbackService;
        }

        private void Awake()
        {
            if (_view == null)
                _view = FindAnyObjectByType<CharacterSelectView>();

            if (_networkClient == null)
                _networkClient = FindAnyObjectByType<NetworkGameClient>();

            if (_frontendFlowDirector == null)
                _frontendFlowDirector = FrontendFlowDirector.EnsureInstance();

            if (_view == null)
            {
                Debug.LogError("[CharacterSelectFlowController] CharacterSelectView is missing in scene.");
                enabled = false;
                return;
            }

            _service = BuildService();

            // Ensure session state and feedback services exist
            if (_sessionStateClient == null)
                _sessionStateClient = new SessionStateClient();
            if (_feedbackService == null)
                _feedbackService = new ClientFlowFeedbackService();

            _presenter = new CharacterSelectPresenter(
                _view,
                _service,
                HandleEnterWorldAccepted,
                _sessionStateClient,
                _feedbackService);
        }

        private void OnEnable()
        {
            _presenter?.Bind();
        }

        private void OnDisable()
        {
            _presenter?.Unbind();
        }

        private ICharacterSelectService BuildService()
        {
            var mock = new MockCharacterSelectService();

            if (_useMockService || _networkClient == null)
            {
                Debug.Log("[CharacterSelectFlowController] Using MockCharacterSelectService.");
                return mock;
            }

            Debug.Log("[CharacterSelectFlowController] Using NetworkCharacterSelectService with mock fallback.");
            return new NetworkCharacterSelectService(_networkClient, mock);
        }

        private void HandleEnterWorldAccepted(EnterWorldResultDto result, CharacterSummaryDto selectedCharacter)
        {
            if (result == null)
                return;

            if (_networkClient != null && result.characterId > 0)
                _networkClient.SetLocalPlayerEntityId(result.characterId);

            // Update SessionStateClient with character and world scene
            if (_sessionStateClient != null)
            {
                _sessionStateClient.SetCharacter(result.characterId);
                string sceneToLoad = !string.IsNullOrWhiteSpace(result.sceneName)
                    ? result.sceneName
                    : ResolveSceneByMap(result.mapId);
                _sessionStateClient.SetWorldScene(sceneToLoad);
                _sessionStateClient.TryTransitionTo(ClientSessionState.EnteringWorld);
            }

            if (_frontendFlowDirector != null)
            {
                _frontendFlowDirector.SetSelectedCharacter(
                    result.characterId,
                    selectedCharacter != null ? selectedCharacter.name : string.Empty);
            }

            if (GameContext.TryGetSystem(out CharacterSessionSystem sessionSystem))
            {
                sessionSystem.ApplyDelta(new CharacterSessionSystem.SessionDelta
                {
                    HasAuthentication = true,
                    IsAuthenticated = true,
                    AccountId = sessionSystem.Snapshot.AccountId,
                    SessionToken = sessionSystem.Snapshot.SessionToken,
                    HasCharacterSelection = true,
                    SelectedCharacterId = result.characterId,
                    SelectedCharacterName = selectedCharacter != null ? selectedCharacter.name : string.Empty,
                    HasLastWorldScene = true,
                    LastWorldScene = !string.IsNullOrWhiteSpace(result.sceneName)
                        ? result.sceneName
                        : ResolveSceneByMap(result.mapId)
                });
            }

            if (!_loadSceneOnEnterWorld)
                return;

            string sceneToLoad2 = !string.IsNullOrWhiteSpace(result.sceneName)
                ? result.sceneName
                : _fallbackWorldSceneName;

            if (string.IsNullOrWhiteSpace(result.sceneName))
            {
                sceneToLoad2 = ResolveSceneByMap(result.mapId);
            }

            if (string.IsNullOrWhiteSpace(sceneToLoad2))
            {
                Debug.LogWarning("[CharacterSelectFlowController] Target scene is empty.");
                return;
            }

            if (_frontendFlowDirector != null)
            {
                _frontendFlowDirector.EnterWorld(sceneToLoad2);
                return;
            }

            if (SceneController.Instance != null)
            {
                SceneController.Instance.LoadScene(sceneToLoad2);
                return;
            }

            SceneController.EnsureInstance().LoadScene(sceneToLoad2, _fallbackWorldSceneName);
        }

        private string ResolveSceneByMap(int mapId)
        {
            // Suggested map routing for current MVP maps.
            if (mapId == 1)
                return _townSceneName;

            return _fallbackWorldSceneName;
        }
    }
}
