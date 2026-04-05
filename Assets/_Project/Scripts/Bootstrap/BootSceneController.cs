using UnityEngine;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Example boot scene entry point. Add this component to any object in Boot scene.
    /// </summary>
    public sealed class BootSceneController : MonoBehaviour
    {
        [SerializeField] private ClientBootstrap _bootstrap;
        [SerializeField] private bool _continueToLoginAfterBootstrap = true;

        private void Start()
        {
            if (_bootstrap == null)
                _bootstrap = ClientBootstrap.EnsureInstance();

            if (_bootstrap == null)
            {
                Debug.LogError("[BootSceneController] ClientBootstrap is missing and could not be created.");
                return;
            }

            _bootstrap.OnBootstrapped += HandleBootstrapped;

            if (!_bootstrap.BootstrapClient())
            {
                Debug.LogError("[BootSceneController] Bootstrap failed.");
                return;
            }

            if (_continueToLoginAfterBootstrap)
                _bootstrap.SceneFlow?.GoToLogin();
        }

        private void OnDestroy()
        {
            if (_bootstrap != null)
                _bootstrap.OnBootstrapped -= HandleBootstrapped;
        }

        private static void HandleBootstrapped()
        {
            Debug.Log("[BootSceneController] OnBootstrapped event received.");
        }
    }
}
