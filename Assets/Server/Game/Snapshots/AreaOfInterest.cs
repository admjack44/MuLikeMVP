using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.Snapshots
{
    public interface IAreaOfInterest
    {
        bool IsInView(int observerEntityId, Entity targetEntity);
    }

    public sealed class DistanceBasedAOI : IAreaOfInterest
    {
        private readonly float _viewDistance;

        public DistanceBasedAOI(float viewDistance = 50f)
        {
            _viewDistance = viewDistance > 0 ? viewDistance : 50f;
        }

        public bool IsInView(int observerEntityId, Entity targetEntity)
        {
            if (targetEntity == null)
                return false;

            if (targetEntity.Id == observerEntityId)
                return false;

            float distX = targetEntity.X;
            float distZ = targetEntity.Z;

            float distSquared = distX * distX + distZ * distZ;
            float rangeSquared = _viewDistance * _viewDistance;

            return distSquared <= rangeSquared;
        }
    }
}
