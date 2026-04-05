using System;
using UnityEngine;

namespace MuLike.Core
{
    /// <summary>
    /// Persistent frontend flow coordinator for Boot/Login/CharacterSelect/World scene transitions.
    /// </summary>
    public sealed class FrontendFlowDirector : MonoBehaviour
    {
        private static FrontendFlowDirector _instance;

        [Header("Scenes")]
        [SerializeField] private string _bootSceneName = "Boot";
        [SerializeField] private string _loginSceneName = "Login";
        [SerializeField] private string _characterSelectSceneName = "CharacterSelect";
        [SerializeField] private string _defaultWorldSceneName = "World_Dev";

        public static FrontendFlowDirector Instance => _instance;

        public FrontendFlowState State { get; } = new FrontendFlowState();

        public event Action<FrontendFlowState> StateChanged;
        public event Action<string> FlowStepEntered;

        public static FrontendFlowDirector EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            FrontendFlowDirector existing = FindAnyObjectByType<FrontendFlowDirector>();
            if (existing != null)
                return existing;

            var go = new GameObject("FrontendFlowDirector");
            return go.AddComponent<FrontendFlowDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[FrontendFlow] Duplicate FrontendFlowDirector detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void EnterBoot()
        {
            EnterFlowStep("Boot");
            LoadSceneSafe(_bootSceneName, _loginSceneName);
        }

        public void EnterLogin()
        {
            EnterFlowStep("Login");
            LoadSceneSafe(_loginSceneName, _bootSceneName);
        }

        public void EnterCharacterSelect()
        {
            EnterFlowStep("CharacterSelect");

            if (!State.IsAuthenticated)
            {
                Debug.LogWarning("[FrontendFlow] EnterCharacterSelect blocked: not authenticated. Redirecting to Login.");
                EnterLogin();
                return;
            }

            LoadSceneSafe(_characterSelectSceneName, _loginSceneName);
        }

        public void EnterWorld(string sceneName)
        {
            EnterFlowStep("World");

            if (!State.IsAuthenticated)
            {
                Debug.LogWarning("[FrontendFlow] EnterWorld blocked: not authenticated. Redirecting to Login.");
                EnterLogin();
                return;
            }

            string chosen = string.IsNullOrWhiteSpace(sceneName) ? _defaultWorldSceneName : sceneName;
            State.SetLastWorldScene(chosen);
            EmitStateChanged();
            LoadSceneSafe(chosen, _characterSelectSceneName);
        }

        public void LogoutToLogin()
        {
            EnterFlowStep("Logout");
            State.ResetForLogout();
            EmitStateChanged();
            EnterLogin();
        }

        public void SetAuthenticatedSession(int accountId, string sessionToken)
        {
            State.SetAuthenticated(true, accountId, sessionToken);
            EmitStateChanged();
        }

        public void SetSelectedCharacter(int characterId, string characterName)
        {
            State.SetSelectedCharacter(characterId, characterName);
            EmitStateChanged();
        }

        private void EnterFlowStep(string step)
        {
            Debug.Log($"[FrontendFlow] Entering step: {step}");
            FlowStepEntered?.Invoke(step);
        }

        private void LoadSceneSafe(string sceneName, string fallbackSceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"[FrontendFlow] Scene name is empty. Using fallback '{fallbackSceneName}'.");
                sceneName = fallbackSceneName;
            }

            SceneController sceneController = SceneController.EnsureInstance();
            if (sceneController == null)
            {
                Debug.LogError("[FrontendFlow] SceneController unavailable. Cannot continue flow.");
                return;
            }

            sceneController.LoadScene(sceneName, fallbackSceneName);
        }

        private void EmitStateChanged()
        {
            StateChanged?.Invoke(State);
        }
    }
}