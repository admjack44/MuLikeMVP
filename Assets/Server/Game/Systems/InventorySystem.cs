using System;
using System.Collections.Generic;
using MuLike.Server.Game.Definitions;
using MuLike.Server.Game.Repositories;

namespace MuLike.Server.Game.Systems
{
    public sealed class InventorySystem
    {
        private readonly ItemDatabase _itemDatabase;
        private long _nextItemInstanceId = 1;

        public int Width { get; } = 8;
        public int Height { get; } = 8;
        public int Capacity => Width * Height;

        public InventorySystem(ItemDatabase itemDatabase)
        {
            _itemDatabase = itemDatabase ?? throw new ArgumentNullException(nameof(itemDatabase));
        }

        public bool TryAddItem(InventoryRepository repository, int characterId, int itemId, int amount, out int remaining)
        {
            remaining = amount;
            if (amount <= 0)
                return false;

            if (!_itemDatabase.TryGet(itemId, out var definition))
                return false;

            var inventory = repository.Load(characterId);

            // Fill existing stacks first.
            if (definition.IsStackable)
            {
                foreach (var kvp in inventory)
                {
                    InventoryItemRecord record = kvp.Value;
                    if (!CanStack(definition, record, null, itemId))
                        continue;

                    int free = Math.Max(0, definition.MaxStack - record.Quantity);
                    if (free <= 0)
                        continue;

                    int toMove = Math.Min(free, remaining);
                    record.Quantity += toMove;
                    remaining -= toMove;
                    if (remaining == 0)
                        return true;
                }
            }

            for (int slot = 0; slot < Capacity && remaining > 0; slot++)
            {
                if (inventory.ContainsKey(slot))
                    continue;

                int quantity = definition.IsStackable
                    ? Math.Min(definition.MaxStack, remaining)
                    : 1;

                inventory[slot] = new InventoryItemRecord
                {
                    ItemInstanceId = _nextItemInstanceId++,
                    ItemId = itemId,
                    Quantity = quantity,
                    Options = CreateDefaultInstanceOptions(definition)
                };

                remaining -= quantity;
            }

            return remaining == 0;
        }

        public bool TryMoveItem(InventoryRepository repository, int characterId, int fromSlot, int toSlot)
        {
            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot) || fromSlot == toSlot)
                return false;

            var inventory = repository.Load(characterId);
            if (!inventory.TryGetValue(fromSlot, out var fromItem))
                return false;

            if (!inventory.TryGetValue(toSlot, out var toItem))
            {
                inventory.Remove(fromSlot);
                inventory[toSlot] = fromItem;
                return true;
            }

            if (fromItem.ItemId == toItem.ItemId && _itemDatabase.TryGet(fromItem.ItemId, out var definition) && definition.IsStackable)
            {
                if (CanStack(definition, toItem, fromItem, fromItem.ItemId))
                {
                    int free = Math.Max(0, definition.MaxStack - toItem.Quantity);
                    if (free > 0)
                    {
                        int transfer = Math.Min(free, fromItem.Quantity);
                        toItem.Quantity += transfer;
                        fromItem.Quantity -= transfer;
                        if (fromItem.Quantity <= 0)
                            inventory.Remove(fromSlot);
                        return true;
                    }
                }
            }

