using System;

namespace MuLike.Data.DTO
{
    [Serializable]
    public class ItemInstanceDto
    {
        public string instanceId;
        public int itemId;
        public int quantity;
        public int maxStack;
        public int durabilityCurrent;
        public int durabilityMax;
        public int enhancementLevel;
        public string ownerCharacterId;
    }

    [Serializable]
    public class InventorySlotDto
    {
        public int slotIndex;
        public ItemInstanceDto item;
    }

    [Serializable]
    public class EquippedItemDto
    {
        public string slot;
        public ItemInstanceDto item;
    }

    [Serializable]
    public class InventoryEquipmentSnapshotDto
    {
        public string characterId;
        public InventorySlotDto[] inventorySlots;
        public EquippedItemDto[] equipped;
        public QuickSlotDto[] quickSlots;
        public int powerScore;
        public long serverRevision;
    }

    [Serializable]
    public class QuickSlotDto
    {
        public string kind;
        public int slotIndex;
    }

    [Serializable]
    public class InventorySlotDeltaDto
    {
        public bool remove;
        public InventorySlotDto slot;
    }

    [Serializable]
    public class EquippedItemDeltaDto
    {
        public bool remove;
        public string slot;
        public EquippedItemDto equipped;
    }

    [Serializable]
    public class QuickSlotDeltaDto
    {
        public bool remove;
        public string kind;
        public int slotIndex;
    }

    [Serializable]
    public class InventoryEquipmentDeltaDto
    {
        public string characterId;
        public InventorySlotDeltaDto[] inventory;
        public EquippedItemDeltaDto[] equipment;
        public QuickSlotDeltaDto[] quickSlots;
        public long serverRevision;
    }

    [Serializable]
    public class WorldDropDto
    {
        public int dropEntityId;
        public string dropInstanceId;
        public int itemId;
        public int quantity;
        public float x;
        public float y;
        public float z;
        public bool reserved;
    }

    [Serializable]
    public class PickupDropRequestDto
    {
        public int dropEntityId;
        public string characterId;
    }

    [Serializable]
    public class EquipItemRequestDto
    {
        public int slotIndex;
        public string equipSlot;
        public string characterId;
    }

    [Serializable]
    public class UnequipItemRequestDto
    {
        public string equipSlot;
        public int targetInventorySlotIndex;
        public string characterId;
    }

    [Serializable]
    public class DropItemRequestDto
    {
        public int slotIndex;
        public int quantity;
        public string characterId;
    }

    [Serializable]
    public class InventoryOperationResultDto
    {
        public bool success;
        public string message;
        public long serverRevision;
    }

    [Serializable]
    public class InventoryOperationEnvelopeDto
    {
        public string op;
        public string requestId;
        public string accessToken;
        public PickupDropRequestDto pickupDrop;
        public EquipItemRequestDto equip;
        public UnequipItemRequestDto unequip;
        public DropItemRequestDto drop;
    }
}
