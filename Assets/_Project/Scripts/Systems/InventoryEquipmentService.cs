using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Data.Catalogs;
using MuLike.Data.DTO;
using MuLike.Networking;
using UnityEngine;

namespace MuLike.Systems
{
    public sealed class InventoryEquipmentService
    {
        private readonly InventoryClientSystem _inventory;
        private readonly EquipmentClientSystem _equipment;
        private readonly StatsClientSystem _stats;
        private readonly CatalogResolver _catalog;
        private readonly IInventoryEquipmentTransport _transport;

        private string _characterId = "char-local";
        private long _lastServerRevision;

        public event Action<InventoryEquipmentSnapshotDto> SnapshotApplied;
        public event Action<InventoryEquipmentDeltaDto> DeltaApplied;
        public event Action<IReadOnlyList<WorldDropDto>> WorldDropsUpdated;
        public event Action<string> OperationFailed;
        public event Action<string> OperationSucceeded;

        public InventoryEquipmentService(
            InventoryClientSystem inventory,
            EquipmentClientSystem equipment,
            StatsClientSystem stats,
            CatalogResolver catalog,
            IInventoryEquipmentTransport transport)
        {
            _inventory = inventory;
            _equipment = equipment;
            _stats = stats;
            _catalog = catalog;
            _transport = transport;

            if (_transport != null)
            {
                _transport.SnapshotReceived += HandleSnapshotReceived;
                _transport.DeltaReceived += HandleDeltaReceived;
                _transport.WorldDropsReceived += HandleWorldDropsReceived;
            }
        }

        public void SetCharacterId(string characterId)
        {
            _characterId = string.IsNullOrWhiteSpace(characterId) ? "char-local" : characterId.Trim();
        }

        public async Task RefreshSnapshotAsync(CancellationToken ct)
        {
            if (_transport == null)
            {
                OperationFailed?.Invoke("Inventory transport missing.");
                return;
            }

            InventoryEquipmentSnapshotDto snapshot = await _transport.RequestFullSnapshotAsync(_characterId, ct);
            ApplySnapshot(snapshot);
        }

        public async Task<bool> PickupDropAsync(int dropEntityId, CancellationToken ct)
        {
            if (_transport == null)
                return false;

            InventoryOperationResultDto result = await _transport.PickupDropAsync(
                new PickupDropRequestDto { dropEntityId = dropEntityId, characterId = _characterId },
                ct);

            HandleResult(result);
            return result != null && result.success;
        }

        public async Task<bool> EquipAsync(int slotIndex, EquipmentClientSystem.EquipSlot slot, CancellationToken ct)
        {
            if (_transport == null)
                return false;

            if (!_inventory.TryGetSlot(slotIndex, out InventoryClientSystem.InventorySlot inv) || inv.IsEmpty)
            {
                OperationFailed?.Invoke("Inventory slot is empty.");
                return false;
            }

            if (!ValidateClassAndLevel(inv.ItemId, out string validationError))
            {
                OperationFailed?.Invoke(validationError);
                return false;
            }

            InventoryOperationResultDto result = await _transport.EquipAsync(
                new EquipItemRequestDto
                {
                    slotIndex = slotIndex,
                    equipSlot = slot.ToString(),
                    characterId = _characterId
                },
                ct);

            HandleResult(result);
            return result != null && result.success;
        }

        public async Task<bool> UnequipAsync(EquipmentClientSystem.EquipSlot slot, int preferredInventorySlot, CancellationToken ct)
        {
            if (_transport == null)
                return false;

            InventoryOperationResultDto result = await _transport.UnequipAsync(
                new UnequipItemRequestDto
                {
                    equipSlot = slot.ToString(),
                    targetInventorySlotIndex = preferredInventorySlot,
                    characterId = _characterId
                },
                ct);

            HandleResult(result);
            return result != null && result.success;
        }

        public async Task<bool> DropAsync(int slotIndex, int quantity, CancellationToken ct)
        {
            if (_transport == null)
                return false;

            InventoryOperationResultDto result = await _transport.DropItemAsync(
                new DropItemRequestDto
                {
                    slotIndex = slotIndex,
                    quantity = Mathf.Max(1, quantity),
                    characterId = _characterId
                },
                ct);

            HandleResult(result);
            return result != null && result.success;
        }

        public int CalculatePowerScore()
        {
            return ItemPowerScoreCalculator.CalculateCharacterPower(_inventory, _equipment, _catalog);
        }

        public string BuildTooltip(InventoryClientSystem.InventorySlot slot)
        {
            if (slot.IsEmpty)
                return "Empty slot";

            if (_catalog == null || !_catalog.TryGetItemDefinition(slot.ItemId, out ItemDefinition item))
                return $"Item {slot.ItemId} x{slot.Quantity}";

            int score = ItemPowerScoreCalculator.Calculate(item);
            string slots = item.AllowedEquipSlots != null && item.AllowedEquipSlots.Count > 0
                ? string.Join(", ", item.AllowedEquipSlots)
                : "N/A";

            return $"{item.Name}\n"
                + $"Rarity: {item.Rarity}\n"
                + $"Req Lv: {item.RequiredLevel}\n"
                + $"Equip: {slots}\n"
                + $"Power: {score}\n"
                + $"Qty: {slot.Quantity}";
        }

