using System;
using System.Collections.Generic;
using MuLike.Data.Catalogs;

namespace MuLike.Systems
{
    /// <summary>
    /// Tracks equipped items with slot validation, snapshot synchronization, and appearance hooks.
    /// </summary>
    public class EquipmentClientSystem
    {
        public enum EquipSlot
        {
            Helm,
            Armor,
            Pants,
            Gloves,
            Boots,
            WeaponMain,
            WeaponOffhand,
            RingLeft,
            RingRight,
            Pendant,
            Wings,
            Pet
        }

        public enum EquipItemCategory
        {
            Unknown,
            Weapon,
            Shield,
            Armor,
            Accessory,
            Wings,
            Pet,
            Costume
        }

        [Serializable]
        public struct EquipmentSlotSnapshot
        {
            public string slot;
            public int itemId;
            public string type;
            public string subtype;
            public string family;
            public EquipItemCategory category;
            public bool twoHanded;
            public int visualId;
        }

        [Serializable]
        public sealed class EquipmentSnapshot
        {
            public List<EquipmentSlotSnapshot> slots = new();
        }

        public struct EquippedItemDescriptor
        {
            public int ItemId;
            public string Type;
            public string Subtype;
            public string Family;
            public EquipItemCategory Category;
            public bool IsTwoHanded;
            public int VisualId;

            public bool IsEmpty => ItemId <= 0;

            public EquipmentSlotSnapshot ToSnapshot(EquipSlot slot)
            {
                return new EquipmentSlotSnapshot
                {
                    slot = slot.ToString(),
                    itemId = ItemId,
                    type = Type,
                    subtype = Subtype,
                    family = Family,
                    category = Category,
                    twoHanded = IsTwoHanded,
                    visualId = VisualId
                };
            }

            public static EquippedItemDescriptor FromSnapshot(EquipmentSlotSnapshot snapshot)
            {
                return new EquippedItemDescriptor
                {
                    ItemId = snapshot.itemId,
                    Type = snapshot.type,
                    Subtype = snapshot.subtype,
                    Family = snapshot.family,
                    Category = snapshot.category,
                    IsTwoHanded = snapshot.twoHanded,
                    VisualId = snapshot.visualId
                };
            }
        }

        public struct EquipmentSlotState
        {
            public EquipSlot Slot;
            public EquippedItemDescriptor Item;

            public bool IsEmpty => Item.IsEmpty;
            public int AppearanceId => Item.VisualId > 0 ? Item.VisualId : Item.ItemId;
        }

        public struct EquipmentSlotChange
        {
            public EquipSlot Slot;
            public EquipmentSlotState Previous;
            public EquipmentSlotState Current;
        }

        public struct AppearanceChange
        {
            public EquipSlot Slot;
            public int PreviousAppearanceId;
            public int CurrentAppearanceId;
        }

