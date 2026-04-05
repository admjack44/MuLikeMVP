using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MuLike.Data.DTO;
using UnityEngine;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Local in-memory character select service for MVP iteration before server-backed list is available.
    /// </summary>
    public sealed class MockCharacterSelectService : ICharacterSelectService
    {
        private const int MaxCharacters = 5;

        private readonly List<CharacterSummaryDto> _characters = new();
        private int _nextCharacterId = 1001;

        public MockCharacterSelectService()
        {
            SeedIfEmpty();
        }

        public Task<IReadOnlyList<CharacterSummaryDto>> GetCharactersAsync()
        {
            SeedIfEmpty();
            return Task.FromResult((IReadOnlyList<CharacterSummaryDto>)_characters.ToArray());
        }

        public Task<CharacterSelectOperationResultDto> CreateCharacterAsync(CreateCharacterRequestDto request)
        {
            if (request == null)
                return Task.FromResult(Fail("Create request is null."));

            string name = (request.name ?? string.Empty).Trim();
            string classId = (request.classId ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
                return Task.FromResult(Fail("Character name is required."));

            if (string.IsNullOrWhiteSpace(classId))
                return Task.FromResult(Fail("Character class is required."));

            if (_characters.Count >= MaxCharacters)
                return Task.FromResult(Fail($"Character limit reached ({MaxCharacters})."));

            if (ExistsByName(name))
                return Task.FromResult(Fail("Character name already exists."));

            var created = new CharacterSummaryDto
            {
                characterId = _nextCharacterId++,
                name = name,
                classId = classId,
                level = 1,
                powerScore = 120,
                mapId = 1,
                mapName = "Lorencia",
                isLastPlayed = false
            };

            _characters.Add(created);
            Debug.Log($"[MockCharacterSelectService] Created character {created.name} ({created.characterId}).");

            return Task.FromResult(new CharacterSelectOperationResultDto
            {
                success = true,
                message = "Character created.",
                characterId = created.characterId
            });
        }

        public Task<CharacterSelectOperationResultDto> DeleteCharacterAsync(int characterId)
        {
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].characterId != characterId)
                    continue;

                string deletedName = _characters[i].name;
                _characters.RemoveAt(i);
                Debug.Log($"[MockCharacterSelectService] Deleted character {deletedName} ({characterId}).");

                return Task.FromResult(new CharacterSelectOperationResultDto
                {
                    success = true,
                    message = "Character deleted.",
                    characterId = characterId
                });
            }

            return Task.FromResult(Fail("Character not found."));
        }

        public Task<EnterWorldResultDto> EnterWorldAsync(int characterId)
        {
            CharacterSummaryDto character = FindById(characterId);
            if (character == null)
            {
                return Task.FromResult(new EnterWorldResultDto
                {
                    success = false,
                    message = "Character not found.",
                    characterId = characterId,
                    sceneName = string.Empty,
                    mapId = 0
                });
            }

            return Task.FromResult(new EnterWorldResultDto
            {
                success = true,
                message = "Entering world.",
                characterId = character.characterId,
                sceneName = character.mapId == 1 ? "Town_01" : "World_Dev",
                mapId = character.mapId
            });
        }

        private void SeedIfEmpty()
        {
            if (_characters.Count > 0) return;

            _characters.Add(new CharacterSummaryDto
            {
                characterId = _nextCharacterId++,
                name = "KnightOne",
                classId = "DarkKnight",
                level = 35,
                powerScore = 12850,
                mapId = 1,
                mapName = "Lorencia",
                isLastPlayed = true
            });

            _characters.Add(new CharacterSummaryDto
            {
                characterId = _nextCharacterId++,
                name = "ElfNova",
                classId = "FairyElf",
                level = 22,
                powerScore = 7640,
                mapId = 2,
                mapName = "Noria",
                isLastPlayed = false
            });
        }

        private bool ExistsByName(string candidate)
        {
            for (int i = 0; i < _characters.Count; i++)
            {
                if (string.Equals(_characters[i].name, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private CharacterSummaryDto FindById(int characterId)
        {
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].characterId == characterId)
                    return _characters[i];
            }

            return null;
        }

        private static CharacterSelectOperationResultDto Fail(string message)
        {
            return new CharacterSelectOperationResultDto
            {
                success = false,
                message = message,
                characterId = 0
            };
        }
    }
}
