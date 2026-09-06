using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IsometricGame.UI
{
    /// <summary>
    /// Renders numbers using pixel art digit sprites sliced from the numbers texture.
    /// Provides pixel-perfect scaling, pooling, comma formatting, and tint color controls.
    /// </summary>
    [ExecuteAlways]
    public class PixelNumberDisplay : MonoBehaviour
    {
        [Header("Source Texture & Sprites")]
        [Tooltip("The numbers texture (Assets/Sprites/numbers 1 - 9.png)")]
        [SerializeField] private Texture2D numbersTexture;

        [Header("Layout & Scale")]
        [Tooltip("Pixel scale factor for UI rendering (e.g. 4 = 12x20 px per digit).")]
        [SerializeField] private float digitScale = 4f;
        [Tooltip("Horizontal spacing between digits in pixels.")]
        [SerializeField] private float spacing = 4f;
        [Tooltip("Whether to format numbers with commas (e.g., 25,000).")]
        [SerializeField] private bool formatCommas = true;

        [Header("Color")]
        [SerializeField] private Color digitColor = Color.white;

        [Header("Container")]
        [SerializeField] private RectTransform digitsContainer;

        private Dictionary<char, Sprite> digitSpriteMap = new Dictionary<char, Sprite>();
        private List<Image> digitImages = new List<Image>();
        private long currentValue = 0;

        public long CurrentValue => currentValue;
        public float DigitScale { get => digitScale; set { digitScale = value; Refresh(); } }
        public Color DigitColor { get => digitColor; set { digitColor = value; UpdateColors(); } }
        public bool FormatCommas { get => formatCommas; set { formatCommas = value; SetValue(currentValue); } }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            SetValue(currentValue);
        }

        public void Initialize()
        {
            if (digitsContainer == null)
            {
                digitsContainer = GetComponent<RectTransform>();
            }

            EnsureTextureLoaded();
            BuildDigitSprites();
            CacheExistingImages();
        }

        private void EnsureTextureLoaded()
        {
#if UNITY_EDITOR
            if (numbersTexture == null)
            {
                numbersTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/GUI/numbers 1 - 9.png")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/numbers 1 - 9.png");
            }
#endif
        }

        public void BuildDigitSprites()
        {
            if (numbersTexture == null) return;
            digitSpriteMap.Clear();

            // Texture is 48x16
            // In Unity bottom-up coordinates:
            // Digits 0..9 are at Y = 7 (height 5)
            // Digit 0: X = 2
            // Digit 1..9: X = 6 + (d - 1) * 4
            for (int d = 0; d <= 9; d++)
            {
                int sx = (d == 0) ? 2 : (6 + (d - 1) * 4);
                Rect rect = new Rect(sx, 7, 3, 5);
                Sprite digitSprite = Sprite.Create(
                    numbersTexture,
                    rect,
                    new Vector2(0.5f, 0.5f),
                    32f,
                    0,
                    SpriteMeshType.FullRect
                );
                digitSprite.name = $"Digit_{d}";
                digitSpriteMap[(char)('0' + d)] = digitSprite;
            }

            // Comma ',' at X = 41, Y = 6, W = 3, H = 6
            Rect commaRect = new Rect(41, 6, 3, 6);
            Sprite commaSprite = Sprite.Create(
                numbersTexture,
                commaRect,
                new Vector2(0.5f, 0.5f),
                32f,
                0,
                SpriteMeshType.FullRect
            );
            commaSprite.name = "Glyph_Comma";
            digitSpriteMap[','] = commaSprite;
        }

        private void CacheExistingImages()
        {
            if (digitsContainer == null) return;

            digitImages.Clear();
            for (int i = 0; i < digitsContainer.childCount; i++)
            {
                Transform child = digitsContainer.GetChild(i);
                if (child.TryGetComponent<Image>(out var img))
                {
                    digitImages.Add(img);
                }
            }
        }

        public void SetValue(long amount)
        {
            currentValue = amount;
            if (digitSpriteMap.Count == 0)
            {
                Initialize();
            }

            string text = formatCommas ? amount.ToString("#,##0") : amount.ToString();
            RenderString(text);
        }

        public void RenderString(string text)
        {
            if (digitsContainer == null) return;

            // Ensure we have enough Image components
            while (digitImages.Count < text.Length)
            {
                GameObject digitObj = new GameObject($"Digit_{digitImages.Count}", typeof(RectTransform), typeof(Image));
                digitObj.transform.SetParent(digitsContainer, false);

                Image img = digitObj.GetComponent<Image>() ?? digitObj.AddComponent<Image>();
                img.raycastTarget = false;
                digitImages.Add(img);
            }

            float currentX = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                Image img = digitImages[i];
                img.gameObject.SetActive(true);

                if (digitSpriteMap.TryGetValue(c, out Sprite sprite))
                {
                    img.sprite = sprite;
                    img.color = digitColor;

                    RectTransform rt = img.rectTransform;
                    rt.anchorMin = new Vector2(0, 0.5f);
                    rt.anchorMax = new Vector2(0, 0.5f);
                    rt.pivot = new Vector2(0, 0.5f);

                    float w = sprite.rect.width * digitScale;
                    float h = sprite.rect.height * digitScale;
                    rt.sizeDelta = new Vector2(w, h);

                    // Align baseline
                    float yOffset = (c == ',') ? -1.5f * digitScale : 0f;
                    rt.anchoredPosition = new Vector2(currentX, yOffset);

                    currentX += w + spacing;
                }
                else
                {
                    img.gameObject.SetActive(false);
                }
            }

            // Deactivate leftover images
            for (int i = text.Length; i < digitImages.Count; i++)
            {
                digitImages[i].gameObject.SetActive(false);
            }

            // Adjust container width to snugly fit digits
            float totalWidth = Mathf.Max(0f, currentX - spacing);
            digitsContainer.sizeDelta = new Vector2(totalWidth, 5 * digitScale);

            LayoutElement le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            le.minWidth = totalWidth;
            le.preferredWidth = totalWidth;
            le.minHeight = 5 * digitScale;
            le.preferredHeight = 5 * digitScale;

            if (transform.parent is RectTransform parentRt)
            {
                LayoutRebuilder.MarkLayoutForRebuild(parentRt);
            }
        }

        public void UpdateColors()
        {
            for (int i = 0; i < digitImages.Count; i++)
            {
                if (digitImages[i] != null && digitImages[i].gameObject.activeSelf)
                {
                    digitImages[i].color = digitColor;
                }
            }
        }

        public void Refresh()
        {
            BuildDigitSprites();
            SetValue(currentValue);
        }
    }
}
