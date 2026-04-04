using System.Collections.Generic;

namespace MuLike.Server.Game.World
{
    public sealed class WorldManager
    {
        private readonly Dictionary<int, MapInstance> _maps = new();

        public void RegisterMap(MapInstance map)
        {
            _maps[map.MapId] = map;
        }

        public bool TryGetMap(int mapId, out MapInstance map)
        {
            return _maps.TryGetValue(mapId, out map);
        }
    }
}
