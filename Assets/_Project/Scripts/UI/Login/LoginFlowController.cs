using MuLike.Core;
using MuLike.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MuLike.UI.Login
{
    /// <summary>
    /// Scene composition root for the MVP login flow.
    /// </summary>
    public class LoginFlowController : MonoBehaviour
    {
        [SerializeField] private LoginView _view;
        [SerializeField] private NetworkGameClient _networkClient;

        [Header("Navigation")]
        [SerializeField] private bool _loadSceneOnSuccess = false;
        [SerializeField] private string _worldSceneName = "World_Dev";

        private LoginPresenter _presenter;

        private void Awake()
        {
            if (_view == null)
                _view = FindObjectOfType<LoginView>();

            if (_networkClient == null)
                _networkClient = FindObjectOfType<NetworkGameClient>();

            if (_view == null)
            {
                Debug.LogError("[LoginFlowController] LoginView is missing in scene.");
                enabled = false;
                return;
            }

            if (_networkClient == null)
            {
                Debug.LogError("[LoginFlowController] NetworkGameClient is missing in scene.");
                _view.SetStatus("Network client missing. Check scene setup.");
                enabled = false;
                return;
            }

            _presenter = new LoginPresenter(_view, _networkClient, HandleLoginSuccess);
        }

        private void OnEnable()
        {
            _presenter?.Bind();
        }

        private void OnDisable()
        {
            _presenter?.Unbind();
        }

        private void HandleLoginSuccess()
        {
            if (!_loadSceneOnSuccess)
                return;

            if (string.IsNullOrWhiteSpace(_worldSceneName))
            {
                Debug.LogWarning("[LoginFlowController] World scene name is empty.");
                return;
            }

            if (SceneController.Instance != null)
            {
                SceneController.Instance.LoadScene(_worldSceneName);
                return;
            }

            SceneManager.LoadScene(_worldSceneName);
        }
    }
}
