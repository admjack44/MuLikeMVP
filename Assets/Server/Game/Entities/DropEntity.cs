namespace MuLike.Server.Game.Entities
{
    public sealed class DropEntity : Entity
    {
        public int ItemId { get; }
        public int Quantity { get; private set; }

        public DropEntity(int id, int itemId, int quantity, float x, float y, float z)
            : base(id, x, y, z)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public void Take(int amount)
        {
            Quantity -= amount;
            if (Quantity < 0) Quantity = 0;
        }
    }
}
