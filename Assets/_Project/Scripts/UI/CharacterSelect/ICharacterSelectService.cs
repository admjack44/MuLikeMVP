using System.Collections.Generic;
using System.Threading.Tasks;
using MuLike.Data.DTO;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Contract for character list/create/delete/enter operations.
    /// Implementations can be local mock or network-backed.
    /// </summary>
    public interface ICharacterSelectService
    {
        Task<IReadOnlyList<CharacterSummaryDto>> GetCharactersAsync();
        Task<CharacterSelectOperationResultDto> CreateCharacterAsync(CreateCharacterRequestDto request);
        Task<CharacterSelectOperationResultDto> DeleteCharacterAsync(int characterId);
        Task<EnterWorldResultDto> EnterWorldAsync(int characterId);
    }
}
