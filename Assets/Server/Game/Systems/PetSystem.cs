namespace MuLike.Server.Game.Systems
{
    public sealed class PetSystem
    {
        public bool CanSummon(bool hasPetItem, bool alreadySummoned)
        {
            return hasPetItem && !alreadySummoned;
        }
    }
}
