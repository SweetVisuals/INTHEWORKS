using UnityEngine;

namespace IsometricGame.UI
{
    /// <summary>
    /// Utility for robustly loading pixel art UI sprites in Editor and Runtime,
    /// with multi-tier fallbacks (Direct Sprite -> Sub-Asset -> Dynamic Texture2D Slice).
    /// </summary>
    public static class UISpriteUtility
    {
        public static Sprite LoadSprite(string path, Vector4? border = null)
        {
#if UNITY_EDITOR
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;

            var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (all != null)
            {
                foreach (var a in all)
                {
                    if (a is Sprite sp) return sp;
                }
            }

            Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                Vector4 b = border.HasValue ? border.Value : Vector4.zero;
                Sprite generated = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, b);
                generated.name = System.IO.Path.GetFileNameWithoutExtension(path);
                return generated;
            }
#endif
            return null;
        }

        public static Sprite[] LoadSpriteFrames(string path, int frameWidth, int frameHeight, int frameCount)
        {
#if UNITY_EDITOR
            Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                Sprite[] frames = new Sprite[frameCount];
                for (int i = 0; i < frameCount; i++)
                {
                    int x = i * frameWidth;
                    int y = tex.height - frameHeight; // Top-aligned strip
                    if (x + frameWidth <= tex.width)
                    {
                        frames[i] = Sprite.Create(tex, new Rect(x, 0, frameWidth, frameHeight), new Vector2(0.5f, 0.5f), 32f);
                        frames[i].name = $"{System.IO.Path.GetFileNameWithoutExtension(path)}_{i}";
                    }
                }
                return frames;
            }
#endif
            return null;
        }

        public static Sprite LoadSpriteRect(string path, Rect rect, Vector2? pivot = null)
        {
#if UNITY_EDITOR
            Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                Vector2 piv = pivot.HasValue ? pivot.Value : new Vector2(0.5f, 0.5f);
                Sprite s = Sprite.Create(tex, rect, piv, 32f);
                s.name = $"{System.IO.Path.GetFileNameWithoutExtension(path)}_{rect.x}_{rect.y}";
                return s;
            }
#endif
            return null;
        }
    }
}