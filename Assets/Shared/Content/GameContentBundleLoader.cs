using UnityEngine;

namespace MuLike.Shared.Content
{
    public static class GameContentBundleLoader
    {
        public static bool TryLoadFromResources(string resourcePath, out GameContentBundleDto bundle)
        {
            bundle = null;
            if (string.IsNullOrWhiteSpace(resourcePath))
                return false;

            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
                return false;

            bundle = JsonUtility.FromJson<GameContentBundleDto>(asset.text);
            return bundle != null;
        }
    }
}
