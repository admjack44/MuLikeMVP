using MuLike.Bootstrap;
using MuLike.Core;
using MuLike.Networking;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MuLike.UI.Login
{
    /// <summary>
    /// Scene composition root for the MVP login flow.
    /// Instantiates and coordinates LoginPresenter, LoginFlowService, and SessionStateClient.
    /// </summary>
    public class LoginFlowController : MonoBehaviour
    {
        [SerializeField] private LoginView _view;
        [SerializeField] private NetworkGameClient _networkClient;

        [Header("Navigation")]
        [SerializeField] private bool _loadSceneOnSuccess = true;
        [SerializeField] private string _characterSelectSceneName = "CharacterSelect";
        [SerializeField] private FrontendFlowDirector _frontendFlowDirector;

        private LoginPresenter _presenter;
        private ILoginFlowService _loginFlowService;
        private CancellationTokenSource _lifetimeCts;
        private bool _isRefreshInFlight;

        // Session state services (shared with CharacterSelectFlowController)
        private SessionStateClient _sessionStateClient;
        private ClientFlowFeedbackService _feedbackService;

        [Header("Flow")]
        [SerializeField] private int _connectTimeoutMs = 10000;
        [SerializeField] private int _requestTimeoutMs = 12000;
        [SerializeField] private int _refreshLeadSeconds = 90;

        [Header("Logout")]
        [SerializeField] private bool _disconnectSocketOnLogout = false;

        public event Action<bool, string> AuthenticatedChanged;

        /// <summary>
        /// Expose session state for other scenes or controllers.
        /// </summary>
        public SessionStateClient SessionStateClient => _sessionStateClient;
        public ClientFlowFeedbackService FeedbackService => _feedbackService;

        private void Awake()
        {
            if (_view == null)
                _view = FindObjectOfType<LoginView>();

            if (_networkClient == null)
                _networkClient = FindObjectOfType<NetworkGameClient>();

            if (_frontendFlowDirector == null)
                _frontendFlowDirector = FrontendFlowDirector.EnsureInstance();

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

            _lifetimeCts = new CancellationTokenSource();

            // Initialize session state and feedback services (can be reused in CharacterSelect scene)
            _sessionStateClient = new SessionStateClient();
            _feedbackService = new ClientFlowFeedbackService();

            var gateway = new NetworkGameClientLoginGateway(_networkClient);
            var sessionStore = new PlayerPrefsLoginSessionStore();
            _loginFlowService = new LoginFlowService(
                gateway,
                sessionStore,
                _requestTimeoutMs,
                _connectTimeoutMs,
                _refreshLeadSeconds);

            _presenter = new LoginPresenter(
                _view,
                _loginFlowService,
                HandleLoginSuccess,
                HandleLoggedOut,
                _sessionStateClient,
                _feedbackService);
        }

        private void OnEnable()
        {
            if (_lifetimeCts == null || _lifetimeCts.IsCancellationRequested)
            {
                _lifetimeCts?.Dispose();
                _lifetimeCts = new CancellationTokenSource();
            }

            _presenter?.Bind();
        }

        private void OnDisable()
        {
            _presenter?.Unbind();

            if (_lifetimeCts != null && !_lifetimeCts.IsCancellationRequested)
                _lifetimeCts.Cancel();
        }

        private void OnDestroy()
        {
            if (_lifetimeCts != null)
            {
                _lifetimeCts.Cancel();
                _lifetimeCts.Dispose();
                _lifetimeCts = null;
            }
        }

        private async void Update()
        {
            if (_loginFlowService == null || _isRefreshInFlight)
                return;

            if (_loginFlowService.State != LoginFlowState.Authenticated)
                return;

            _isRefreshInFlight = true;
            try
            {
                LoginAttemptResult result = await _loginFlowService.TryAutoRefreshAsync(_lifetimeCts.Token);
                if (!result.Success)
                    Debug.LogWarning($"[LoginFlow] Auto-refresh failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LoginFlow] Auto-refresh exception: {ex.Message}");
            }
            finally
            {
                _isRefreshInFlight = false;
            }
        }

        private void HandleLoginSuccess()
        {
            AuthSessionSnapshot snapshot = _networkClient != null
                ? _networkClient.CaptureAuthSession()
                : default;

            // Update SessionStateClient with authenticated account info
            if (_sessionStateClient != null)
            {
                _sessionStateClient.SetAuthenticatedSession(
                    accountId: 0, // TODO: extract accountId from game server
                    sessionToken: snapshot.AccessToken ?? string.Empty);
            }

            if (_frontendFlowDirector != null)
            {
                _frontendFlowDirector.SetAuthenticatedSession(
                    accountId: 0,
                    sessionToken: snapshot.AccessToken);
            }

            AuthenticatedChanged?.Invoke(true, snapshot.AccessToken ?? string.Empty);

            if (!_loadSceneOnSuccess)
                return;

            if (string.IsNullOrWhiteSpace(_characterSelectSceneName))
            {
                Debug.LogWarning("[LoginFlow] CharacterSelect scene name is empty.");
                return;
            }

            if (_frontendFlowDirector != null)
            {
                _frontendFlowDirector.EnterCharacterSelect();
                return;
            }

            SceneController.EnsureInstance().LoadScene(_characterSelectSceneName, "Login");
        }

        private void HandleLoggedOut()
        {
            if (_networkClient != null)
                _networkClient.Logout(_disconnectSocketOnLogout);

            if (_sessionStateClient != null)
                _sessionStateClient.ClearForLogout();

            AuthenticatedChanged?.Invoke(false, string.Empty);

            if (_frontendFlowDirector != null)
            {
                _frontendFlowDirector.LogoutToLogin();
                return;
            }

            SceneController.EnsureInstance().LoadScene("Login", "Boot");
        }
    }
}
