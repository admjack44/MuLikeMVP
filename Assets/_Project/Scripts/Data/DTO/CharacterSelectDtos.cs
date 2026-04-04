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
        public int mapId;
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
}
