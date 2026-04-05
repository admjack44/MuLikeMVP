using UnityEngine;

namespace MuLike.Performance.Rendering
{
    /// <summary>
    /// Applies distance-based camera culling defaults for mid-range Android devices.
    /// </summary>
    public sealed class MobileCameraCullingConfigurator : MonoBehaviour
    {
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private bool _applyOnStart = true;
        [SerializeField] private float _nearClip = 0.2f;
        [SerializeField] private float _farClip = 90f;

        [Header("Layer Cull Distances")]
        [SerializeField] private string _monsterLayer = "Monster";
        [SerializeField] private float _monsterDistance = 45f;
        [SerializeField] private string _dropLayer = "Drop";
        [SerializeField] private float _dropDistance = 25f;
        [SerializeField] private string _vfxLayer = "VFX";
        [SerializeField] private float _vfxDistance = 35f;

        private void Start()
        {
            if (_applyOnStart)
                Apply();
        }

        [ContextMenu("Apply Camera Culling")]
        public void Apply()
        {
            Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[MobileCameraCullingConfigurator] No camera found.");
                return;
            }

            cam.nearClipPlane = _nearClip;
            cam.farClipPlane = _farClip;

            float[] distances = new float[32];
            float[] existing = cam.layerCullDistances;
            if (existing != null && existing.Length == 32)
                existing.CopyTo(distances, 0);

            ApplyLayerDistance(distances, _monsterLayer, _monsterDistance);
            ApplyLayerDistance(distances, _dropLayer, _dropDistance);
            ApplyLayerDistance(distances, _vfxLayer, _vfxDistance);

            cam.layerCullDistances = distances;
            cam.layerCullSpherical = true;

            Debug.Log($"[MobileCameraCullingConfigurator] Applied to camera '{cam.name}'. FarClip={_farClip}.");
        }

        private static void ApplyLayerDistance(float[] distances, string layerName, float distance)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0 || layer >= 32)
            {
                Debug.LogWarning($"[MobileCameraCullingConfigurator] Layer '{layerName}' not found. Skipping cull distance.");
                return;
            }

            distances[layer] = Mathf.Max(1f, distance);
        }
    }
}