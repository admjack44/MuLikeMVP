using UnityEngine;

namespace MuLike.Performance.Pooling
{
    /// <summary>
    /// Recycles entities to pool instead of destroying them.
    /// </summary>
    public sealed class EntityRecycler : MonoBehaviour
    {
        [SerializeField] private bool _recycleOnDisable;
        [SerializeField] private float _maxDistanceFromCamera = 120f;

        private bool _isRecycling;

        public void RecycleNow()
        {
            if (_isRecycling)
                return;

            _isRecycling = true;
            if (MobilePoolManager.Instance != null)
            {
                MobilePoolManager.Instance.Release(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            _isRecycling = false;
        }

        private void LateUpdate()
        {
            if (_maxDistanceFromCamera <= 0f)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            float distance = Vector3.Distance(cam.transform.position, transform.position);
            if (distance > _maxDistanceFromCamera)
                RecycleNow();
        }

        private void OnDisable()
        {
            if (_recycleOnDisable && gameObject.scene.IsValid() && gameObject.activeInHierarchy)
                RecycleNow();
        }
    }
}