        private sealed class SlotRule
        {
            public HashSet<string> AllowedTypes { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> AllowedFamilies { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<EquipItemCategory> AllowedCategories { get; } = new();

            public bool IsMatch(EquippedItemDescriptor item)
            {
                if (item.IsEmpty) return true;

                bool hasItemHints = !string.IsNullOrWhiteSpace(item.Type)
                    || !string.IsNullOrWhiteSpace(item.Family)
                    || item.Category != EquipItemCategory.Unknown;

                // Keep compatibility for legacy callers that only provide itemId.
                if (!hasItemHints) return true;

                if (item.Category != EquipItemCategory.Unknown && AllowedCategories.Contains(item.Category))
                    return true;

                if (!string.IsNullOrWhiteSpace(item.Type) && AllowedTypes.Contains(item.Type))
                    return true;

                if (!string.IsNullOrWhiteSpace(item.Family) && AllowedFamilies.Contains(item.Family))
                    return true;

                return false;
            }
        }

        private readonly Dictionary<EquipSlot, EquipmentSlotState> _equippedBySlot = new();
        private readonly Dictionary<EquipSlot, SlotRule> _rulesBySlot;
        private readonly CatalogResolver _catalogResolver;

        public IReadOnlyDictionary<EquipSlot, EquipmentSlotState> Equipped => _equippedBySlot;

        public event Action OnEquipmentChanged;
        public event Action<EquipmentSlotChange> OnSlotChanged;
        public event Action<AppearanceChange> OnAppearanceChanged;

        public EquipmentClientSystem(CatalogResolver catalogResolver = null)
        {
            _rulesBySlot = BuildSlotRules();
            _catalogResolver = catalogResolver;
        }

        public bool TryEquip(EquipSlot slot, EquippedItemDescriptor item, out string error)
        {
            if (!ValidateEquip(slot, item, out error))
                return false;

            EquipmentSlotState previous = GetState(slot);
            EquipmentSlotState current = new EquipmentSlotState
            {
                Slot = slot,
                Item = item
            };

            if (current.IsEmpty)
                _equippedBySlot.Remove(slot);
            else
                _equippedBySlot[slot] = current;

            RaiseChangeEvents(slot, previous, current);
            return true;
        }

        public void SetEquipped(EquipSlot slot, int itemId)
        {
            EquippedItemDescriptor item = BuildDescriptor(itemId);

            TryEquip(slot, item, out _);
        }

        public bool Unequip(EquipSlot slot)
        {
            return TryEquip(slot, default, out _);
        }

        public bool TryGetEquipped(EquipSlot slot, out int itemId)
        {
            if (_equippedBySlot.TryGetValue(slot, out EquipmentSlotState state) && !state.IsEmpty)
            {
                itemId = state.Item.ItemId;
                return true;
            }

            itemId = 0;
            return false;
        }

        public bool TryGetEquippedState(EquipSlot slot, out EquipmentSlotState state)
        {
            if (_equippedBySlot.TryGetValue(slot, out state) && !state.IsEmpty)
                return true;

            state = GetState(slot);
            return false;
        }

        public void ApplySnapshot(IEnumerable<(EquipSlot slot, int itemId)> snapshot)
        {
            var wrapped = new List<EquipmentSlotSnapshot>();

            if (snapshot != null)
            {
                foreach (var entry in snapshot)
                {
                    wrapped.Add(new EquipmentSlotSnapshot
                    {
                        slot = entry.slot.ToString(),
                        itemId = entry.itemId,
                        type = string.Empty,
                        subtype = string.Empty,
                        family = string.Empty,
                        category = EquipItemCategory.Unknown,
                        twoHanded = false,
                        visualId = 0
                    });
                }
            }

            ApplySnapshot(wrapped);
        }

        public void ApplySnapshot(IEnumerable<EquipmentSlotSnapshot> snapshot)
        {
            var previous = CreateSnapshot();
            _equippedBySlot.Clear();

            if (snapshot != null)
            {
                foreach (EquipmentSlotSnapshot entry in snapshot)
                {
                    if (!Enum.TryParse(entry.slot, true, out EquipSlot slot))
                        continue;

                    EquippedItemDescriptor item = EquippedItemDescriptor.FromSnapshot(entry);
                    if (!ValidateEquip(slot, item, out _))
                        continue;

                    if (!item.IsEmpty)
                    {
                        _equippedBySlot[slot] = new EquipmentSlotState
                        {
                            Slot = slot,
                            Item = item
                        };
                    }
                }
            }

            EmitChangesForSnapshots(previous, CreateSnapshot());
            OnEquipmentChanged?.Invoke();
        }

        public void ApplySnapshot(EquipmentSnapshot snapshot)
        {
            ApplySnapshot(snapshot != null ? snapshot.slots : null);
        }

        public EquipmentSnapshot CreateSnapshot()
        {
            var snapshot = new EquipmentSnapshot();

            foreach (var pair in _equippedBySlot)
            {
                if (pair.Value.IsEmpty) continue;
                snapshot.slots.Add(pair.Value.Item.ToSnapshot(pair.Key));
            }

            return snapshot;
        }

        public static IReadOnlyList<EquipmentSlotChange> CalculateSnapshotChanges(EquipmentSnapshot previous, EquipmentSnapshot current)
        {
            var previousMap = BuildSnapshotMap(previous);
            var currentMap = BuildSnapshotMap(current);

            var allSlots = new HashSet<EquipSlot>(previousMap.Keys);
            foreach (var slot in currentMap.Keys)
                allSlots.Add(slot);

            var changes = new List<EquipmentSlotChange>();
            foreach (EquipSlot slot in allSlots)
            {
                EquipmentSlotState oldState = previousMap.TryGetValue(slot, out var p) ? p : EmptyState(slot);
                EquipmentSlotState newState = currentMap.TryGetValue(slot, out var c) ? c : EmptyState(slot);

                if (AreSame(oldState, newState))
                    continue;

                changes.Add(new EquipmentSlotChange
                {
                    Slot = slot,
                    Previous = oldState,
                    Current = newState
                });
            }

            return changes;
        }

        private bool ValidateEquip(EquipSlot slot, EquippedItemDescriptor item, out string error)
        {
            error = string.Empty;

            if (!_rulesBySlot.TryGetValue(slot, out SlotRule rule))
            {
                error = $"No slot rule configured for {slot}.";
                return false;
            }

            if (!rule.IsMatch(item))
            {
                error = $"Item {item.ItemId} is not compatible with slot {slot}.";
                return false;
            }

            if (_catalogResolver != null
                && item.ItemId > 0
                && _catalogResolver.TryGetItemDefinition(item.ItemId, out ItemDefinition definition))
            {
                ItemEquipSlot mappedSlot = MapToItemEquipSlot(slot);
                if (mappedSlot != ItemEquipSlot.None
                    && definition.AllowedEquipSlots != null
                    && definition.AllowedEquipSlots.Count > 0
                    && !definition.AllowedEquipSlots.Contains(mappedSlot))
                {
                    error = $"Item {item.ItemId} does not allow slot {slot} in catalog definition.";
                    return false;
                }
            }

            if (slot == EquipSlot.WeaponOffhand && item.IsTwoHanded)
            {
                error = "Offhand slot cannot equip two-handed items.";
                return false;
            }

            if (slot == EquipSlot.WeaponMain && item.IsTwoHanded)
            {
                EquipmentSlotState offhand = GetState(EquipSlot.WeaponOffhand);
                if (!offhand.IsEmpty)
                {
                    error = "Cannot equip two-handed weapon while offhand slot is occupied.";
                    return false;
                }
            }

            if (slot == EquipSlot.WeaponOffhand)
            {
                EquipmentSlotState main = GetState(EquipSlot.WeaponMain);
                if (!main.IsEmpty && main.Item.IsTwoHanded && !item.IsEmpty)
                {
                    error = "Cannot equip offhand while main-hand has two-handed weapon.";
                    return false;
                }
            }

            return true;
        }

        private EquippedItemDescriptor BuildDescriptor(int itemId)
        {
            if (itemId <= 0)
                return default;

            if (_catalogResolver != null
                && _catalogResolver.TryGetItemDefinition(itemId, out ItemDefinition definition))
            {
                return new EquippedItemDescriptor
                {
                    ItemId = definition.ItemId,
                    Type = definition.Type,
                    Subtype = definition.Subtype,
                    Family = definition.Family,
                    Category = MapCategory(definition.Category),
                    IsTwoHanded = definition.IsTwoHanded,
                    VisualId = definition.ItemId
                };
            }

            return new EquippedItemDescriptor
            {
                ItemId = itemId,
                Category = EquipItemCategory.Unknown,
                Type = string.Empty,
                Family = string.Empty,
                Subtype = string.Empty,
                IsTwoHanded = false,
                VisualId = 0
            };
        }

        private static EquipItemCategory MapCategory(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Weapon => EquipItemCategory.Weapon,
                ItemCategory.Shield => EquipItemCategory.Shield,
                ItemCategory.Armor => EquipItemCategory.Armor,
                ItemCategory.Accessory => EquipItemCategory.Accessory,
                ItemCategory.Wings => EquipItemCategory.Wings,
                ItemCategory.Pet => EquipItemCategory.Pet,
                ItemCategory.Costume => EquipItemCategory.Costume,
                _ => EquipItemCategory.Unknown
            };
        }

