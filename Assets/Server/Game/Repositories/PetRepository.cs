using System.Collections.Generic;

namespace MuLike.Server.Game.Repositories
{
    public sealed class PetRepository
    {
        private readonly Dictionary<int, int> _activePetByCharacter = new();

        public void SetActivePet(int characterId, int petId)
        {
            _activePetByCharacter[characterId] = petId;
        }

        public bool TryGetActivePet(int characterId, out int petId)
        {
            return _activePetByCharacter.TryGetValue(characterId, out petId);
        }
    }
}
