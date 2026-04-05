using UnityEngine;

namespace MuLike.Performance.Pooling
{
    /// <summary>
    /// Marker + lifecycle callbacks for pooled GameObjects.
    /// </summary>
    public sealed class PoolableObject : MonoBehaviour
    {
        [SerializeField] private string _poolKey;

        public string PoolKey => _poolKey;

        public void ConfigurePoolKey(string poolKey)
        {
            _poolKey = poolKey;
        }

        public void OnSpawnedFromPool()
        {
            SendMessage("OnSpawnedFromPool", SendMessageOptions.DontRequireReceiver);
        }

        public void OnRecycledToPool()
        {
            SendMessage("OnRecycledToPool", SendMessageOptions.DontRequireReceiver);
        }
    }
}