        private static ItemEquipSlot MapToItemEquipSlot(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.Helm => ItemEquipSlot.Helm,
                EquipSlot.Armor => ItemEquipSlot.Armor,
                EquipSlot.Pants => ItemEquipSlot.Pants,
                EquipSlot.Gloves => ItemEquipSlot.Gloves,
                EquipSlot.Boots => ItemEquipSlot.Boots,
                EquipSlot.WeaponMain => ItemEquipSlot.WeaponMain,
                EquipSlot.WeaponOffhand => ItemEquipSlot.WeaponOffhand,
                EquipSlot.RingLeft => ItemEquipSlot.RingLeft,
                EquipSlot.RingRight => ItemEquipSlot.RingRight,
                EquipSlot.Pendant => ItemEquipSlot.Pendant,
                EquipSlot.Wings => ItemEquipSlot.Wings,
                EquipSlot.Pet => ItemEquipSlot.Pet,
                _ => ItemEquipSlot.None
            };
        }

        private void RaiseChangeEvents(EquipSlot slot, EquipmentSlotState previous, EquipmentSlotState current)
        {
            if (AreSame(previous, current))
                return;

            OnSlotChanged?.Invoke(new EquipmentSlotChange
            {
                Slot = slot,
                Previous = previous,
                Current = current
            });

            if (previous.AppearanceId != current.AppearanceId)
            {
                OnAppearanceChanged?.Invoke(new AppearanceChange
                {
                    Slot = slot,
                    PreviousAppearanceId = previous.AppearanceId,
                    CurrentAppearanceId = current.AppearanceId
                });
            }

            OnEquipmentChanged?.Invoke();
        }

