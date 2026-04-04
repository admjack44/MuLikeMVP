using MuLike.Core;
using MuLike.Data.DTO;
using MuLike.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Composition root for character select scene.
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

        private ICharacterSelectService _service;
        private CharacterSelectPresenter _presenter;

        private void Awake()
        {
            if (_view == null)
                _view = FindObjectOfType<CharacterSelectView>();

            if (_networkClient == null)
                _networkClient = FindObjectOfType<NetworkGameClient>();

            if (_view == null)
            {
                Debug.LogError("[CharacterSelectFlowController] CharacterSelectView is missing in scene.");
                enabled = false;
                return;
            }

            _service = BuildService();
            _presenter = new CharacterSelectPresenter(_view, _service, HandleEnterWorldAccepted);
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

        private void HandleEnterWorldAccepted(EnterWorldResultDto result)
        {
            if (!_loadSceneOnEnterWorld)
                return;

            string sceneToLoad = !string.IsNullOrWhiteSpace(result.sceneName)
                ? result.sceneName
                : _fallbackWorldSceneName;

            if (string.IsNullOrWhiteSpace(sceneToLoad))
            {
                Debug.LogWarning("[CharacterSelectFlowController] Target scene is empty.");
                return;
            }

            if (SceneController.Instance != null)
            {
                SceneController.Instance.LoadScene(sceneToLoad);
                return;
            }

            SceneManager.LoadScene(sceneToLoad);
        }
    }
}
