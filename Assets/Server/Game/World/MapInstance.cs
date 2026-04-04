using System.Collections.Generic;
using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.World
{
    public sealed class MapInstance
    {
        private readonly Dictionary<int, Entity> _entities = new();

        public int MapId { get; }
        public string Name { get; }

        public MapInstance(int mapId, string name)
        {
            MapId = mapId;
            Name = name;
        }

        public void AddEntity(Entity entity)
        {
            _entities[entity.Id] = entity;
        }

        public bool RemoveEntity(int entityId)
        {
            return _entities.Remove(entityId);
        }

        public IReadOnlyCollection<Entity> GetEntities()
        {
            return _entities.Values;
        }

        public bool TryGetEntity(int entityId, out Entity entity)
        {
            return _entities.TryGetValue(entityId, out entity);
        }
    }
}
