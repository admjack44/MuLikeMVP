namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// UI-ready immutable payload for character summary rendering.
    /// </summary>
    public readonly struct CharacterSummaryViewData
    {
        public readonly int CharacterId;
        public readonly string Name;
        public readonly string ClassId;
        public readonly int Level;
        public readonly int PowerScore;
        public readonly int MapId;
        public readonly string MapName;
        public readonly bool IsLastPlayed;

        public CharacterSummaryViewData(
            int characterId,
            string name,
            string classId,
            int level,
            int powerScore,
            int mapId,
            string mapName,
            bool isLastPlayed)
        {
            CharacterId = characterId;
            Name = name ?? string.Empty;
            ClassId = classId ?? string.Empty;
            Level = level < 1 ? 1 : level;
            PowerScore = powerScore < 0 ? 0 : powerScore;
            MapId = mapId < 0 ? 0 : mapId;
            MapName = mapName ?? string.Empty;
            IsLastPlayed = isLastPlayed;
        }
    }
}