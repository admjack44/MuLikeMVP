using System.Collections.Generic;

namespace MuLike.Shared.Items
{
    /// <summary>
    /// Single source of truth for item ID ranges and catalog versioning.
    ///
    /// Both client (ItemCatalogLoader) and server (ItemDatabase / ServerItemCatalogBridge)
    /// reference this class to guarantee IDs never collide across categories and that both
    /// sides are loading the same catalog version at startup.
    ///
    /// Range assignment rule:
    ///   - Each category owns a non-overlapping 1 000-unit block.
    ///   - New content must stay within its category's block.
    ///   - Bump CatalogVersion whenever ranges change or IDs are retired/recycled.
    /// </summary>
    public static class ItemCatalogSyncPolicy
    {
        /// <summary>
        /// Increment whenever the ID layout changes incompatibly.
        /// Server and client compare this at startup via ItemDatabase.CatalogVersion.
        /// </summary>
        public const int CatalogVersion = 1;

        /// <summary>Layout identifier embedded in the items manifest JSON.</summary>
        public const string LayoutId = "mu-arpg-items-v1";

        // -----------------------------------------------------------------------
        // ID ranges — each block is 1 000 units wide.
        // -----------------------------------------------------------------------
        public const int RangeMaterialsMin    = 1000;
        public const int RangeMaterialsMax    = 1999;

        public const int RangeAccessoriesMin  = 2000;
        public const int RangeAccessoriesMax  = 2999;

        public const int RangeEquipmentMin    = 3000;   // weapons, shields, armors
        public const int RangeEquipmentMax    = 6999;

        public const int RangeWingsMin        = 7000;
        public const int RangeWingsMax        = 7499;

        public const int RangeCostumesMin     = 7500;
        public const int RangeCostumesMax     = 7999;

        public const int RangeConsumablesMin  = 8000;
        public const int RangeConsumablesMax  = 8999;

        public const int RangeQuestItemsMin   = 9000;
        public const int RangeQuestItemsMax   = 9499;

        public const int RangePetsMin         = 9500;
        public const int RangePetsMax         = 9999;

        // -----------------------------------------------------------------------
        // Range queries
        // -----------------------------------------------------------------------
        public static bool IsMaterial(int itemId)
            => itemId >= RangeMaterialsMin && itemId <= RangeMaterialsMax;

        public static bool IsAccessory(int itemId)
            => itemId >= RangeAccessoriesMin && itemId <= RangeAccessoriesMax;

        public static bool IsEquipment(int itemId)
            => itemId >= RangeEquipmentMin && itemId <= RangeEquipmentMax;

        public static bool IsWings(int itemId)
            => itemId >= RangeWingsMin && itemId <= RangeWingsMax;

        public static bool IsCostume(int itemId)
            => itemId >= RangeCostumesMin && itemId <= RangeCostumesMax;

        public static bool IsConsumable(int itemId)
            => itemId >= RangeConsumablesMin && itemId <= RangeConsumablesMax;

        public static bool IsQuestItem(int itemId)
            => itemId >= RangeQuestItemsMin && itemId <= RangeQuestItemsMax;

        public static bool IsPet(int itemId)
            => itemId >= RangePetsMin && itemId <= RangePetsMax;

        public static bool IsInKnownRange(int itemId)
            => IsMaterial(itemId) || IsAccessory(itemId) || IsEquipment(itemId)
            || IsWings(itemId) || IsCostume(itemId) || IsConsumable(itemId)
            || IsQuestItem(itemId) || IsPet(itemId);

        // -----------------------------------------------------------------------
        // Sync validation
        // -----------------------------------------------------------------------

        /// <summary>
        /// Compares two sets of item IDs (e.g. client catalog vs server catalog) and returns
        /// all IDs that are present in one side but not the other.
        /// Use at server startup to catch catalog drift between builds.
        /// </summary>
        public static CatalogSyncReport Validate(
            IReadOnlyCollection<int> clientIds,
            IReadOnlyCollection<int> serverIds)
        {
            var clientSet = new HashSet<int>(clientIds ?? new int[0]);
            var serverSet = new HashSet<int>(serverIds ?? new int[0]);

            var onlyOnClient = new List<int>();
            var onlyOnServer = new List<int>();
            var outOfRange    = new List<int>();

            foreach (int id in clientSet)
            {
                if (!serverSet.Contains(id)) onlyOnClient.Add(id);
                if (!IsInKnownRange(id)) outOfRange.Add(id);
            }

            foreach (int id in serverSet)
            {
                if (!clientSet.Contains(id)) onlyOnServer.Add(id);
            }

            return new CatalogSyncReport(onlyOnClient, onlyOnServer, outOfRange);
        }
    }

    public sealed class CatalogSyncReport
    {
        /// <summary>IDs present in the client catalog but absent from the server catalog.</summary>
        public IReadOnlyList<int> OnlyOnClient { get; }

        /// <summary>IDs present in the server catalog but absent from the client catalog.</summary>
        public IReadOnlyList<int> OnlyOnServer { get; }

        /// <summary>IDs that fall outside every defined range (likely authoring errors).</summary>
        public IReadOnlyList<int> OutOfRange { get; }

        public bool IsInSync => OnlyOnClient.Count == 0 && OnlyOnServer.Count == 0;

        public CatalogSyncReport(
            IReadOnlyList<int> onlyOnClient,
            IReadOnlyList<int> onlyOnServer,
            IReadOnlyList<int> outOfRange)
        {
            OnlyOnClient = onlyOnClient;
            OnlyOnServer = onlyOnServer;
            OutOfRange   = outOfRange;
        }
    }
}

