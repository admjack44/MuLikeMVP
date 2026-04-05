using System.Collections.Generic;

namespace MuLike.Server.Game.Repositories
{
    public sealed class ItemInstanceOptionsRecord
    {
        public int EnhancementLevel { get; set; }
        public int ExcellentFlags { get; set; }
        public int[] Sockets { get; set; } = new[] { -1, -1, -1, -1, -1 };
        public int SellValue { get; set; }
    }

    public sealed class InventoryItemRecord
    {
        public long ItemInstanceId { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public ItemInstanceOptionsRecord Options { get; set; } = new ItemInstanceOptionsRecord();
    }

    public sealed class InventoryRepository
    {
        private readonly Dictionary<int, Dictionary<int, InventoryItemRecord>> _storage = new();

        public Dictionary<int, InventoryItemRecord> Load(int characterId)
        {
            if (!_storage.TryGetValue(characterId, out var value))
            {
                value = new Dictionary<int, InventoryItemRecord>();
                _storage[characterId] = value;
            }

            return value;
        }

        public void Replace(int characterId, IReadOnlyDictionary<int, InventoryItemRecord> items)
        {
            var value = Load(characterId);
            value.Clear();

            if (items == null)
                return;

            foreach (KeyValuePair<int, InventoryItemRecord> entry in items)
            {
                value[entry.Key] = new InventoryItemRecord
                {
                    ItemInstanceId = entry.Value.ItemInstanceId,
                    ItemId = entry.Value.ItemId,
                    Quantity = entry.Value.Quantity,
                    Options = CloneOptions(entry.Value.Options)
                };
            }
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
    }
}
