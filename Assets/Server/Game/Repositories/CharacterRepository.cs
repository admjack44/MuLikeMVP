using System.Collections.Generic;
using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.Repositories
{
    public sealed class CharacterRepository
    {
        private readonly Dictionary<int, PlayerEntity> _cache = new();

        public void Save(PlayerEntity entity)
        {
            _cache[entity.Id] = entity;
        }

        public bool TryGet(int characterId, out PlayerEntity entity)
        {
            return _cache.TryGetValue(characterId, out entity);
        }

        public bool Remove(int characterId)
        {
            return _cache.Remove(characterId);
        }
    }
}
