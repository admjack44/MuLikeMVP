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
        private readonly System.Func<int, Entity> _observerResolver;

        public DistanceBasedAOI(float viewDistance = 50f, System.Func<int, Entity> observerResolver = null)
        {
            _viewDistance = viewDistance > 0 ? viewDistance : 50f;
            _observerResolver = observerResolver;
        }

        public bool IsInView(int observerEntityId, Entity targetEntity)
        {
            if (targetEntity == null)
                return false;

            if (targetEntity.Id == observerEntityId)
                return false;

            Entity observer = _observerResolver?.Invoke(observerEntityId);

            float originX = observer != null ? observer.X : 0f;
            float originZ = observer != null ? observer.Z : 0f;
            float distX = targetEntity.X - originX;
            float distZ = targetEntity.Z - originZ;

            float distSquared = distX * distX + distZ * distZ;
            float rangeSquared = _viewDistance * _viewDistance;

            return distSquared <= rangeSquared;
        }
    }
}
