using System;
using System.Collections.Generic;
using MuLike.Data.Catalogs;
using UnityEngine;

namespace MuLike.Inventory
{
    /// <summary>
    /// MU Immortal style inventory runtime.
    ///
    /// Features:
    /// - 8x4 visible grid (32 slots) with virtual vertical scroll pages.
    /// - Variable item sizes (1x1, 1x2, 2x2).
    /// - Touch long-press drag and drop semantics.
    /// - Auto-sort by type and rarity.
    /// - Bank (120 slots), account-shared vault and simple mail attachments.
    /// - Refinery (+1..+9 bless/soul, +10..+15 chaos machine, guardian protection).
    /// </summary>
    public sealed class InventoryManager : MonoBehaviour
    {
        public const int VisibleColumns = 8;
        public const int VisibleRows = 4;
        public const int VisibleSlots = VisibleColumns * VisibleRows;

        public enum ItemSize
        {
            OneByOne,
            OneByTwo,
            TwoByTwo
        }

        public enum InventoryRarity
        {
            Common,
            Magic,
            Rare,
            Epic,
            Legendary
        }

        public enum StorageType
        {
            Inventory,
            Bank,
            Vault,
            Mail
        }

        public enum RefineryStone
        {
            Bless,
            Soul,
            Chaos,
            Guardian
        }

        [Serializable]
        public struct ItemFootprint
        {
            public int width;
            public int height;

            public static ItemFootprint FromSize(ItemSize size)
            {
                return size switch
                {
                    ItemSize.OneByTwo => new ItemFootprint { width = 1, height = 2 },
                    ItemSize.TwoByTwo => new ItemFootprint { width = 2, height = 2 },
                    _ => new ItemFootprint { width = 1, height = 1 }
                };
            }
        }

        [Serializable]
        public struct ItemStats
        {
            public int damage;
            public int defense;
            public int hp;
            public int mana;
            public int agility;
            public int strength;
        }

        [Serializable]
        public sealed class InventoryItem
        {
            public string instanceId;
            public int itemId;
            public string displayName;
            public ItemCategory category;
            public InventoryRarity rarity;
            public ItemSize size;
            public int quantity;
            public int enhancementLevel;
            public int visualId;
            public ItemStats stats;

            public ItemFootprint Footprint => ItemFootprint.FromSize(size);
        }

        [Serializable]
        public struct GridPosition
        {
            public int x;
            public int y;

