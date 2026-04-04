using System;
using System.Collections.Generic;
using MuLike.Data.Catalogs;

namespace MuLike.Systems
{
    /// <summary>
    /// Maintains authoritative-on-client inventory cache and exposes granular mutation events.
    /// </summary>
    public class InventoryClientSystem
    {
        [Flags]
        public enum InventoryItemFlags
        {
            None = 0,
            Bound = 1 << 0,
            Locked = 1 << 1,
            Quest = 1 << 2,
            Equipped = 1 << 3,
            Tradable = 1 << 4
        }

        [Serializable]
        public struct InventorySlotSnapshot
        {
            public int slotIndex;
            public int itemId;
            public int quantity;
            public int maxStack;
            public int durabilityCurrent;
            public int durabilityMax;
            public InventoryItemFlags flags;
        }

        [Serializable]
        public sealed class InventorySnapshot
        {
            public List<InventorySlotSnapshot> slots = new();
        }

        public struct InventorySlot
        {
            public int SlotIndex;
            public int ItemId;
            public int Quantity;
            public int MaxStack;
            public int DurabilityCurrent;
            public int DurabilityMax;
            public InventoryItemFlags Flags;

            public bool IsEmpty => ItemId <= 0 || Quantity <= 0;

            public static InventorySlot Empty(int slotIndex)
            {
                return new InventorySlot
                {
                    SlotIndex = slotIndex,
                    ItemId = 0,
                    Quantity = 0,
                    MaxStack = 1,
                    DurabilityCurrent = 0,
                    DurabilityMax = 0,
                    Flags = InventoryItemFlags.None
                };
            }

            public bool CanStackWith(in InventorySlot other)
            {
                if (IsEmpty || other.IsEmpty) return false;

                return ItemId == other.ItemId
                    && DurabilityCurrent == other.DurabilityCurrent
                    && DurabilityMax == other.DurabilityMax
                    && Flags == other.Flags;
            }

            public InventorySlotSnapshot ToSnapshot()
            {
                return new InventorySlotSnapshot
                {
                    slotIndex = SlotIndex,
                    itemId = ItemId,
                    quantity = Quantity,
                    maxStack = MaxStack,
                    durabilityCurrent = DurabilityCurrent,
                    durabilityMax = DurabilityMax,
                    flags = Flags
                };
            }

            public static InventorySlot FromSnapshot(in InventorySlotSnapshot snapshot)
            {
                return new InventorySlot
                {
                    SlotIndex = snapshot.slotIndex,
                    ItemId = snapshot.itemId,
                    Quantity = snapshot.quantity,
                    MaxStack = snapshot.maxStack,
                    DurabilityCurrent = snapshot.durabilityCurrent,
                    DurabilityMax = snapshot.durabilityMax,
                    Flags = snapshot.flags
                };
            }
        }

        public struct InventorySlotChange
        {
            public InventorySlot Previous;
            public InventorySlot Current;
        }

        public struct InventoryMoveEvent
        {
            public int FromSlotIndex;
            public int ToSlotIndex;
            public int Quantity;
        }

        public struct InventorySplitEvent
        {
            public int SourceSlotIndex;
            public int TargetSlotIndex;
            public int Quantity;
        }

        public struct InventorySwapEvent
        {
            public int LeftSlotIndex;
            public int RightSlotIndex;
        }

        /// <summary>
        /// Legacy lightweight slot payload kept for compatibility with existing callers.
        /// </summary>
        public struct ItemSlot
        {
            public int SlotIndex;
            public int ItemId;
            public int Quantity;
        }

        private readonly Dictionary<int, InventorySlot> _slotsByIndex = new();
        private readonly List<InventorySlot> _slotsCache = new();
        private readonly CatalogResolver _catalogResolver;

        public int SlotCapacity { get; }
        public IReadOnlyList<InventorySlot> Slots => _slotsCache;

        public event Action OnInventoryChanged;
        public event Action<InventorySlotChange> OnSlotChanged;
        public event Action<int> OnSlotRemoved;
        public event Action<InventoryMoveEvent> OnItemMoved;
        public event Action<InventorySplitEvent> OnStackSplit;
        public event Action<InventorySwapEvent> OnSlotsSwapped;

        public InventoryClientSystem(int slotCapacity = 128, CatalogResolver catalogResolver = null)
        {
            SlotCapacity = slotCapacity > 0 ? slotCapacity : 128;
            _catalogResolver = catalogResolver;
        }