        private void EmitChangesForSnapshots(EquipmentSnapshot previous, EquipmentSnapshot current)
        {
            IReadOnlyList<EquipmentSlotChange> changes = CalculateSnapshotChanges(previous, current);
            for (int i = 0; i < changes.Count; i++)
            {
                EquipmentSlotChange change = changes[i];
                OnSlotChanged?.Invoke(change);

                int previousAppearance = change.Previous.AppearanceId;
                int currentAppearance = change.Current.AppearanceId;
                if (previousAppearance != currentAppearance)
                {
                    OnAppearanceChanged?.Invoke(new AppearanceChange
                    {
                        Slot = change.Slot,
                        PreviousAppearanceId = previousAppearance,
                        CurrentAppearanceId = currentAppearance
                    });
                }
            }
        }

        private EquipmentSlotState GetState(EquipSlot slot)
        {
            if (_equippedBySlot.TryGetValue(slot, out EquipmentSlotState state))
                return state;

            return EmptyState(slot);
        }

        private static bool AreSame(EquipmentSlotState left, EquipmentSlotState right)
        {
            return left.Item.ItemId == right.Item.ItemId
                && string.Equals(left.Item.Type, right.Item.Type, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Item.Subtype, right.Item.Subtype, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Item.Family, right.Item.Family, StringComparison.OrdinalIgnoreCase)
                && left.Item.Category == right.Item.Category
                && left.Item.IsTwoHanded == right.Item.IsTwoHanded
                && left.Item.VisualId == right.Item.VisualId;
        }

        private static EquipmentSlotState EmptyState(EquipSlot slot)
        {
            return new EquipmentSlotState
            {
                Slot = slot,
                Item = default
            };
        }

