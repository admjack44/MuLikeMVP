using System.Collections.Generic;

namespace MuLike.Server.Game.Repositories
{
    public sealed class EquippedItemRecord
    {
        public long ItemInstanceId { get; set; }
        public int ItemId { get; set; }
        public ItemInstanceOptionsRecord Options { get; set; } = new ItemInstanceOptionsRecord();
    }

    public sealed class EquipmentRepository
    {
        private readonly Dictionary<int, Dictionary<string, EquippedItemRecord>> _storage = new();

        public Dictionary<string, EquippedItemRecord> Load(int characterId)
        {
            if (!_storage.TryGetValue(characterId, out var value))
            {
                value = new Dictionary<string, EquippedItemRecord>(System.StringComparer.OrdinalIgnoreCase);
                _storage[characterId] = value;
            }

            return value;
        }

        public void Replace(int characterId, IReadOnlyDictionary<string, EquippedItemRecord> items)
        {
            var value = Load(characterId);
            value.Clear();

            if (items == null)
                return;

            foreach (KeyValuePair<string, EquippedItemRecord> entry in items)
            {
                value[entry.Key] = new EquippedItemRecord
                {
                    ItemInstanceId = entry.Value.ItemInstanceId,
                    ItemId = entry.Value.ItemId,
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
