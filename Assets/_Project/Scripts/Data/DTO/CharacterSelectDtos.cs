using System;

namespace MuLike.Data.DTO
{
    [Serializable]
    public class CharacterSummaryDto
    {
        public int characterId;
        public string name;
        public string classId;
        public int level;
        public int powerScore;
        public int mapId;
        public string mapName;
        public bool isLastPlayed;
    }

    [Serializable]
    public class CreateCharacterRequestDto
    {
        public string name;
        public string classId;
    }

    [Serializable]
    public class CharacterSelectOperationResultDto
    {
        public bool success;
        public string message;
        public int characterId;
    }

    [Serializable]
    public class EnterWorldResultDto
    {
        public bool success;
        public string message;
        public int characterId;
        public string sceneName;
        public int mapId;
    }

    [Serializable]
    public class CharacterListResponseDto
    {
        public bool success;
        public string message;
        public CharacterSummaryDto[] characters;
    }

    [Serializable]
    public class DeleteCharacterRequestDto
    {
        public int characterId;
    }

    [Serializable]
    public class EnterWorldRequestDto
    {
        public int characterId;
    }
}
