#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MuLike.Tools.Editor
{
    public static class MobileTextureImportTool
    {
        [MenuItem("MuLike/Performance/Apply Mobile Texture Settings")]
        public static void ApplyMobileTextureSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project" });
            int updated = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                bool dirty = false;

                if (path.Contains("/UI/"))
                {
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        dirty = true;
                    }

                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        dirty = true;
                    }
                }
                else
                {
                    if (!importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = true;
                        dirty = true;
                    }
                }

                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = Mathf.Min(android.maxTextureSize <= 0 ? 1024 : android.maxTextureSize, 2048);
                android.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(android);

                if (dirty)
                {
                    updated++;
                    importer.SaveAndReimport();
                }
            }

            Debug.Log($"[MobileTextureImportTool] Updated textures: {updated}/{guids.Length}");
        }
    }
}
#endif
