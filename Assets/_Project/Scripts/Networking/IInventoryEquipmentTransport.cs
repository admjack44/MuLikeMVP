using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Data.DTO;

namespace MuLike.Networking
{
    /// <summary>
    /// Suggested authoritative contract for inventory/equipment/drop operations.
    /// Server owns truth and pushes snapshots/revisions.
    /// </summary>
    public interface IInventoryEquipmentTransport
    {
        event Action<InventoryEquipmentSnapshotDto> SnapshotReceived;
        event Action<InventoryEquipmentDeltaDto> DeltaReceived;
        event Action<IReadOnlyList<WorldDropDto>> WorldDropsReceived;

        Task<InventoryEquipmentSnapshotDto> RequestFullSnapshotAsync(string characterId, CancellationToken ct);
        Task<InventoryOperationResultDto> PickupDropAsync(PickupDropRequestDto request, CancellationToken ct);
        Task<InventoryOperationResultDto> EquipAsync(EquipItemRequestDto request, CancellationToken ct);
        Task<InventoryOperationResultDto> UnequipAsync(UnequipItemRequestDto request, CancellationToken ct);
        Task<InventoryOperationResultDto> DropItemAsync(DropItemRequestDto request, CancellationToken ct);
    }
}