            public GridPosition(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [Serializable]
        public sealed class GridEntry
        {
            public InventoryItem item;
            public GridPosition anchor;
        }

        [Serializable]
        public struct MailPackage
        {
            public string sender;
            public string receiver;
            public string subject;
            public InventoryItem attachment;
            public DateTime sentAtUtc;
        }

        [Serializable]
        public struct LongPressConfig
        {
            public float seconds;
            public float moveThresholdPixels;
        }

        [Header("Grid")]
        [SerializeField, Min(8)] private int _totalRows = 12;
        [SerializeField] private int _scrollRowOffset;
        [SerializeField] private bool _allowVerticalScroll = true;

        [Header("Touch Drag")]
        [SerializeField] private LongPressConfig _longPress = new LongPressConfig
        {
            seconds = 0.25f,
            moveThresholdPixels = 18f
        };

        [Header("Storage")]
        [SerializeField, Min(120)] private int _bankSlots = 120;
        [SerializeField, Min(120)] private int _vaultSlots = 120;

        [Header("Refinery")]
        [SerializeField] private float _blessBaseSuccess = 0.95f;
        [SerializeField] private float _soulBaseSuccess = 0.70f;
        [SerializeField] private float _chaosBaseSuccess = 0.45f;

        private readonly List<GridEntry> _inventoryEntries = new();
        private readonly List<InventoryItem> _bankItems = new();
        private readonly List<InventoryItem> _vaultItems = new();
        private readonly List<MailPackage> _mailInbox = new();

        private readonly Dictionary<int, PointerDragState> _pointerDragById = new();
        private InventoryItem _grabbedItem;

        public event Action OnInventoryChanged;
        public event Action<InventoryItem> OnItemGrabbed;
        public event Action<InventoryItem, bool> OnRefineryResult;
        public event Action<StorageType, int> OnStorageCountChanged;

        private struct PointerDragState
        {
            public int pointerId;
            public Vector2 downScreenPos;
            public float downTime;
            public GridPosition downCell;
            public bool longPressTriggered;
        }

        public int TotalColumns => VisibleColumns;
        public int TotalRows => Mathf.Max(VisibleRows, _totalRows);
        public int ScrollRowOffset => _scrollRowOffset;
        public IReadOnlyList<GridEntry> InventoryEntries => _inventoryEntries;
        public IReadOnlyList<InventoryItem> BankItems => _bankItems;
        public IReadOnlyList<InventoryItem> VaultItems => _vaultItems;
        public IReadOnlyList<MailPackage> MailInbox => _mailInbox;

        public Color GetRarityColor(InventoryRarity rarity)
        {
            return rarity switch
            {
                InventoryRarity.Magic => new Color(0.20f, 0.55f, 1f),
                InventoryRarity.Rare => new Color(1f, 0.82f, 0.23f),
                InventoryRarity.Epic => new Color(0.68f, 0.33f, 0.92f),
                InventoryRarity.Legendary => new Color(1f, 0.55f, 0.12f),
                _ => Color.white
            };
        }

        public bool TryAddItem(InventoryItem item)
        {
            if (item == null)
                return false;

            if (TryFindFreeAnchor(item.Footprint, out GridPosition pos))
            {
                _inventoryEntries.Add(new GridEntry { item = item, anchor = pos });
                EmitInventoryChanged();
                return true;
            }

            return false;
        }

        public bool TryRemoveItem(string instanceId, out InventoryItem removed)
        {
            for (int i = 0; i < _inventoryEntries.Count; i++)
            {
                GridEntry e = _inventoryEntries[i];
                if (e.item == null || e.item.instanceId != instanceId)
                    continue;

                removed = e.item;
                _inventoryEntries.RemoveAt(i);
                EmitInventoryChanged();
                return true;
            }

            removed = null;
            return false;
        }

        public bool TryMoveItem(string instanceId, GridPosition newAnchor)
        {
            int idx = FindEntryIndex(instanceId);
            if (idx < 0)
                return false;

            GridEntry entry = _inventoryEntries[idx];
            if (!CanPlace(entry.item, newAnchor, ignoreInstanceId: instanceId))
                return false;

            entry.anchor = newAnchor;
            _inventoryEntries[idx] = entry;
            EmitInventoryChanged();
            return true;
        }

        public bool TryGetEntryAtCell(GridPosition cell, out GridEntry entry)
        {
            for (int i = 0; i < _inventoryEntries.Count; i++)
            {
                GridEntry e = _inventoryEntries[i];
                if (e.item == null)
                    continue;

                if (OccupiesCell(e, cell))
                {
                    entry = e;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public void SetScrollOffsetRows(int offset)
        {
            if (!_allowVerticalScroll)
            {
                _scrollRowOffset = 0;
                return;
            }

            int maxOffset = Mathf.Max(0, TotalRows - VisibleRows);
            _scrollRowOffset = Mathf.Clamp(offset, 0, maxOffset);
            EmitInventoryChanged();
        }

        public IReadOnlyList<GridEntry> GetVisibleEntries()
        {
            var visible = new List<GridEntry>(VisibleSlots);
            int minY = _scrollRowOffset;
            int maxY = _scrollRowOffset + VisibleRows - 1;

            for (int i = 0; i < _inventoryEntries.Count; i++)
            {
                GridEntry e = _inventoryEntries[i];
                if (e.item == null)
                    continue;

                int top = e.anchor.y;
                int bottom = e.anchor.y + e.item.Footprint.height - 1;
                if (bottom < minY || top > maxY)
                    continue;

                visible.Add(e);
            }

            return visible;
        }

        /// <summary>
        /// Sorts by category, rarity (desc), then item id and repacks in reading order.
        /// </summary>
        public void AutoOrganize()
        {
            _inventoryEntries.Sort((a, b) =>
            {
                if (a.item == null || b.item == null)
                    return 0;

                int c0 = a.item.category.CompareTo(b.item.category);
                if (c0 != 0) return c0;

                int c1 = b.item.rarity.CompareTo(a.item.rarity);
                if (c1 != 0) return c1;

                int c2 = a.item.itemId.CompareTo(b.item.itemId);
                if (c2 != 0) return c2;

                return string.CompareOrdinal(a.item.instanceId, b.item.instanceId);
            });

            var rePacked = new List<GridEntry>(_inventoryEntries.Count);
            for (int i = 0; i < _inventoryEntries.Count; i++)
            {
                InventoryItem item = _inventoryEntries[i].item;
                if (item == null)
                    continue;

                if (!TryFindFreeAnchor(item.Footprint, rePacked, out GridPosition pos))
                    continue;

                rePacked.Add(new GridEntry { item = item, anchor = pos });
            }

            _inventoryEntries.Clear();
            _inventoryEntries.AddRange(rePacked);
            EmitInventoryChanged();
        }

        // Touch drag via long-press
        public void BeginPointerDown(int pointerId, Vector2 screenPos, GridPosition cell)
        {
            _pointerDragById[pointerId] = new PointerDragState
            {
                pointerId = pointerId,
                downScreenPos = screenPos,
                downTime = Time.unscaledTime,
                downCell = cell,
                longPressTriggered = false
            };
        }

        public void UpdatePointer(int pointerId, Vector2 screenPos)
        {
            if (!_pointerDragById.TryGetValue(pointerId, out PointerDragState state))
                return;

            if (state.longPressTriggered)
                return;

            float dt = Time.unscaledTime - state.downTime;
            float moved = Vector2.Distance(screenPos, state.downScreenPos);
            if (dt < _longPress.seconds || moved > _longPress.moveThresholdPixels)
                return;

            if (TryGetEntryAtCell(state.downCell, out GridEntry entry) && entry != null && entry.item != null)
            {
                _grabbedItem = entry.item;
                state.longPressTriggered = true;
                _pointerDragById[pointerId] = state;
                OnItemGrabbed?.Invoke(_grabbedItem);
            }
        }

        public bool EndPointerUp(int pointerId, GridPosition releaseCell)
        {
            if (!_pointerDragById.TryGetValue(pointerId, out PointerDragState state))
                return false;

            _pointerDragById.Remove(pointerId);
            if (!state.longPressTriggered || _grabbedItem == null)
                return false;

            bool moved = TryMoveItem(_grabbedItem.instanceId, releaseCell);
            _grabbedItem = null;
            return moved;
        }

        // Storage
        public bool TryDepositToBank(string instanceId)
        {
            if (_bankItems.Count >= _bankSlots)
                return false;

            if (!TryRemoveItem(instanceId, out InventoryItem item))
                return false;

            _bankItems.Add(item);
            OnStorageCountChanged?.Invoke(StorageType.Bank, _bankItems.Count);
            return true;
        }

        public bool TryWithdrawFromBank(string instanceId)
        {
            int index = _bankItems.FindIndex(x => x != null && x.instanceId == instanceId);
            if (index < 0)
                return false;

            InventoryItem item = _bankItems[index];
            if (!TryAddItem(item))
                return false;

            _bankItems.RemoveAt(index);
            OnStorageCountChanged?.Invoke(StorageType.Bank, _bankItems.Count);
            return true;
        }

        public bool TryDepositToVault(string instanceId)
        {
            if (_vaultItems.Count >= _vaultSlots)
                return false;

            if (!TryRemoveItem(instanceId, out InventoryItem item))
                return false;

            _vaultItems.Add(item);
            OnStorageCountChanged?.Invoke(StorageType.Vault, _vaultItems.Count);
            return true;
        }

        public bool TryWithdrawFromVault(string instanceId)
        {
            int index = _vaultItems.FindIndex(x => x != null && x.instanceId == instanceId);
            if (index < 0)
                return false;

            InventoryItem item = _vaultItems[index];
            if (!TryAddItem(item))
                return false;

            _vaultItems.RemoveAt(index);
            OnStorageCountChanged?.Invoke(StorageType.Vault, _vaultItems.Count);
            return true;
        }

        public bool TrySendItemByMail(string instanceId, string receiver, string subject)
        {
            if (!TryRemoveItem(instanceId, out InventoryItem item))
                return false;

            _mailInbox.Add(new MailPackage
            {
                sender = "local-player",
                receiver = receiver,
                subject = subject,
                attachment = item,
                sentAtUtc = DateTime.UtcNow
            });

            OnStorageCountChanged?.Invoke(StorageType.Mail, _mailInbox.Count);
            return true;
        }

        public bool TryClaimMailItem(int mailIndex)
        {
            if (mailIndex < 0 || mailIndex >= _mailInbox.Count)
                return false;

            MailPackage pack = _mailInbox[mailIndex];
            if (pack.attachment == null)
                return false;

            if (!TryAddItem(pack.attachment))
                return false;

            _mailInbox.RemoveAt(mailIndex);
            OnStorageCountChanged?.Invoke(StorageType.Mail, _mailInbox.Count);
            return true;
        }

        // Refinery
        public bool TryRefine(string instanceId, RefineryStone primaryStone, bool useGuardian)
        {
            int idx = FindEntryIndex(instanceId);
            if (idx < 0)
                return false;

            GridEntry entry = _inventoryEntries[idx];
            InventoryItem item = entry.item;
            if (item == null)
                return false;

            int before = item.enhancementLevel;
            bool success = RollRefinery(item.enhancementLevel, primaryStone);

            if (success)
            {
                item.enhancementLevel = Mathf.Clamp(item.enhancementLevel + 1, 0, 15);
                _inventoryEntries[idx] = entry;
                OnRefineryResult?.Invoke(item, true);
                EmitInventoryChanged();
                return true;
            }

            // Failure behavior
            if (item.enhancementLevel <= 9)
            {
                if (primaryStone == RefineryStone.Soul)
                    item.enhancementLevel = Mathf.Max(0, item.enhancementLevel - 1);
            }
            else
            {
                // Chaos machine +10..+15
                if (!useGuardian)
                {
                    // Destruction semantics: remove item when no guardian protection
                    _inventoryEntries.RemoveAt(idx);
                    OnRefineryResult?.Invoke(item, false);
                    EmitInventoryChanged();
                    return false;
                }

                // Guardian protects from destruction but can reduce level
                item.enhancementLevel = Mathf.Max(9, item.enhancementLevel - 1);
            }

            _inventoryEntries[idx] = entry;
            OnRefineryResult?.Invoke(item, false);
            EmitInventoryChanged();
            return false;
        }

        private bool RollRefinery(int currentLevel, RefineryStone stone)
        {
            float baseChance = stone switch
            {
                RefineryStone.Bless => _blessBaseSuccess,
                RefineryStone.Soul => _soulBaseSuccess,
                RefineryStone.Chaos => _chaosBaseSuccess,
                _ => _chaosBaseSuccess
            };

            float penalty = Mathf.Clamp01(currentLevel / 20f);
            float chance = Mathf.Clamp01(baseChance - penalty * 0.35f);
            return UnityEngine.Random.value <= chance;
        }

        private int FindEntryIndex(string instanceId)
        {
            for (int i = 0; i < _inventoryEntries.Count; i++)
            {
                GridEntry e = _inventoryEntries[i];
                if (e.item == null || e.item.instanceId != instanceId)
                    continue;

                return i;
            }

            return -1;
        }

        private bool TryFindFreeAnchor(ItemFootprint fp, out GridPosition pos)
        {
            return TryFindFreeAnchor(fp, _inventoryEntries, out pos);
        }

        private bool TryFindFreeAnchor(ItemFootprint fp, List<GridEntry> entries, out GridPosition pos)
        {
            for (int y = 0; y <= TotalRows - fp.height; y++)
            {
                for (int x = 0; x <= TotalColumns - fp.width; x++)
                {
                    GridPosition candidate = new GridPosition(x, y);
                    if (CanPlace(fp, candidate, entries))
                    {
                        pos = candidate;
                        return true;
                    }
                }
            }

            pos = default;
            return false;
        }

        private bool CanPlace(InventoryItem item, GridPosition anchor, string ignoreInstanceId)
        {
            if (item == null)
                return false;

            if (!CanPlace(item.Footprint, anchor, _inventoryEntries, ignoreInstanceId))
                return false;

            return true;
        }

        private bool CanPlace(ItemFootprint fp, GridPosition anchor, List<GridEntry> entries, string ignoreInstanceId = null)
        {
            if (anchor.x < 0 || anchor.y < 0)
                return false;

            if (anchor.x + fp.width > TotalColumns)
                return false;

            if (anchor.y + fp.height > TotalRows)
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                GridEntry other = entries[i];
                if (other.item == null)
                    continue;

                if (!string.IsNullOrEmpty(ignoreInstanceId) && other.item.instanceId == ignoreInstanceId)
                    continue;

                if (RectOverlap(anchor, fp, other.anchor, other.item.Footprint))
                    return false;
            }

            return true;
        }

        private static bool RectOverlap(GridPosition a, ItemFootprint af, GridPosition b, ItemFootprint bf)
        {
            int aLeft = a.x;
            int aRight = a.x + af.width - 1;
            int aTop = a.y;
            int aBottom = a.y + af.height - 1;

            int bLeft = b.x;
            int bRight = b.x + bf.width - 1;
            int bTop = b.y;
            int bBottom = b.y + bf.height - 1;

            bool xOverlap = aLeft <= bRight && bLeft <= aRight;
            bool yOverlap = aTop <= bBottom && bTop <= aBottom;
            return xOverlap && yOverlap;
        }

        private static bool OccupiesCell(GridEntry entry, GridPosition cell)
        {
            ItemFootprint fp = entry.item.Footprint;
            return cell.x >= entry.anchor.x
                && cell.y >= entry.anchor.y
                && cell.x < entry.anchor.x + fp.width
                && cell.y < entry.anchor.y + fp.height;
        }

        private void EmitInventoryChanged()
        {
            OnInventoryChanged?.Invoke();
            OnStorageCountChanged?.Invoke(StorageType.Inventory, _inventoryEntries.Count);
        }
    }
}
