using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// Spawns short-lived loot markers for demo kill feedback.
    /// </summary>
    public sealed class DemoLootSpawner : MonoBehaviour
    {
        [SerializeField] private DropView _dropPrefab;
        [SerializeField, Min(0.5f)] private float _dropLifetimeSeconds = 8f;

        private readonly List<DropRecord> _activeDrops = new();
        private int _nextDropEntityId = 90_000;

        private void Update()
        {
            if (_activeDrops.Count == 0)
                return;

            float now = Time.time;
            for (int i = _activeDrops.Count - 1; i >= 0; i--)
            {
                DropRecord record = _activeDrops[i];
                if (record.View == null || now < record.ReleaseAt)
                    continue;

                Destroy(record.View.gameObject);
                _activeDrops.RemoveAt(i);
            }
        }

        public void SpawnMockLoot(Vector3 position, int itemId, string itemName)
        {
            DropView view = CreateDrop(position);
            if (view == null)
                return;

            view.Initialize(_nextDropEntityId++);
            view.Setup(itemId, string.IsNullOrWhiteSpace(itemName) ? $"Item {itemId}" : itemName);
            _activeDrops.Add(new DropRecord(view, Time.time + Mathf.Max(0.5f, _dropLifetimeSeconds)));
        }

        private DropView CreateDrop(Vector3 position)
        {
            if (_dropPrefab != null)
                return Instantiate(_dropPrefab, position, Quaternion.identity, transform);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DemoLootDrop";
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.45f;

            DropView fallback = go.AddComponent<DropView>();
            return fallback;
        }

        private readonly struct DropRecord
        {
            public DropRecord(DropView view, float releaseAt)
            {
                View = view;
                ReleaseAt = releaseAt;
            }

            public DropView View { get; }
            public float ReleaseAt { get; }
        }
    }
}