        private bool ValidateClassAndLevel(int itemId, out string error)
        {
            error = string.Empty;
            if (_catalog == null || !_catalog.TryGetItemDefinition(itemId, out ItemDefinition item))
                return true;

            if (_stats != null)
            {
                int level = _stats.Snapshot.Primary.Level;
                if (level < item.RequiredLevel)
                {
                    error = $"Requires level {item.RequiredLevel}.";
                    return false;
                }

                if (item.AllowedClasses != null
                    && item.AllowedClasses.Count > 0
                    && !item.AllowedClasses.Contains(CharacterClassRestriction.Any)
                    && !MatchesClass(item.AllowedClasses, _stats.Snapshot.Primary.Class))
                {
                    error = "Item class restriction not met.";
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesClass(IReadOnlyList<CharacterClassRestriction> allowed, StatsClientSystem.CharacterClass cls)
        {
            for (int i = 0; i < allowed.Count; i++)
            {
                CharacterClassRestriction entry = allowed[i];
                switch (entry)
                {
                    case CharacterClassRestriction.Warrior when cls == StatsClientSystem.CharacterClass.DarkKnight:
                    case CharacterClassRestriction.Mage when cls == StatsClientSystem.CharacterClass.DarkWizard:
                    case CharacterClassRestriction.Ranger when cls == StatsClientSystem.CharacterClass.FairyElf:
                    case CharacterClassRestriction.DarkLord when cls == StatsClientSystem.CharacterClass.DarkLord:
                    case CharacterClassRestriction.Paladin when cls == StatsClientSystem.CharacterClass.MagicGladiator:
                        return true;
                }
            }

            return false;
        }

        private void HandleSnapshotReceived(InventoryEquipmentSnapshotDto snapshot)
        {
            ApplySnapshot(snapshot);
        }

        private void HandleWorldDropsReceived(IReadOnlyList<WorldDropDto> drops)
        {
            WorldDropsUpdated?.Invoke(drops);
        }

        private void HandleDeltaReceived(InventoryEquipmentDeltaDto delta)
        {
            ApplyDelta(delta);
        }

        private void ApplySnapshot(InventoryEquipmentSnapshotDto snapshot)
        {
            if (snapshot == null)
                return;

            if (snapshot.serverRevision > 0 && snapshot.serverRevision < _lastServerRevision)
            {
                Debug.LogWarning($"[InventoryEquipmentService] Ignored stale snapshot revision {snapshot.serverRevision} < {_lastServerRevision}.");
                return;
            }

            _lastServerRevision = snapshot.serverRevision;

            var invSnapshots = new List<InventoryClientSystem.InventorySlotSnapshot>();
            if (snapshot.inventorySlots != null)
            {
                for (int i = 0; i < snapshot.inventorySlots.Length; i++)
                {
                    InventorySlotDto s = snapshot.inventorySlots[i];
                    if (s == null || s.item == null || s.item.itemId <= 0 || s.item.quantity <= 0)
                        continue;

                    invSnapshots.Add(new InventoryClientSystem.InventorySlotSnapshot
                    {
                        slotIndex = s.slotIndex,
                        itemId = s.item.itemId,
                        quantity = s.item.quantity,
                        maxStack = Mathf.Max(1, s.item.maxStack),
                        durabilityCurrent = s.item.durabilityCurrent,
                        durabilityMax = s.item.durabilityMax,
                        flags = InventoryClientSystem.InventoryItemFlags.Tradable
                    });
                }
            }

            _inventory?.ApplySnapshot(invSnapshots);

            var equipSnapshots = new List<EquipmentClientSystem.EquipmentSlotSnapshot>();
            if (snapshot.equipped != null)
            {
                for (int i = 0; i < snapshot.equipped.Length; i++)
                {
                    EquippedItemDto e = snapshot.equipped[i];
                    if (e == null || e.item == null || e.item.itemId <= 0)
                        continue;

                    equipSnapshots.Add(new EquipmentClientSystem.EquipmentSlotSnapshot
                    {
                        slot = e.slot,
                        itemId = e.item.itemId,
                        type = string.Empty,
                        subtype = string.Empty,
                        family = string.Empty,
                        category = EquipmentClientSystem.EquipItemCategory.Unknown,
                        twoHanded = false,
                        visualId = e.item.itemId
                    });
                }
            }

            _equipment?.ApplySnapshot(equipSnapshots);

            if (snapshot.quickSlots != null)
            {
                for (int i = 0; i < snapshot.quickSlots.Length; i++)
                {
                    QuickSlotDto quick = snapshot.quickSlots[i];
                    if (quick == null || string.IsNullOrWhiteSpace(quick.kind))
                        continue;

                    if (!TryParseQuickSlotKind(quick.kind, out InventoryClientSystem.QuickSlotKind kind))
                        continue;

                    if (quick.slotIndex >= 0)
                        _inventory?.SetQuickSlot(kind, quick.slotIndex);
                    else
                        _inventory?.ClearQuickSlot(kind);
                }
            }

            SnapshotApplied?.Invoke(snapshot);
        }

        private void ApplyDelta(InventoryEquipmentDeltaDto delta)
        {
            if (delta == null)
                return;

            if (delta.serverRevision > 0 && delta.serverRevision < _lastServerRevision)
            {
                Debug.LogWarning($"[InventoryEquipmentService] Ignored stale delta revision {delta.serverRevision} < {_lastServerRevision}.");
                return;
            }

            if (delta.serverRevision > 0)
                _lastServerRevision = delta.serverRevision;

            if (delta.inventory != null)
            {
                for (int i = 0; i < delta.inventory.Length; i++)
                {
                    InventorySlotDeltaDto slotDelta = delta.inventory[i];
                    if (slotDelta == null)
                        continue;

                    if (slotDelta.remove)
                    {
                        int removeIndex = slotDelta.slot != null ? slotDelta.slot.slotIndex : -1;
                        if (removeIndex >= 0)
                        {
                            _inventory?.ApplyDelta(new InventoryClientSystem.InventoryDelta
                            {
                                HasRemoveSlot = true,
                                RemoveSlotIndex = removeIndex
                            });
                        }

                        continue;
                    }

                    if (slotDelta.slot == null || slotDelta.slot.item == null)
                        continue;

                    _inventory?.ApplyDelta(new InventoryClientSystem.InventoryDelta
                    {
                        HasUpsertSlot = true,
                        UpsertSlot = new InventoryClientSystem.InventorySlotSnapshot
                        {
                            slotIndex = slotDelta.slot.slotIndex,
                            itemId = slotDelta.slot.item.itemId,
                            quantity = slotDelta.slot.item.quantity,
                            maxStack = Math.Max(1, slotDelta.slot.item.maxStack),
                            durabilityCurrent = slotDelta.slot.item.durabilityCurrent,
                            durabilityMax = slotDelta.slot.item.durabilityMax,
                            flags = InventoryClientSystem.InventoryItemFlags.None
                        }
                    });
                }
            }

            if (delta.equipment != null)
            {
                for (int i = 0; i < delta.equipment.Length; i++)
                {
                    EquippedItemDeltaDto equipDelta = delta.equipment[i];
                    if (equipDelta == null || string.IsNullOrWhiteSpace(equipDelta.slot))
                        continue;

                    if (equipDelta.remove)
                    {
                        _equipment?.ApplyDelta(new EquipmentClientSystem.EquipmentDelta
                        {
                            HasRemoveSlot = true,
                            RemoveSlot = equipDelta.slot
                        });

                        continue;
                    }

                    int itemId = equipDelta.equipped != null && equipDelta.equipped.item != null
                        ? equipDelta.equipped.item.itemId
                        : 0;

                    _equipment?.ApplyDelta(new EquipmentClientSystem.EquipmentDelta
                    {
                        HasUpsertSlot = true,
                        UpsertSlot = new EquipmentClientSystem.EquipmentSlotSnapshot
                        {
                            slot = equipDelta.slot,
                            itemId = itemId,
                            type = string.Empty,
                            subtype = string.Empty,
                            family = string.Empty,
                            category = EquipmentClientSystem.EquipItemCategory.Unknown,
                            twoHanded = false,
                            visualId = itemId
                        }
                    });
                }
            }

            if (delta.quickSlots != null)
            {
                for (int i = 0; i < delta.quickSlots.Length; i++)
                {
                    QuickSlotDeltaDto quickDelta = delta.quickSlots[i];
                    if (quickDelta == null || !TryParseQuickSlotKind(quickDelta.kind, out InventoryClientSystem.QuickSlotKind kind))
                        continue;

                    if (quickDelta.remove)
                    {
                        _inventory?.ApplyDelta(new InventoryClientSystem.InventoryDelta
                        {
                            HasRemoveQuickSlot = true,
                            RemoveQuickSlotKind = kind
                        });

                        continue;
                    }

                    if (quickDelta.slotIndex < 0)
                        continue;

                    _inventory?.ApplyDelta(new InventoryClientSystem.InventoryDelta
                    {
                        HasUpsertQuickSlot = true,
                        UpsertQuickSlot = new InventoryClientSystem.QuickSlotSnapshot
                        {
                            kind = kind,
                            slotIndex = quickDelta.slotIndex
                        }
                    });
                }
            }

            DeltaApplied?.Invoke(delta);
        }

        private static bool TryParseQuickSlotKind(string raw, out InventoryClientSystem.QuickSlotKind kind)
        {
            kind = InventoryClientSystem.QuickSlotKind.Unknown;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            return Enum.TryParse(raw, true, out kind) && kind != InventoryClientSystem.QuickSlotKind.Unknown;
        }

        private void HandleResult(InventoryOperationResultDto result)
        {
            if (result != null && result.serverRevision > 0)
                _lastServerRevision = result.serverRevision;

            if (result != null && result.success)
            {
                OperationSucceeded?.Invoke(result.message ?? "Inventory operation completed.");
                return;
            }

            OperationFailed?.Invoke(result != null ? result.message : "Inventory operation failed.");
        }
    }
}
