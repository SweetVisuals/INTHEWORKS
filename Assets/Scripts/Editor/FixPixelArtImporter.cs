#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace IsometricGame.Editor
{
    [InitializeOnLoad]
    public static class FixPixelArtImporter
    {
        static FixPixelArtImporter()
        {
            EditorApplication.delayCall += ForcePointFilterAllSprites;
        }

        [MenuItem("GameObject/2D Isometric/Force Crisp Point Filter On All Sprites", false, 50)]
        public static void ForcePointFilterAllSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new string[] { "Assets/Sprites", "Assets" });
            int fixedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".png") && !path.EndsWith(".jpg")) continue;

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    bool modified = false;

                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        modified = true;
                    }

                    if (importer.filterMode != FilterMode.Point)
                    {
                        importer.filterMode = FilterMode.Point;
                        modified = true;
                    }

                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        modified = true;
                    }

                    if (importer.spritePixelsPerUnit != 32f)
                    {
                        importer.spritePixelsPerUnit = 32f;
                        modified = true;
                    }

                    if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        modified = true;
                    }

                    if (importer.spritePivot != new Vector2(0.5f, 0.5f))
                    {
                        importer.spritePivot = new Vector2(0.5f, 0.5f);
                        modified = true;
                    }

                    TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();
                    if (defaultSettings.format != TextureImporterFormat.RGBA32 || defaultSettings.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        defaultSettings.format = TextureImporterFormat.RGBA32;
                        defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SetPlatformTextureSettings(defaultSettings);
                        modified = true;
                    }

                    if (modified)
                    {
                        importer.SaveAndReimport();
                        fixedCount++;
                    }
                }
            }

            Debug.Log($"<color=green>[Pixel Art Fixer]</color> Verified and reimported {fixedCount} sprites with Point filter.");
        }
    }
}
#endif