        private static Dictionary<EquipSlot, EquipmentSlotState> BuildSnapshotMap(EquipmentSnapshot snapshot)
        {
            var map = new Dictionary<EquipSlot, EquipmentSlotState>();
            if (snapshot == null || snapshot.slots == null)
                return map;

            for (int i = 0; i < snapshot.slots.Count; i++)
            {
                EquipmentSlotSnapshot entry = snapshot.slots[i];
                if (!Enum.TryParse(entry.slot, true, out EquipSlot slot))
                    continue;

                var state = new EquipmentSlotState
                {
                    Slot = slot,
                    Item = EquippedItemDescriptor.FromSnapshot(entry)
                };

                if (!state.IsEmpty)
                    map[slot] = state;
            }

            return map;
        }

        private static Dictionary<EquipSlot, SlotRule> BuildSlotRules()
        {
            var rules = new Dictionary<EquipSlot, SlotRule>
            {
                [EquipSlot.Helm] = Rule(new[] { EquipItemCategory.Armor }, new[] { "armor" }, new[] { "helms" }),
                [EquipSlot.Armor] = Rule(new[] { EquipItemCategory.Armor }, new[] { "armor" }, new[] { "armors" }),
                [EquipSlot.Pants] = Rule(new[] { EquipItemCategory.Armor }, new[] { "armor" }, new[] { "pants" }),
                [EquipSlot.Gloves] = Rule(new[] { EquipItemCategory.Armor }, new[] { "armor" }, new[] { "gloves" }),
                [EquipSlot.Boots] = Rule(new[] { EquipItemCategory.Armor }, new[] { "armor" }, new[] { "boots" }),
                [EquipSlot.WeaponMain] = Rule(
                    new[] { EquipItemCategory.Weapon },
                    new[] { "weapon" },
                    new[] { "swords", "axes", "maces", "spears", "bows", "crossbows", "staffs", "scepters" }),
                [EquipSlot.WeaponOffhand] = Rule(
                    new[] { EquipItemCategory.Shield, EquipItemCategory.Weapon },
                    new[] { "shield", "weapon" },
                    new[] { "shields", "swords", "axes", "maces" }),
                [EquipSlot.RingLeft] = Rule(new[] { EquipItemCategory.Accessory }, new[] { "accessory" }, new[] { "rings" }),
                [EquipSlot.RingRight] = Rule(new[] { EquipItemCategory.Accessory }, new[] { "accessory" }, new[] { "rings" }),
                [EquipSlot.Pendant] = Rule(new[] { EquipItemCategory.Accessory }, new[] { "accessory" }, new[] { "pendants" }),
                [EquipSlot.Wings] = Rule(new[] { EquipItemCategory.Wings }, new[] { "special" }, new[] { "wings" }),
                [EquipSlot.Pet] = Rule(new[] { EquipItemCategory.Pet }, new[] { "special" }, new[] { "pets" })
            };

            return rules;
        }

        private static SlotRule Rule(params EquipItemCategory[] categories)
        {
            return Rule(categories, Array.Empty<string>(), Array.Empty<string>());
        }

        private static SlotRule Rule(EquipItemCategory category, string[] type = null, params string[] family)
        {
            return Rule(new[] { category }, type, family);
        }

        private static SlotRule Rule(EquipItemCategory categoryA, EquipItemCategory categoryB, string[] type = null, params string[] family)
        {
            return Rule(new[] { categoryA, categoryB }, type, family);
        }

        private static SlotRule Rule(EquipItemCategory[] categories, string[] type, string[] family)
        {
            var rule = new SlotRule();

            if (categories != null)
            {
                for (int i = 0; i < categories.Length; i++)
                    rule.AllowedCategories.Add(categories[i]);
            }

            if (type != null)
            {
                for (int i = 0; i < type.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(type[i]))
                        rule.AllowedTypes.Add(type[i]);
                }
            }

            if (family != null)
            {
                for (int i = 0; i < family.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(family[i]))
                        rule.AllowedFamilies.Add(family[i]);
                }
            }

            return rule;
        }
    }
}