            // Swap when merge is not possible.
            inventory[fromSlot] = toItem;
            inventory[toSlot] = fromItem;
            return true;
        }

        public bool TrySplitStack(InventoryRepository repository, int characterId, int fromSlot, int toSlot, int amount)
        {
            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot) || fromSlot == toSlot || amount <= 0)
                return false;

            var inventory = repository.Load(characterId);
            if (!inventory.TryGetValue(fromSlot, out var fromItem))
                return false;

            if (inventory.ContainsKey(toSlot))
                return false;

            if (!_itemDatabase.TryGet(fromItem.ItemId, out var definition) || !definition.IsStackable)
                return false;

            if (fromItem.Quantity <= amount)
                return false;

            fromItem.Quantity -= amount;
            inventory[toSlot] = new InventoryItemRecord
            {
                ItemInstanceId = _nextItemInstanceId++,
                ItemId = fromItem.ItemId,
                Quantity = amount,
                Options = CloneOptions(fromItem.Options)
            };

            return true;
        }

        public bool TryDropItem(InventoryRepository repository, int characterId, int fromSlot, int amount, out int itemId, out int droppedAmount)
        {
            itemId = 0;
            droppedAmount = 0;
            if (!IsValidSlot(fromSlot) || amount <= 0)
                return false;

            var inventory = repository.Load(characterId);
            if (!inventory.TryGetValue(fromSlot, out var fromItem))
                return false;

            int actual = Math.Min(amount, fromItem.Quantity);
            if (actual <= 0)
                return false;

            fromItem.Quantity -= actual;
            if (fromItem.Quantity <= 0)
                inventory.Remove(fromSlot);

            itemId = fromItem.ItemId;
            droppedAmount = actual;
            return true;
        }

        public bool TryTakeFromSlot(InventoryRepository repository, int characterId, int slotIndex, out InventoryItemRecord item)
        {
            item = null;
            if (!IsValidSlot(slotIndex))
                return false;

            var inventory = repository.Load(characterId);
            if (!inventory.TryGetValue(slotIndex, out item))
                return false;

            inventory.Remove(slotIndex);
            return true;
        }

        public bool TryPlaceInSlot(InventoryRepository repository, int characterId, int slotIndex, InventoryItemRecord item)
        {
            if (item == null || !IsValidSlot(slotIndex))
                return false;

            var inventory = repository.Load(characterId);
            if (inventory.ContainsKey(slotIndex))
                return false;

            inventory[slotIndex] = item;
            return true;
        }

        private static ItemInstanceOptionsRecord CreateDefaultInstanceOptions(ItemDefinition definition)
        {
            return new ItemInstanceOptionsRecord
            {
                EnhancementLevel = 0,
                ExcellentFlags = 0,
                SellValue = definition.SellValue,
                Sockets = definition.AllowSockets
                    ? CreateDefaultSockets(definition.MaxSockets)
                    : new[] { -1, -1, -1, -1, -1 }
            };
        }

        private static int[] CreateDefaultSockets(int maxSockets)
        {
            int slots = maxSockets;
            if (slots < 0) slots = 0;
            if (slots > 5) slots = 5;
            var sockets = new[] { -1, -1, -1, -1, -1 };
            for (int i = 0; i < slots; i++)
                sockets[i] = 0;

            return sockets;
        }

        private static ItemInstanceOptionsRecord CloneOptions(ItemInstanceOptionsRecord source)
        {
            if (source == null)
                return new ItemInstanceOptionsRecord();

            return new ItemInstanceOptionsRecord
            {
                EnhancementLevel = source.EnhancementLevel,
                ExcellentFlags = source.ExcellentFlags,
                SellValue = source.SellValue,
                Sockets = source.Sockets != null ? (int[])source.Sockets.Clone() : new[] { -1, -1, -1, -1, -1 }
            };
        }

        private static bool CanStack(ItemDefinition definition, InventoryItemRecord targetStack, InventoryItemRecord sourceStack, int expectedItemId)
        {
            if (targetStack == null || targetStack.ItemId != expectedItemId)
                return false;

            if (definition.StackRule == ItemStackRule.ByItemId)
                return true;

            if (definition.StackRule == ItemStackRule.ByItemAndEnhancement)
            {
                int targetEnh = targetStack.Options?.EnhancementLevel ?? 0;
                int sourceEnh = sourceStack?.Options?.EnhancementLevel ?? targetEnh;
                return targetEnh == sourceEnh;
            }

            return false;
        }

        public int FindFirstEmptySlot(InventoryRepository repository, int characterId)
        {
            var inventory = repository.Load(characterId);
            for (int i = 0; i < Capacity; i++)
            {
                if (!inventory.ContainsKey(i))
                    return i;
            }

            return -1;
        }

        private bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < Capacity;
        }
    }
}
