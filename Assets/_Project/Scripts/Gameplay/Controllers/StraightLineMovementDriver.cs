using UnityEngine;

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// Minimal movement driver that steers in a straight line to destination.
    /// Replace with pathfinding driver later if needed.
    /// </summary>
    public sealed class StraightLineMovementDriver : ICharacterMovementDriver
    {
        public bool HasDestination { get; private set; }
        public Vector3 Destination { get; private set; }

        public void SetDestination(Vector3 destination)
        {
            Destination = destination;
            HasDestination = true;
        }

        public void ClearDestination()
        {
            HasDestination = false;
            Destination = Vector3.zero;
        }

        public bool TryGetSteering(
            Vector3 currentPosition,
            float stoppingDistance,
            out Vector3 direction,
            out float remainingDistance)
        {
            direction = Vector3.zero;
            remainingDistance = 0f;

            if (!HasDestination)
                return false;

            Vector3 delta = Destination - currentPosition;
            delta.y = 0f;
            remainingDistance = delta.magnitude;

            if (remainingDistance <= stoppingDistance)
            {
                ClearDestination();
                return false;
            }

            direction = delta / remainingDistance;
            return true;
        }
    }
}
