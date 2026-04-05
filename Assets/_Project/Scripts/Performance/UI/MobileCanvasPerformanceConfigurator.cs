using UnityEngine;
using UnityEngine.UI;

namespace MuLike.Performance.UI
{
    /// <summary>
    /// Applies lightweight canvas defaults to reduce UI rebuild/raycast overhead.
    /// </summary>
    public sealed class MobileCanvasPerformanceConfigurator : MonoBehaviour
    {
        [SerializeField] private bool _applyOnAwake = true;
        [SerializeField] private bool _autoFindRootCanvases = true;
        [SerializeField] private Canvas[] _targetCanvases = new Canvas[0];

        [Header("Canvas")]
        [SerializeField] private bool _disablePixelPerfect = true;

        [Header("Raycast")]
        [SerializeField] private bool _disableRaycastOnStaticGraphics = true;
        [SerializeField] private Transform[] _staticGraphicRoots = new Transform[0];

        private void Awake()
        {
            if (_applyOnAwake)
                Apply();
        }

        [ContextMenu("Apply Canvas Optimization")]
        public void Apply()
        {
            if (_autoFindRootCanvases)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                    ApplyToCanvas(canvases[i]);
            }

            for (int i = 0; i < _targetCanvases.Length; i++)
                ApplyToCanvas(_targetCanvases[i]);

            if (_disableRaycastOnStaticGraphics)
            {
                for (int i = 0; i < _staticGraphicRoots.Length; i++)
                    DisableRaycastTargets(_staticGraphicRoots[i]);
            }
        }

        private void ApplyToCanvas(Canvas canvas)
        {
            if (canvas == null || !canvas.isRootCanvas)
                return;

            if (_disablePixelPerfect)
                canvas.pixelPerfect = false;

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.ignoreReversedGraphics = true;
                raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            }
        }

        private static void DisableRaycastTargets(Transform root)
        {
            if (root == null)
                return;

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic g = graphics[i];
                if (g == null)
                    continue;

                if (g.GetComponent<Button>() != null || g.GetComponent<Toggle>() != null || g.GetComponent<Slider>() != null || g.GetComponent<Scrollbar>() != null)
                    continue;

                g.raycastTarget = false;
            }
        }
    }
}
