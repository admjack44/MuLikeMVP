namespace MuLike.Server.Game.Entities
{
    public abstract class Entity
    {
        public int Id { get; }
        public float X { get; protected set; }
        public float Y { get; protected set; }
        public float Z { get; protected set; }
        public int HpCurrent { get; protected set; }
        public int HpMax { get; protected set; }

        protected Entity(int id, float x, float y, float z)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
        }

        public virtual void SetPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public virtual int ApplyDamage(int damage)
        {
            int applied = damage < 0 ? 0 : damage;
            HpCurrent -= applied;
            if (HpCurrent < 0) HpCurrent = 0;
            return applied;
        }

        public virtual int Heal(int amount)
        {
            int heal = amount < 0 ? 0 : amount;
            HpCurrent += heal;
            if (HpCurrent > HpMax) HpCurrent = HpMax;
            return heal;
        }

        public bool IsDead()
        {
            return HpCurrent <= 0;
        }
    }
}
