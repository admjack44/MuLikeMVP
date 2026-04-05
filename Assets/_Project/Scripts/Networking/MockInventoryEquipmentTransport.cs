using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Data.DTO;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Temporary fallback transport until server inventory opcodes are available.
    /// TODO: replace with real packet-based authoritative implementation.
    /// </summary>
    public sealed class MockInventoryEquipmentTransport : IInventoryEquipmentTransport
    {
        private readonly InventoryClientSystem _inventory;
        private readonly EquipmentClientSystem _equipment;
        private long _revision = 1;
        private readonly List<WorldDropDto> _drops = new();
        private readonly Dictionary<string, int> _quickSlots = new(StringComparer.OrdinalIgnoreCase);

        public event Action<InventoryEquipmentSnapshotDto> SnapshotReceived;
        public event Action<InventoryEquipmentDeltaDto> DeltaReceived;
        public event Action<IReadOnlyList<WorldDropDto>> WorldDropsReceived;

        public MockInventoryEquipmentTransport(InventoryClientSystem inventory, EquipmentClientSystem equipment)
        {
            _inventory = inventory;
            _equipment = equipment;
            SeedDrops();
        }

        public Task<InventoryEquipmentSnapshotDto> RequestFullSnapshotAsync(string characterId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = BuildSnapshot(characterId);
            SnapshotReceived?.Invoke(snapshot);
            WorldDropsReceived?.Invoke(_drops);
            return Task.FromResult(snapshot);
        }

        public Task<InventoryOperationResultDto> PickupDropAsync(PickupDropRequestDto request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (request == null)
                return Task.FromResult(Fail("Pickup request is null."));

            int index = _drops.FindIndex(d => d.dropEntityId == request.dropEntityId);
            if (index < 0)
                return Task.FromResult(Fail("Drop not found."));

            WorldDropDto drop = _drops[index];
            if (!TryAddToInventory(drop.itemId, drop.quantity, out string error))
                return Task.FromResult(Fail(error));

            _drops.RemoveAt(index);
            _revision++;
            WorldDropsReceived?.Invoke(_drops);
            SnapshotReceived?.Invoke(BuildSnapshot(request.characterId));
            EmitFullDelta(request.characterId);
            return Task.FromResult(Success("Item picked up."));
        }

        public Task<InventoryOperationResultDto> EquipAsync(EquipItemRequestDto request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (request == null)
                return Task.FromResult(Fail("Equip request is null."));

            if (!Enum.TryParse(request.equipSlot, true, out EquipmentClientSystem.EquipSlot equipSlot))
                return Task.FromResult(Fail("Invalid equip slot."));

            if (!_inventory.TryGetSlot(request.slotIndex, out InventoryClientSystem.InventorySlot slot) || slot.IsEmpty)
                return Task.FromResult(Fail("Inventory slot is empty."));

            var descriptor = new EquipmentClientSystem.EquippedItemDescriptor
            {
                ItemId = slot.ItemId
            };

            if (!_equipment.TryEquip(equipSlot, descriptor, out string error))
                return Task.FromResult(Fail(error));

            _inventory.TryConsumeFromSlot(request.slotIndex, 1, out _, out _);
            _revision++;
            SnapshotReceived?.Invoke(BuildSnapshot(request.characterId));
            EmitFullDelta(request.characterId);
            return Task.FromResult(Success("Item equipped."));
        }

        public Task<InventoryOperationResultDto> UnequipAsync(UnequipItemRequestDto request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (request == null)
                return Task.FromResult(Fail("Unequip request is null."));

            if (!Enum.TryParse(request.equipSlot, true, out EquipmentClientSystem.EquipSlot equipSlot))
                return Task.FromResult(Fail("Invalid equip slot."));

            if (!_equipment.TryGetEquippedState(equipSlot, out EquipmentClientSystem.EquipmentSlotState equipped) || equipped.IsEmpty)
                return Task.FromResult(Fail("No equipped item in selected slot."));

            if (!TryAddToInventory(equipped.Item.ItemId, 1, out string addError))
                return Task.FromResult(Fail(addError));

            _equipment.Unequip(equipSlot);
            _revision++;
            SnapshotReceived?.Invoke(BuildSnapshot(request.characterId));
            EmitFullDelta(request.characterId);
            return Task.FromResult(Success("Item unequipped."));
        }

        public Task<InventoryOperationResultDto> DropItemAsync(DropItemRequestDto request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (request == null)
                return Task.FromResult(Fail("Drop request is null."));

            if (!_inventory.TryGetSlot(request.slotIndex, out InventoryClientSystem.InventorySlot slot) || slot.IsEmpty)
                return Task.FromResult(Fail("Slot is empty."));

            int quantity = Mathf.Clamp(request.quantity, 1, slot.Quantity);
            if (!_inventory.TryConsumeFromSlot(request.slotIndex, quantity, out _, out string consumeError))
                return Task.FromResult(Fail(consumeError));

            _drops.Add(new WorldDropDto
            {
                dropEntityId = UnityEngine.Random.Range(10_000, 99_999),
                dropInstanceId = Guid.NewGuid().ToString("N"),
                itemId = slot.ItemId,
                quantity = quantity,
                x = 0f,
                y = 0f,
                z = 0f,
                reserved = false
            });

            _revision++;
            WorldDropsReceived?.Invoke(_drops);
            SnapshotReceived?.Invoke(BuildSnapshot(request.characterId));
            EmitFullDelta(request.characterId);
            return Task.FromResult(Success("Item dropped to world."));
        }

        private bool TryAddToInventory(int itemId, int quantity, out string error)
        {
            error = string.Empty;
            int remaining = quantity;

            for (int i = 0; i < _inventory.Slots.Count && remaining > 0; i++)
            {
                InventoryClientSystem.InventorySlot slot = _inventory.Slots[i];
                if (slot.IsEmpty || slot.ItemId != itemId)
                    continue;

                int free = Mathf.Max(0, slot.MaxStack - slot.Quantity);
                if (free <= 0)
                    continue;

                int transfer = Mathf.Min(free, remaining);
                slot.Quantity += transfer;
                _inventory.UpdateSlot(slot, out _);
                remaining -= transfer;
            }

            while (remaining > 0)
            {
                int emptySlot = FindEmptySlot();
                if (emptySlot < 0)
                {
                    error = "Inventory is full.";
                    return false;
                }

                int transfer = Mathf.Min(99, remaining);
                var slot = new InventoryClientSystem.InventorySlot
                {
                    SlotIndex = emptySlot,
                    ItemId = itemId,
                    Quantity = transfer,
                    MaxStack = 99,
                    DurabilityCurrent = 0,
                    DurabilityMax = 0,
                    Flags = InventoryClientSystem.InventoryItemFlags.Tradable
                };

                _inventory.UpdateSlot(slot, out _);
                remaining -= transfer;
            }

            return true;
        }

        private int FindEmptySlot()
        {
            for (int i = 0; i < _inventory.SlotCapacity; i++)
            {
                if (!_inventory.TryGetSlot(i, out InventoryClientSystem.InventorySlot slot) || slot.IsEmpty)
                    return i;
            }

            return -1;
        }

        private InventoryEquipmentSnapshotDto BuildSnapshot(string characterId)
        {
            var inventoryDtos = new List<InventorySlotDto>();
            for (int i = 0; i < _inventory.Slots.Count; i++)
            {
                InventoryClientSystem.InventorySlot slot = _inventory.Slots[i];
                inventoryDtos.Add(new InventorySlotDto
                {
                    slotIndex = slot.SlotIndex,
                    item = slot.IsEmpty
                        ? null
                        : new ItemInstanceDto
                        {
                            instanceId = Guid.NewGuid().ToString("N"),
                            itemId = slot.ItemId,
                            quantity = slot.Quantity,
                            maxStack = slot.MaxStack,
                            durabilityCurrent = slot.DurabilityCurrent,
                            durabilityMax = slot.DurabilityMax,
                            enhancementLevel = 0,
                            ownerCharacterId = characterId
                        }
                });
            }

            var equippedDtos = new List<EquippedItemDto>();
            foreach (var pair in _equipment.Equipped)
            {
                if (pair.Value.IsEmpty)
                    continue;

                equippedDtos.Add(new EquippedItemDto
                {
                    slot = pair.Key.ToString(),
                    item = new ItemInstanceDto
                    {
                        instanceId = Guid.NewGuid().ToString("N"),
                        itemId = pair.Value.Item.ItemId,
                        quantity = 1,
                        maxStack = 1,
                        durabilityCurrent = 0,
                        durabilityMax = 0,
                        enhancementLevel = 0,
                        ownerCharacterId = characterId
                    }
                });
            }

            return new InventoryEquipmentSnapshotDto
            {
                characterId = characterId,
                inventorySlots = inventoryDtos.ToArray(),
                equipped = equippedDtos.ToArray(),
                quickSlots = BuildQuickSlots(),
                powerScore = 0,
                serverRevision = _revision
            };
        }

        private QuickSlotDto[] BuildQuickSlots()
        {
            if (_quickSlots.Count == 0)
            {
                if (_inventory.TryGetQuickSlot(InventoryClientSystem.QuickSlotKind.HpPotion, out int hpSlot))
                    _quickSlots["HpPotion"] = hpSlot;

                if (_inventory.TryGetQuickSlot(InventoryClientSystem.QuickSlotKind.MpPotion, out int mpSlot))
                    _quickSlots["MpPotion"] = mpSlot;
            }

            var list = new List<QuickSlotDto>(_quickSlots.Count);
            foreach (KeyValuePair<string, int> pair in _quickSlots)
            {
                list.Add(new QuickSlotDto
                {
                    kind = pair.Key,
                    slotIndex = pair.Value
                });
            }

            return list.ToArray();
        }

        private void EmitFullDelta(string characterId)
        {
            InventoryEquipmentSnapshotDto snapshot = BuildSnapshot(characterId);
            if (snapshot == null)
                return;

            var inv = new List<InventorySlotDeltaDto>();
            if (snapshot.inventorySlots != null)
            {
                for (int i = 0; i < snapshot.inventorySlots.Length; i++)
                {
                    inv.Add(new InventorySlotDeltaDto
                    {
                        remove = false,
                        slot = snapshot.inventorySlots[i]
                    });
                }
            }

            var equip = new List<EquippedItemDeltaDto>();
            if (snapshot.equipped != null)
            {
                for (int i = 0; i < snapshot.equipped.Length; i++)
                {
                    equip.Add(new EquippedItemDeltaDto
                    {
                        remove = false,
                        slot = snapshot.equipped[i] != null ? snapshot.equipped[i].slot : string.Empty,
                        equipped = snapshot.equipped[i]
                    });
                }
            }

            var quick = new List<QuickSlotDeltaDto>();
            if (snapshot.quickSlots != null)
            {
                for (int i = 0; i < snapshot.quickSlots.Length; i++)
                {
                    quick.Add(new QuickSlotDeltaDto
                    {
                        remove = false,
                        kind = snapshot.quickSlots[i] != null ? snapshot.quickSlots[i].kind : string.Empty,
                        slotIndex = snapshot.quickSlots[i] != null ? snapshot.quickSlots[i].slotIndex : -1
                    });
                }
            }

            DeltaReceived?.Invoke(new InventoryEquipmentDeltaDto
            {
                characterId = characterId,
                inventory = inv.ToArray(),
                equipment = equip.ToArray(),
                quickSlots = quick.ToArray(),
                serverRevision = _revision
            });
        }

        private InventoryOperationResultDto Success(string message)
        {
            return new InventoryOperationResultDto { success = true, message = message, serverRevision = _revision };
        }

        private InventoryOperationResultDto Fail(string message)
        {
            return new InventoryOperationResultDto { success = false, message = message, serverRevision = _revision };
        }

        private void SeedDrops()
        {
            _drops.Add(new WorldDropDto
            {
                dropEntityId = 501,
                dropInstanceId = Guid.NewGuid().ToString("N"),
                itemId = 1001,
                quantity = 2,
                x = 2f,
                y = 0f,
                z = 2f,
                reserved = false
            });
        }
    }
}
