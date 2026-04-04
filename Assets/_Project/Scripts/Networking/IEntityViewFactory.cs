using MuLike.Gameplay.Entities;

namespace MuLike.Networking
{
    /// <summary>
    /// Factory abstraction for spawning and destroying entity views from snapshot data.
    /// </summary>
    public interface IEntityViewFactory
    {
        EntityView CreateView(SnapshotApplier.EntitySnapshot snapshot);
        void DestroyView(EntityView view);
    }
}