        public bool TryGetSlot(int slotIndex, out InventorySlot slot)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                slot = default;
                return false;
            }

            if (_slotsByIndex.TryGetValue(slotIndex, out slot))
                return true;

            slot = InventorySlot.Empty(slotIndex);
            return false;
        }

        public bool TryFindFirstByItemId(int itemId, out InventorySlot slot)
        {
            foreach (var pair in _slotsByIndex)
            {
                if (pair.Value.ItemId == itemId)
                {
                    slot = pair.Value;
                    return true;
                }
            }

            slot = default;
            return false;
        }

        public IReadOnlyList<InventorySlot> FindAllByItemId(int itemId)
        {
            var matches = new List<InventorySlot>();

            foreach (var pair in _slotsByIndex)
            {
                if (pair.Value.ItemId == itemId)
                    matches.Add(pair.Value);
            }

            matches.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            return matches;
        }

        public void ApplySnapshot(IEnumerable<ItemSlot> slots)
        {
            _slotsByIndex.Clear();

            if (slots != null)
            {
                foreach (var legacy in slots)
                {
                    var slot = new InventorySlot
                    {
                        SlotIndex = legacy.SlotIndex,
                        ItemId = legacy.ItemId,
                        Quantity = legacy.Quantity,
                        MaxStack = legacy.Quantity > 0 ? legacy.Quantity : 1,
                        DurabilityCurrent = 0,
                        DurabilityMax = 0,
                        Flags = InventoryItemFlags.None
                    };

                    if (TryValidateSlot(slot, out _))
                        _slotsByIndex[slot.SlotIndex] = slot;
                }
            }

            RebuildCacheAndBroadcast();
        }

        public void ApplySnapshot(IEnumerable<InventorySlotSnapshot> slots)
        {
            _slotsByIndex.Clear();

            if (slots != null)
            {
                foreach (var snapshot in slots)
                {
                    InventorySlot slot = InventorySlot.FromSnapshot(snapshot);
                    if (TryValidateSlot(slot, out _))
                        _slotsByIndex[slot.SlotIndex] = slot;
                }
            }

            RebuildCacheAndBroadcast();
        }

        public void ApplySnapshot(InventorySnapshot snapshot)
        {
            ApplySnapshot(snapshot != null ? snapshot.slots : null);
        }

        public void UpdateSlot(ItemSlot slot)
        {
            var upgraded = new InventorySlot
            {
                SlotIndex = slot.SlotIndex,
                ItemId = slot.ItemId,
                Quantity = slot.Quantity,
                MaxStack = slot.Quantity > 0 ? slot.Quantity : 1,
                DurabilityCurrent = 0,
                DurabilityMax = 0,
                Flags = InventoryItemFlags.None
            };

            UpdateSlot(upgraded, out _);
        }

        public bool UpdateSlot(InventorySlot slot, out string validationError)
        {
            if (!TryValidateSlot(slot, out validationError))
                return false;

            InventorySlot previous = _slotsByIndex.TryGetValue(slot.SlotIndex, out var existing)
                ? existing
                : InventorySlot.Empty(slot.SlotIndex);

            StoreSlot(slot);

            RebuildCacheAndBroadcast();
            OnSlotChanged?.Invoke(new InventorySlotChange { Previous = previous, Current = slot });
            return true;
        }

        public bool TrySwapSlots(int leftSlotIndex, int rightSlotIndex, out string error)
        {
            error = string.Empty;

            if (!IsValidSlotIndex(leftSlotIndex) || !IsValidSlotIndex(rightSlotIndex))
            {
                error = "Invalid slot index for swap.";
                return false;
            }

            if (leftSlotIndex == rightSlotIndex)
                return true;

            InventorySlot left = _slotsByIndex.TryGetValue(leftSlotIndex, out var leftValue)
                ? leftValue
                : InventorySlot.Empty(leftSlotIndex);
            InventorySlot right = _slotsByIndex.TryGetValue(rightSlotIndex, out var rightValue)
                ? rightValue
                : InventorySlot.Empty(rightSlotIndex);

            left.SlotIndex = rightSlotIndex;
            right.SlotIndex = leftSlotIndex;

            if (!TryValidateSlot(left, out error) || !TryValidateSlot(right, out error))
                return false;

            StoreSlot(left);
            StoreSlot(right);

            RebuildCacheAndBroadcast();
            OnSlotsSwapped?.Invoke(new InventorySwapEvent { LeftSlotIndex = leftSlotIndex, RightSlotIndex = rightSlotIndex });
            return true;
        }

        public bool TryMoveItem(int fromSlotIndex, int toSlotIndex, int quantity, out string error)
        {
            error = string.Empty;

            if (quantity <= 0)
            {
                error = "Move quantity must be greater than zero.";
                return false;
            }

            if (!TryGetSlot(fromSlotIndex, out var from) || from.IsEmpty)
            {
                error = "Source slot is empty.";
                return false;
            }

            if (!IsValidSlotIndex(toSlotIndex))
            {
                error = "Target slot index is out of bounds.";
                return false;
            }

            if (fromSlotIndex == toSlotIndex)
                return true;

            if (quantity > from.Quantity)
            {
                error = "Not enough quantity in source slot.";
                return false;
            }

            bool targetExists = _slotsByIndex.TryGetValue(toSlotIndex, out var to);
            if (!targetExists || to.IsEmpty)
            {
                InventorySlot moved = from;
                moved.SlotIndex = toSlotIndex;
                moved.Quantity = quantity;

                if (!TryValidateSlot(moved, out error))
                    return false;

                from.Quantity -= quantity;
                if (from.Quantity <= 0)
                    from = InventorySlot.Empty(fromSlotIndex);

                StoreSlot(from);
                StoreSlot(moved);

                RebuildCacheAndBroadcast();
                OnItemMoved?.Invoke(new InventoryMoveEvent { FromSlotIndex = fromSlotIndex, ToSlotIndex = toSlotIndex, Quantity = quantity });
                return true;
            }

            if (!from.CanStackWith(to))
            {
                error = "Target slot contains a different item. Use swap operation instead.";
                return false;
            }

            int freeSpace = to.MaxStack - to.Quantity;
            if (freeSpace < quantity)
            {
                error = "Target stack has insufficient capacity.";
                return false;
            }

            to.Quantity += quantity;
            from.Quantity -= quantity;

            if (from.Quantity <= 0)
                from = InventorySlot.Empty(fromSlotIndex);

            StoreSlot(from);
            StoreSlot(to);

            RebuildCacheAndBroadcast();
            OnItemMoved?.Invoke(new InventoryMoveEvent { FromSlotIndex = fromSlotIndex, ToSlotIndex = toSlotIndex, Quantity = quantity });
            return true;
        }

        public bool TrySplitStack(int sourceSlotIndex, int targetSlotIndex, int splitQuantity, out string error)
        {
            error = string.Empty;

            if (splitQuantity <= 0)
            {
                error = "Split quantity must be greater than zero.";
                return false;
            }

            if (!_slotsByIndex.TryGetValue(sourceSlotIndex, out var source) || source.IsEmpty)
            {
                error = "Source slot is empty.";
                return false;
            }

            if (!IsValidSlotIndex(targetSlotIndex))
            {
                error = "Target slot index is out of bounds.";
                return false;
            }

            if (_slotsByIndex.TryGetValue(targetSlotIndex, out var target) && !target.IsEmpty)
            {
                error = "Target slot must be empty for stack split.";
                return false;
            }

            if (source.Quantity <= splitQuantity)
            {
                error = "Split quantity must be lower than source stack quantity.";
                return false;
            }

            InventorySlot created = source;
            created.SlotIndex = targetSlotIndex;
            created.Quantity = splitQuantity;

            source.Quantity -= splitQuantity;

            if (!TryValidateSlot(source, out error) || !TryValidateSlot(created, out error))
                return false;

            StoreSlot(source);
            StoreSlot(created);

            RebuildCacheAndBroadcast();
            OnStackSplit?.Invoke(new InventorySplitEvent
            {
                SourceSlotIndex = sourceSlotIndex,
                TargetSlotIndex = targetSlotIndex,
                Quantity = splitQuantity
            });
            return true;
        }

        public void RemoveSlot(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
                return;

            bool removed = _slotsByIndex.Remove(slotIndex);
            if (!removed)
                return;

            RebuildCacheAndBroadcast();
            OnSlotRemoved?.Invoke(slotIndex);
        }

        public bool TryConsumeFromSlot(int slotIndex, int quantity, out InventorySlot resultingSlot, out string error)
        {
            error = string.Empty;
            resultingSlot = default;

            if (quantity <= 0)
            {
                error = "Consume quantity must be greater than zero.";
                return false;
            }

            if (!_slotsByIndex.TryGetValue(slotIndex, out InventorySlot slot) || slot.IsEmpty)
            {
                error = "Source slot is empty.";
                return false;
            }

            if (quantity > slot.Quantity)
            {
                error = "Not enough quantity in source slot.";
                return false;
            }

            slot.Quantity -= quantity;

            if (slot.Quantity <= 0)
            {
                _slotsByIndex.Remove(slotIndex);
                resultingSlot = InventorySlot.Empty(slotIndex);
                RebuildCacheAndBroadcast();
                OnSlotRemoved?.Invoke(slotIndex);
                return true;
            }

            if (!TryValidateSlot(slot, out error))
                return false;

            _slotsByIndex[slotIndex] = slot;
            resultingSlot = slot;

            RebuildCacheAndBroadcast();
            OnSlotChanged?.Invoke(new InventorySlotChange
            {
                Previous = new InventorySlot
                {
                    SlotIndex = slot.SlotIndex,
                    ItemId = slot.ItemId,
                    Quantity = slot.Quantity + quantity,
                    MaxStack = slot.MaxStack,
                    DurabilityCurrent = slot.DurabilityCurrent,
                    DurabilityMax = slot.DurabilityMax,
                    Flags = slot.Flags
                },
                Current = slot
            });

            return true;
        }

        public InventorySnapshot CreateSnapshot()
        {
            var snapshot = new InventorySnapshot();

            for (int i = 0; i < _slotsCache.Count; i++)
            {
                snapshot.slots.Add(_slotsCache[i].ToSnapshot());
            }

            return snapshot;
        }

        private bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SlotCapacity;
        }

        private bool TryValidateSlot(InventorySlot slot, out string error)
        {
            error = string.Empty;

            if (!IsValidSlotIndex(slot.SlotIndex))
            {
                error = $"Slot index out of range: {slot.SlotIndex}";
                return false;
            }

            if (slot.IsEmpty)
                return true;

            if (slot.ItemId <= 0)
            {
                error = "ItemId must be greater than zero for non-empty slot.";
                return false;
            }

            if (slot.Quantity <= 0)
            {
                error = "Quantity must be greater than zero for non-empty slot.";
                return false;
            }

            if (slot.MaxStack < 1)
            {
                error = "MaxStack must be at least 1.";
                return false;
            }

            if (_catalogResolver != null
                && _catalogResolver.TryGetItemDefinition(slot.ItemId, out ItemDefinition definition))
            {
                if (!definition.Stackable && slot.Quantity > 1)
                {
                    error = $"Item {slot.ItemId} is not stackable.";
                    return false;
                }

                if (definition.MaxStack > 0 && slot.Quantity > definition.MaxStack)
                {
                    error = $"Item {slot.ItemId} exceeds catalog MaxStack ({definition.MaxStack}).";
                    return false;
                }

                if (slot.MaxStack > definition.MaxStack && definition.MaxStack > 0)
                {
                    slot.MaxStack = definition.MaxStack;
                }
            }

            if (slot.Quantity > slot.MaxStack)
            {
                error = "Quantity cannot exceed MaxStack.";
                return false;
            }

            if (slot.DurabilityMax < 0)
            {
                error = "DurabilityMax cannot be negative.";
                return false;
            }

            if (slot.DurabilityCurrent < 0)
            {
                error = "DurabilityCurrent cannot be negative.";
                return false;
            }

            if (slot.DurabilityMax > 0 && slot.DurabilityCurrent > slot.DurabilityMax)
            {
                error = "DurabilityCurrent cannot exceed DurabilityMax.";
                return false;
            }

            return true;
        }

        private void StoreSlot(InventorySlot slot)
        {
            if (slot.IsEmpty)
                _slotsByIndex.Remove(slot.SlotIndex);
            else
                _slotsByIndex[slot.SlotIndex] = slot;
        }

        private void RebuildCacheAndBroadcast()
        {
            _slotsCache.Clear();
            foreach (var pair in _slotsByIndex)
                _slotsCache.Add(pair.Value);

            _slotsCache.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            OnInventoryChanged?.Invoke();
        }
    }
}
