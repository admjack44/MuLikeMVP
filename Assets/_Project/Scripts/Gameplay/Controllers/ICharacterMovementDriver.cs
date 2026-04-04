using UnityEngine;

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// Abstraction for movement steering so CharacterMotor can swap pathing implementation later.
    /// </summary>
    public interface ICharacterMovementDriver
    {
        bool HasDestination { get; }
        Vector3 Destination { get; }

        void SetDestination(Vector3 destination);
        void ClearDestination();

        bool TryGetSteering(
            Vector3 currentPosition,
            float stoppingDistance,
            out Vector3 direction,
            out float remainingDistance);
    }
}
