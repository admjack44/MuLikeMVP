using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Demo helper that keeps local in-memory sessions authenticated for quick playtesting.
    /// </summary>
    public sealed class DemoAutoAuthController : MonoBehaviour
    {
        [SerializeField] private NetworkGameClient _networkClient;
        [SerializeField] private bool _runOnStart = true;
        [SerializeField] private float _retryEverySeconds = 1.2f;

        private float _nextTryAt;

        private void Awake()
        {
            if (_networkClient == null)
                _networkClient = FindAnyObjectByType<NetworkGameClient>();
        }

        private void Update()
        {
            if (!_runOnStart || _networkClient == null)
                return;

            if (Time.unscaledTime < _nextTryAt)
                return;

            _nextTryAt = Time.unscaledTime + Mathf.Max(0.3f, _retryEverySeconds);

            if (!_networkClient.IsConnected || _networkClient.IsAuthenticated || _networkClient.IsConnecting)
                return;

            _ = _networkClient.SendLoginAsync();
        }
    }
}
