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

                    if (!importer.isReadable)
                    {
                        importer.isReadable = true;
                        modified = true;
                    }

                    Vector2 targetPivot = new Vector2(0.5f, 0.5f);
                    if (path.Contains("door") || path.Contains("handle"))
                    {
                        if (path.Contains("wooden door") || path.Contains("handle")) targetPivot = new Vector2(0.5625f, 0.265625f);
                        else targetPivot = new Vector2(0.6875f, 0.234375f);
                    }
                    else if (path.Contains("window")) targetPivot = new Vector2(0.3541667f, 0.40625f);
                    else if (path.Contains("desk"))
                    {
                        if (path.Contains("isometric desk")) targetPivot = new Vector2(0.3125f, 0.1875f);
                        else targetPivot = new Vector2(0.5f, 0.140625f);
                    }
                    else if (path.Contains("just screen glow") || path.Contains("computer scree hover outline") || path.Contains("screen hover outline") || path.Contains("monitor_glow"))
                    {
                        targetPivot = new Vector2(0.3125f, 0.1875f);
                    }
                    else if (path.Contains("bed"))
                    {
                        targetPivot = new Vector2(0.5416667f, 0.1875f);
                    }
                    else if (path.Contains("pine tree") || path.Contains("tree"))
                    {
                        targetPivot = new Vector2(0.5f, 0.2083333f);
                    }
                    else if (path.Contains("Character"))
                    {
                        if (path.Contains("idle"))
                        {
                            targetPivot = new Vector2(0.5f, 0.0625f); // 3px / 48px from bottom
                        }
                        else if (path.Contains("walking"))
                        {
                            targetPivot = new Vector2(0.5f, 0.171875f); // 11px / 64px from bottom
                        }
                        else
                        {
                            targetPivot = new Vector2(0.5f, 0.125f);
                        }
                    }

                    TextureImporterSettings settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    int expectedAlignment = path.Contains("GUI") ? (int)SpriteAlignment.Center : (int)SpriteAlignment.Custom;
                    if (settings.spriteAlignment != expectedAlignment || settings.spritePivot != targetPivot)
                    {
                        settings.spriteAlignment = expectedAlignment;
                        settings.spritePivot = targetPivot;
                        importer.SetTextureSettings(settings);
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
