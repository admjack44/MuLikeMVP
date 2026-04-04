namespace MuLike.Server.Game.Entities
{
    public sealed class PlayerEntity : Entity
    {
        public int AccountId { get; }
        public string Name { get; }
        public int Level { get; private set; }

        public PlayerEntity(int id, int accountId, string name, float x, float y, float z)
            : base(id, x, y, z)
        {
            AccountId = accountId;
            Name = name;
            Level = 1;
            HpMax = 100;
            HpCurrent = HpMax;
        }

        public void SetLevel(int level)
        {
            Level = level;
        }
    }
}
