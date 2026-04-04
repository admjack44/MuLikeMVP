namespace MuLike.Server.Game.Entities
{
    public sealed class PetEntity : Entity
    {
        public int OwnerPlayerId { get; }
        public int PetTypeId { get; }

        public PetEntity(int id, int ownerPlayerId, int petTypeId, float x, float y, float z)
            : base(id, x, y, z)
        {
            OwnerPlayerId = ownerPlayerId;
            PetTypeId = petTypeId;
            HpMax = 80;
            HpCurrent = HpMax;
        }
    }
}
