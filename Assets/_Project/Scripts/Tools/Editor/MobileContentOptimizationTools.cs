#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace MuLike.Tools.Editor
{
    public static class MobileContentOptimizationTools
    {
        private const string AtlasOutputFolder = "Assets/_Project/Art/Atlases";

        [MenuItem("MuLike/Performance/Content/Apply Android Texture Compression (Selected)")]
        public static void ApplyAndroidTextureCompressionForSelection()
        {
            string[] selectedGuids = Selection.assetGUIDs;
            if (selectedGuids == null || selectedGuids.Length == 0)
            {
                Debug.LogWarning("[MobileContentOptimization] Select textures or folders first.");
                return;
            }

            int processed = 0;
            int changed = 0;
            var seen = new HashSet<string>();

            for (int i = 0; i < selectedGuids.Length; i++)
            {
                string rootPath = AssetDatabase.GUIDToAssetPath(selectedGuids[i]);
                if (string.IsNullOrWhiteSpace(rootPath))
                    continue;

                string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { rootPath });
                for (int j = 0; j < textureGuids.Length; j++)
                {
                    string texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[j]);
                    if (!seen.Add(texturePath))
                        continue;

                    processed++;
                    if (ApplyTextureCompression(texturePath))
                        changed++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MobileContentOptimization] Texture compression done. Processed={processed}, Changed={changed}");
        }

        [MenuItem("MuLike/Performance/Content/Create Mobile Sprite Atlases")]
        public static void CreateMobileSpriteAtlases()
        {
            EnsureFolder(AtlasOutputFolder);

            CreateAtlas(
                atlasPath: AtlasOutputFolder + "/UI_Main.spriteatlasv2",
                inputFolders: new[] { "Assets/_Project/Art/UI", "Assets/_Project/Art/Icons" },
                maxSize: 2048,
                blockOffset: 2,
                padding: 4,
                androidFormat: TextureImporterFormat.ASTC_6x6);

            CreateAtlas(
                atlasPath: AtlasOutputFolder + "/VFX_UI.spriteatlasv2",
                inputFolders: new[] { "Assets/_Project/Art/VFX", "Assets/_Project/Art/Skills" },
                maxSize: 2048,
                blockOffset: 2,
                padding: 2,
                androidFormat: TextureImporterFormat.ASTC_8x8);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MobileContentOptimization] Sprite atlas creation finished.");
        }

        private static bool ApplyTextureCompression(string texturePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                return false;

            bool isUiLike = texturePath.Contains("/UI/") || texturePath.Contains("/Icons/");
            TextureImporterFormat format = isUiLike ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_8x8;
            int maxSize = isUiLike ? 2048 : 1024;

            bool changed = false;

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Compressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                changed = true;
            }

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != format || android.maxTextureSize != maxSize)
            {
                android.overridden = true;
                android.maxTextureSize = maxSize;
                android.format = format;
                android.compressionQuality = 50;
                importer.SetPlatformTextureSettings(android);
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();

            return changed;
        }

        private static void CreateAtlas(
            string atlasPath,
            string[] inputFolders,
            int maxSize,
            int blockOffset,
            int padding,
            TextureImporterFormat androidFormat)
        {
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }

            var packing = atlas.GetPackingSettings();
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = padding;
            packing.blockOffset = blockOffset;
            atlas.SetPackingSettings(packing);

            var texture = atlas.GetTextureSettings();
            texture.generateMipMaps = false;
            texture.readable = false;
            texture.sRGB = true;
            texture.filterMode = FilterMode.Bilinear;
            atlas.SetTextureSettings(texture);

            var androidSettings = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                format = androidFormat,
                maxTextureSize = maxSize,
                compressionQuality = 50
            };
            atlas.SetPlatformSettings(androidSettings);

            var standaloneSettings = new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                format = TextureImporterFormat.DXT5,
                maxTextureSize = maxSize,
                compressionQuality = 50
            };
            atlas.SetPlatformSettings(standaloneSettings);

            var packables = new List<Object>();
            for (int i = 0; i < inputFolders.Length; i++)
            {
                string folder = inputFolders[i];
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;

                Object folderObject = AssetDatabase.LoadAssetAtPath<Object>(folder);
                if (folderObject != null)
                    packables.Add(folderObject);
            }

            atlas.Remove(atlas.GetPackables());
            if (packables.Count > 0)
                atlas.Add(packables.ToArray());

            EditorUtility.SetDirty(atlas);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
#endif
