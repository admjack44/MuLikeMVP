using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Data.DTO;
using MuLike.Gameplay.Entities;
using UnityEngine;

namespace MuLike.Systems
{
    public sealed class LootPickupSystem
    {
        private readonly InventoryEquipmentService _inventoryService;
        private readonly DropViewPool _dropPool;
        private readonly Dictionary<int, DropView> _dropsByEntityId = new();
        private bool _tapPickupInFlight;

        public bool AutoPickupEnabled { get; set; }
        public float AutoPickupRadius { get; set; } = 3.2f;

        public event Action<string> PickupMessage;

        public LootPickupSystem(InventoryEquipmentService inventoryService, DropViewPool dropPool)
        {
            _inventoryService = inventoryService;
            _dropPool = dropPool;
        }

        public void ApplyWorldDrops(IReadOnlyList<WorldDropDto> worldDrops)
        {
            var seen = new HashSet<int>();

            if (worldDrops != null)
            {
                for (int i = 0; i < worldDrops.Count; i++)
                {
                    WorldDropDto dto = worldDrops[i];
                    if (dto == null)
                        continue;

                    seen.Add(dto.dropEntityId);
                    if (_dropsByEntityId.TryGetValue(dto.dropEntityId, out DropView existing))
                    {
                        existing.transform.position = new Vector3(dto.x, dto.y, dto.z);
                        continue;
                    }

                    DropView created = _dropPool.Spawn(new Vector3(dto.x, dto.y, dto.z));
                    created.Setup(dto.itemId, $"Item {dto.itemId}");
                    created.Initialize(dto.dropEntityId);
                    created.Tapped += HandleDropTapped;
                    _dropsByEntityId[dto.dropEntityId] = created;
                }
            }

            var toRemove = new List<int>();
            foreach (var pair in _dropsByEntityId)
            {
                if (seen.Contains(pair.Key))
                    continue;

                pair.Value.Tapped -= HandleDropTapped;
                pair.Value.MarkPickedForPool();
                _dropPool.Release(pair.Value);
                toRemove.Add(pair.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _dropsByEntityId.Remove(toRemove[i]);
        }

        public async Task TryAutoPickupAsync(Transform actor, CancellationToken ct)
        {
            if (!AutoPickupEnabled || actor == null)
                return;

            int bestDrop = 0;
            float bestDist = float.MaxValue;

            foreach (var pair in _dropsByEntityId)
            {
                DropView drop = pair.Value;
                if (drop == null)
                    continue;

                float dist = Vector3.Distance(actor.position, drop.transform.position);
                if (dist > Mathf.Max(0.5f, AutoPickupRadius) || dist >= bestDist)
                    continue;

                bestDist = dist;
                bestDrop = pair.Key;
            }

            if (bestDrop <= 0)
                return;

            bool success = await _inventoryService.PickupDropAsync(bestDrop, ct);
            if (success)
                PickupMessage?.Invoke("Picked up nearby drop.");
        }

        public async Task<bool> TryPickupByTapAsync(int dropEntityId, CancellationToken ct)
        {
            bool success = await _inventoryService.PickupDropAsync(dropEntityId, ct);
            if (success)
                PickupMessage?.Invoke("Drop picked up.");

            return success;
        }

        private async void HandleDropTapped(DropView drop)
        {
            if (_tapPickupInFlight || drop == null)
                return;

            _tapPickupInFlight = true;
            try
            {
                await TryPickupByTapAsync(drop.EntityId, CancellationToken.None);
            }
            finally
            {
                _tapPickupInFlight = false;
            }
        }
    }
}
