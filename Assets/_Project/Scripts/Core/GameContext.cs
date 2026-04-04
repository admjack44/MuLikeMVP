using MuLike.Systems;

namespace MuLike.Core
{
    /// <summary>
    /// Central hub that holds references to all major runtime systems.
    /// </summary>
    public static class GameContext
    {
        public static bool IsInitialized { get; private set; }
        public static CatalogResolver CatalogResolver { get; private set; }

        public static void Initialize()
        {
            if (IsInitialized) return;

            CatalogResolver = new CatalogResolver();
            CatalogResolver.LoadItemCatalog();

            IsInitialized = true;
        }

        public static void Reset()
        {
            CatalogResolver?.Clear();
            CatalogResolver = null;
            IsInitialized = false;
        }
    }
}
