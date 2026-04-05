using UnityEngine;

namespace MuLike.Performance.Rendering
{
    /// <summary>
    /// Simple 2-tier LOD switch for monsters and players.
    /// </summary>
    public sealed class DistanceLodController : MonoBehaviour
    {
        [SerializeField] private Renderer[] _highDetailRenderers;
        [SerializeField] private Renderer[] _lowDetailRenderers;
        [SerializeField] private Animator _animator;
        [SerializeField] private float _lodSwitchDistance = 18f;

        private bool _highActive = true;

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            float distance = Vector3.Distance(cam.transform.position, transform.position);
            bool useHigh = distance <= _lodSwitchDistance;
            if (useHigh == _highActive)
                return;

            _highActive = useHigh;
            SetRendererGroup(_highDetailRenderers, useHigh);
            SetRendererGroup(_lowDetailRenderers, !useHigh);

            if (_animator != null)
                _animator.cullingMode = useHigh ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.CullCompletely;
        }

        private static void SetRendererGroup(Renderer[] renderers, bool enabled)
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = enabled;
            }
        }
    }
}
