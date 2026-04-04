namespace MuLike.Server.Game.Entities
{
    public sealed class MonsterEntity : Entity
    {
        public int MonsterTypeId { get; }
        public string Name { get; }

        public MonsterEntity(int id, int monsterTypeId, string name, float x, float y, float z)
            : base(id, x, y, z)
        {
            MonsterTypeId = monsterTypeId;
            Name = name;
            HpMax = 50;
            HpCurrent = HpMax;
        }
    }
}
