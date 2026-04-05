using System;
using MuLike.Data.DTO;
using UnityEngine;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Manages 3D preview model for selected character and rotates it slowly.
    /// </summary>
    public sealed class CharacterPreviewAnchor : MonoBehaviour
    {
        [Serializable]
        private struct ClassPreviewBinding
        {
            public string classId;
            public GameObject prefab;
        }

        [SerializeField] private Transform _spawnRoot;
        [SerializeField] private ClassPreviewBinding[] _classPrefabs;
        [SerializeField] private float _rotationSpeed = 22f;
        [SerializeField] private bool _rotatePreview = true;

        private GameObject _activePreview;

        private void Update()
        {
            if (!_rotatePreview || _activePreview == null)
                return;

            float angle = _rotationSpeed * Time.deltaTime;
            _activePreview.transform.Rotate(0f, angle, 0f, Space.World);
        }

        public void SetCharacter(CharacterSummaryDto character)
        {
            ClearPreview();

            if (character == null)
                return;

            GameObject prefab = ResolvePrefab(character.classId);
            Transform root = _spawnRoot != null ? _spawnRoot : transform;

            if (prefab != null)
            {
                _activePreview = Instantiate(prefab, root);
                _activePreview.transform.localPosition = Vector3.zero;
                _activePreview.transform.localRotation = Quaternion.identity;
                return;
            }

            // Safe fallback while class-specific art prefab is not configured.
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = $"Preview_{character.name}";
            capsule.transform.SetParent(root, false);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localRotation = Quaternion.identity;

            Renderer renderer = capsule.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = ResolveClassColor(character.classId);

            _activePreview = capsule;
        }

        public void ClearPreview()
        {
            if (_activePreview == null)
                return;

            Destroy(_activePreview);
            _activePreview = null;
        }

        private GameObject ResolvePrefab(string classId)
        {
            string candidate = classId ?? string.Empty;
            for (int i = 0; i < _classPrefabs.Length; i++)
            {
                if (!string.Equals(_classPrefabs[i].classId, candidate, StringComparison.OrdinalIgnoreCase))
                    continue;

                return _classPrefabs[i].prefab;
            }

            return null;
        }

        private static Color ResolveClassColor(string classId)
        {
            if (string.Equals(classId, "DarkKnight", StringComparison.OrdinalIgnoreCase))
                return new Color(0.35f, 0.55f, 0.95f);

            if (string.Equals(classId, "DarkWizard", StringComparison.OrdinalIgnoreCase))
                return new Color(0.35f, 0.8f, 1f);

            if (string.Equals(classId, "FairyElf", StringComparison.OrdinalIgnoreCase))
                return new Color(0.45f, 0.95f, 0.45f);

            return new Color(0.9f, 0.75f, 0.35f);
        }
    }
}
