using MuLike.Data.Catalogs;
using UnityEngine;

namespace MuLike.ContentPipeline.Authoring
{
    [CreateAssetMenu(fileName = "GameContentPipelineProfile", menuName = "MuLike/Content Pipeline/Pipeline Profile")]
    public sealed class GameContentPipelineProfile : ScriptableObject
    {
        [Header("Catalog Sources")]
        public ItemCatalogDatabase itemCatalog;
        public MonsterCatalogDatabase monsterCatalog;
        public SkillCatalogDatabase skillCatalog;
        public MapCatalogDatabase mapCatalog;
        public SpawnTableDatabase spawnTableDatabase;
        public DropTableDatabase dropTableDatabase;
        public BalanceConfigAsset balanceConfig;

        [Header("Export")]
        public string bundleVersion = "1.0.0";
        public string outputAssetPath = "Assets/Resources/Data/Content/server_content_bundle.json";
        public string resourcePath = "Data/Content/server_content_bundle";
    }
}
