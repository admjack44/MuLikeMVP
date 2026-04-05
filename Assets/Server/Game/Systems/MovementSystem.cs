using MuLike.Server.Game.Entities;
using UnityEngine;

namespace MuLike.Server.Game.Systems
{
    public sealed class MovementSystem
    {
        // ARPG mobile constraints: Typical max movement ~8 units/sec, max 1.5 units per tick @ 20 ticks/sec
        private const float MaxMovementSpeedPerSecond = 8f;
        private const float MaxDistancePerTick = 1.5f;
        private const float PositionCorrectionThreshold = 0.5f;

        public struct MovementValidation
        {
            public bool IsValid { get; set; }
            public float CorrectedX { get; set; }
            public float CorrectedY { get; set; }
            public float CorrectedZ { get; set; }
            public string Reason { get; set; }
        }

        // Server-authoritative move validation. Rejects suspicious movement patterns.
        public MovementValidation ValidateMovement(
            Entity entity,
            float targetX,
            float targetY,
            float targetZ,
            float deltaTime)
        {
            var result = new MovementValidation { IsValid = true };

            float dx = targetX - entity.X;
            float dy = targetY - entity.Y;
            float dz = targetZ - entity.Z;
            float distance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);

            // Check maximum distance per tick (speed cheat detection)
            if (distance > MaxDistancePerTick)
            {
                result.IsValid = false;
                result.Reason = "Distance exceeds max per tick";
                return result;
            }

            // Check velocity constraint (accounts for network jitter)
            float maxAllowedDistance = MaxMovementSpeedPerSecond * deltaTime * 1.2f;
            if (distance > maxAllowedDistance)
            {
                result.IsValid = false;
                result.Reason = "Speed exceeds maximum";
                return result;
            }

            // Detect and correct position deviations (simple anti-teleport)
            if (distance > PositionCorrectionThreshold)
            {
                // Position seems too far. Clamp to max distance and correct.
                float clampedDistance = Mathf.Min(distance, MaxDistancePerTick);
                if (distance > 0)
                {
                    float ratio = clampedDistance / distance;
                    result.CorrectedX = entity.X + dx * ratio;
                    result.CorrectedY = entity.Y + dy * ratio;
                    result.CorrectedZ = entity.Z + dz * ratio;
                }
                else
                {
                    result.CorrectedX = entity.X;
                    result.CorrectedY = entity.Y;
                    result.CorrectedZ = entity.Z;
                }
            }
            else
            {
                result.CorrectedX = targetX;
                result.CorrectedY = targetY;
                result.CorrectedZ = targetZ;
            }

            return result;
        }

        // Apply validated movement to entity.
        public void ApplyMovement(Entity entity, float x, float y, float z)
        {
            entity.SetPosition(x, y, z);
        }

        // Legacy simple move for non-validated cases.
        public void Move(Entity entity, float x, float y, float z)
        {
            entity.SetPosition(x, y, z);
        }
    }
}
