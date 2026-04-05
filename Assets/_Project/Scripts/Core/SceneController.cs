using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MuLike.Core
{
    /// <summary>
    /// Handles scene loading and unloading with optional loading screen support.
    /// </summary>
    public class SceneController : MonoBehaviour
    {
        public static SceneController Instance { get; private set; }

        [Header("Recovery")]
        [SerializeField] private string _defaultRecoveryScene = "Login";

        public string ActiveSceneName { get; private set; } = string.Empty;

        public event System.Action<string> SceneLoadStarted;
        public event System.Action<string> SceneLoadCompleted;
        public event System.Action<string, string> SceneLoadFailed;

        public static SceneController EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            SceneController existing = FindAnyObjectByType<SceneController>();
            if (existing != null)
                return existing;

            var go = new GameObject("SceneController");
            return go.AddComponent<SceneController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ActiveSceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[FrontendFlow] SceneController ready. Active scene: {ActiveSceneName}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                Instance = null;
            }
        }

        public async Task LoadSceneAsync(string sceneName)
        {
            await LoadSceneAsync(sceneName, _defaultRecoveryScene);
        }

        public async Task<bool> LoadSceneAsync(string sceneName, string fallbackSceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                string reason = "Scene name is empty.";
                Debug.LogWarning($"[FrontendFlow] {reason}");
                return await RecoverFromLoadFailure(sceneName, fallbackSceneName, reason);
            }

            if (!CanLoadScene(sceneName))
            {
                string reason = $"Scene '{sceneName}' is not available in build settings.";
                Debug.LogWarning($"[FrontendFlow] {reason}");
                return await RecoverFromLoadFailure(sceneName, fallbackSceneName, reason);
            }

            if (string.Equals(ActiveSceneName, sceneName, System.StringComparison.Ordinal))
            {
                Debug.Log($"[FrontendFlow] Scene '{sceneName}' already active.");
                return true;
            }

            SceneLoadStarted?.Invoke(sceneName);
            Debug.Log($"[FrontendFlow] Loading scene '{sceneName}'...");

            try
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op == null)
                {
                    string reason = $"LoadSceneAsync returned null for scene '{sceneName}'.";
                    return await RecoverFromLoadFailure(sceneName, fallbackSceneName, reason);
                }

                while (!op.isDone)
                    await Task.Yield();

                SceneLoadCompleted?.Invoke(sceneName);
                Debug.Log($"[FrontendFlow] Scene '{sceneName}' loaded successfully.");
                return true;
            }
            catch (System.Exception ex)
            {
                string reason = $"Exception while loading scene '{sceneName}': {ex.Message}";
                return await RecoverFromLoadFailure(sceneName, fallbackSceneName, reason);
            }
        }

        public void LoadScene(string sceneName)
        {
            LoadScene(sceneName, _defaultRecoveryScene);
        }

        public void LoadScene(string sceneName, string fallbackSceneName)
        {
            _ = LoadSceneAsync(sceneName, fallbackSceneName);
        }

        public bool CanLoadScene(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
        }

        private async Task<bool> RecoverFromLoadFailure(string targetScene, string fallbackSceneName, string reason)
        {
            SceneLoadFailed?.Invoke(targetScene ?? string.Empty, reason);
            Debug.LogError($"[FrontendFlow] {reason}");

            string fallback = !string.IsNullOrWhiteSpace(fallbackSceneName)
                ? fallbackSceneName
                : _defaultRecoveryScene;

            if (string.IsNullOrWhiteSpace(fallback) || !CanLoadScene(fallback))
            {
                Debug.LogError("[FrontendFlow] No valid fallback scene available.");
                return false;
            }

            if (string.Equals(targetScene, fallback, System.StringComparison.Ordinal))
            {
                Debug.LogError("[FrontendFlow] Fallback equals failing target scene. Recovery aborted.");
                return false;
            }

            Debug.LogWarning($"[FrontendFlow] Recovering by loading fallback scene '{fallback}'.");
            return await LoadSceneAsync(fallback, _defaultRecoveryScene);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            ActiveSceneName = scene.name;
        }
    }
}
